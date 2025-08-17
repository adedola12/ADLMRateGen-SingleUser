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

        private void DeleteLabour(object o)
        {
            if (o is LabourModel labour)
            {
                LabourLibrary.Remove(labour);
                ReassignSerialNumbers();
                _ds.SaveLabours(LabourLibrary);
                LabourLibraryService.Initialize();
                LibraryChanged?.Invoke();
                ApplyFilter();
            }
        }

        private void ReassignSerialNumbers()
        {
            for (int i = 0; i < LabourLibrary.Count; i++)
                LabourLibrary[i].SerialNumber = i + 1;
        }

        private void EditLabour(object o)
        {
            if (o is LabourModel labour)
                EditLabourRequested?.Invoke(labour);
        }

        public ObservableCollection<LabourModel> Labours { get; } = new();

        /* ───────── CRUD helpers ───────── */
        public void AddOrUpdateLabour(LabourModel lab)
        {
            /* give a new serial if it comes in fresh */
            if (lab.SerialNumber == 0)
                lab.SerialNumber = LabourLibrary.Count == 0
                                   ? 1
                                   : LabourLibrary.Max(l => l.SerialNumber) + 1;

            var existing = LabourLibrary.FirstOrDefault(l => l.SerialNumber == lab.SerialNumber);

            if (existing == null)                     // *** ADD ***
            {
                LabourLibrary.Add(lab);
            }
            else                                      // *** UPDATE ***
            {
                existing.LabourUnit = lab.LabourUnit;
                existing.LabourPrice = lab.LabourPrice;
                existing.LabourCategory = lab.LabourCategory;
            }

            /* persist + refresh the grid */
            _ds.SaveLabours(LabourLibrary);
            LabourLibraryService.Initialize();
            LabourCollectionView.Refresh();
            LibraryChanged?.Invoke();
        }

        private void UpdatePricesFromMongo()
        {
            var result = MessageBox.Show(
                "Override prices with ADLM server values?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var mongo = new LabourMongoDataSource(
                    "mongodb+srv://dolapo836:[REDACTED]@adlmratedb.zeur8.mongodb.net/?retryWrites=true&w=majority&appName=ADLMRateDB",
                    "ADLMRateDB",
                    "labours"
                );
                var serverList = mongo.LoadLabours().ToList();

                foreach (var local in LabourLibrary)
                {
                    var found = serverList.FirstOrDefault(s => s.LabourName == local.LabourName);
                    if (found != null)
                        local.LabourPrice = found.LabourPrice;
                }

                _ds.SaveLabours(LabourLibrary);
                LabourLibraryService.Initialize();
                LibraryChanged?.Invoke();
                ApplyFilter();
                MessageBox.Show("Updated from server.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to server: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenNewLabourDialog() =>
            MessageBox.Show("TODO: add new labour");
    }
}
