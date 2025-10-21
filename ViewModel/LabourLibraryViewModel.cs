using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
    public class LabourLibraryViewModel : ViewModelBase
    {
        private readonly LabourJsonDataSource _ds = new(AppPaths.LabourLibraryFile);

        public ObservableCollection<LabourModel> LabourLibrary { get; }
        public ICollectionView LabourCollectionView { get; }

        /* --------  names for ComboBoxes  -------- */
        public static IEnumerable<string> GetAllLabourNames() =>
            LabourLibraryService.GetAllLabourNames();

        /* --------  price lookup for RateEntryItem  -------- */
        public static decimal GetPrice(string labourName) =>
            LabourLibraryService.GetPrice(labourName);

        public ObservableCollection<string> LabourCategory { get; }

        private string _selectedLabourCategory = "All";
        public string SelectedLabourCategory
        {
            get => _selectedLabourCategory;
            set
            {
                if (_selectedLabourCategory != value)
                {
                    _selectedLabourCategory = value;
                    RaisePropertyChanged();
                    ApplyFilter();
                }
            }
        }

        private string _searchTerm = string.Empty;
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (_searchTerm != value)
                {
                    _searchTerm = value;
                    RaisePropertyChanged();
                    ApplyFilter();
                }
            }
        }

        public ICommand SearchLabourCommand { get; }
        public ICommand ClearDatabaseCommand { get; }
        public ICommand DeleteLabourCommand { get; }
        public ICommand EditLabourCommand { get; }
        public ICommand UpdatePricesCommand { get; }

        // Fired when the user clicks “Edit” on a row
        public event Action<LabourModel> EditLabourRequested;
        public event Action LibraryChanged;

        /* --------  convert NGN → currently-selected currency  -------- */
        public double PriceNgnToCurrent(double baseNgn) =>
            baseNgn * CurrencyService.Instance.Rate;

        public LabourLibraryViewModel()
        {
            // Load from the shared AppData file
            LabourLibrary = new ObservableCollection<LabourModel>(_ds.LoadLabours());
            Debug.WriteLine($"[LabourLibraryVM] Loaded {LabourLibrary.Count} from {AppPaths.LabourLibraryFile}");

            ReassignSerialNumbers();   // keep S/N tidy

            /* 2. CollectionView for DataGrid + filter */
            LabourCollectionView = CollectionViewSource.GetDefaultView(LabourLibrary);
            LabourCollectionView.Filter = _ => true;
            ApplyFilter();

            LabourCategory = new ObservableCollection<string> { "All", "Labour", "Plant", "Small Plant" };

            SearchLabourCommand = new DelegateCommand(_ => ApplyFilter());
            ClearDatabaseCommand = new DelegateCommand(_ => ClearDatabase());

            DeleteLabourCommand = new DelegateCommand(o => DeleteLabour(o));
            EditLabourCommand = new DelegateCommand(o => EditLabour(o));

            UpdatePricesCommand = new DelegateCommand(_ => UpdatePricesFromMongo());

            /* when currency changes → redraw the grid */
            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CurrencyService.Rate))
                    LabourCollectionView.Refresh();
            };


            Debug.WriteLine($"[PATH] Labour file used by service: {AppPaths.LabourLibraryFile}");

        }

        /* ───────── filtering helper ───────── */
        private void ApplyFilter()
        {
            LabourCollectionView.Filter = o =>
            {
                if (o is not LabourModel lb) return false;

                var okCategory = SelectedLabourCategory == "All" ||
                                 string.IsNullOrEmpty(SelectedLabourCategory) ||
                                 lb.LabourCategory == SelectedLabourCategory;

                var okSearch = string.IsNullOrWhiteSpace(SearchTerm) ||
                               (lb.LabourName?.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0);

                return okCategory && okSearch;
            };
            LabourCollectionView.Refresh();
        }

        private void ClearDatabase()
        {
            LabourLibrary.Clear();
            _ds.SaveLabours(LabourLibrary);
            LabourLibraryService.Initialize(); // refresh static cache for other screens
            ApplyFilter();
        }

        private void EditLabour(object parameter)
        {
            if (parameter is LabourModel labour)
                EditLabourRequested?.Invoke(labour);
        }

        private void DeleteLabour(object parameter)
        {
            if (parameter is LabourModel labour)
            {
                LabourLibrary.Remove(labour);
                Persist();
            }
        }

        private void ReassignSerialNumbers()
        {
            for (int i = 0; i < LabourLibrary.Count; i++)
                LabourLibrary[i].SerialNumber = i + 1;
        }


        public ObservableCollection<LabourModel> Labours { get; } = new();

        /* ───────── CRUD helpers ───────── */
        public async void AddOrUpdateLabour(LabourModel lab)
        {
            var existing = LabourLibrary.FirstOrDefault(l => l.SerialNumber == lab.SerialNumber);
            if (existing == null)
            {
                lab.SerialNumber = LabourLibrary.Count == 0
                    ? 1
                    : LabourLibrary.Max(l => l.SerialNumber) + 1;

                LabourLibrary.Add(lab);
                Persist();

                _ = UserLibrarySync.Instance.TryAddLabourAsync(lab);
            }
            else
            {
                existing.LabourPrice = lab.LabourPrice;
                Persist();

                _ = UserLibrarySync.Instance.TryUpdateLabourAsync(existing);
            }
        }


        private void Persist()
        {
            _ds.SaveLabours(LabourLibrary);
            LabourLibraryService.Initialize(_ds);  // refresh service cache
            LibraryChanged?.Invoke();              // 🔔 notifies RateEntryItem
        }



        private async void UpdatePricesFromMongo()
        {
            var result = MessageBox.Show(
                "Override labour prices with ADLM server values for your current zone?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                string zone = ADLMRateGen.Properties.AppSettings.Zone ?? "";
                if (string.IsNullOrWhiteSpace(zone))
                {
                    MessageBox.Show("No user zone set. Please sign in again to sync your zone profile.");
                    return;
                }



                var auth = ADLMRateGen.Services.AuthProvider.Instance.Client;

                // NEW
                await UserLibrarySync.Instance.LoadAsync();

                var masterDoc = await auth.GetJsonAsync($"/rategen/master?zone={Uri.EscapeDataString(zone)}");
                var root = masterDoc.RootElement;

                if (root.TryGetProperty("labour", out var labs))
                    ADLMRateGen.Services.DataSourceCloudSync.SaveLaboursFromDto(labs);

                ReloadFromDisk();
                MessageBox.Show($"Labour prices updated for zone '{zone}'.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating labour prices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public void ReloadFromDisk()
        {
            LabourLibrary.Clear();
            foreach (var l in _ds.LoadLabours()) LabourLibrary.Add(l);

            LabourLibraryService.Initialize(_ds);

            ApplyFilter();
            LibraryChanged?.Invoke();
        }

        private void OpenNewLabourDialog() =>
            MessageBox.Show("TODO: add new labour");
    }
}
