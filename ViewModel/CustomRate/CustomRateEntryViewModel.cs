using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;                    // ← for Debug.WriteLine
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel.CustomRate
{
    public class CustomRateEntryViewModel : ViewModelBase
    {
        private string _rateName;
        public string RateName
        {
            get => _rateName;
            set { if (_rateName != value) { _rateName = value; RaisePropertyChanged(nameof(RateName)); } }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set { if (_isEditing != value) { _isEditing = value; RaisePropertyChanged(nameof(IsEditing)); } }
        }

        private CustomRate _currentRate;
        private Guid _originalId;

        // Start empty; we’ll populate in RefreshLookups()
        public ObservableCollection<string> AvailableMaterials { get; } = new();
        public ObservableCollection<string> AvailableLabourItems { get; } = new();

        public ObservableCollection<RateEntryItem> MaterialItems { get; } = new();
        public ObservableCollection<RateEntryItem> LabourItems { get; } = new();

        public event Action Saved;

        public decimal TotalMaterialCost => MaterialItems.Sum(item => item.TotalCost);
        public decimal TotalLabourCost => LabourItems.Sum(item => item.TotalCost);
        public decimal OverallTotal => TotalLabourCost + TotalMaterialCost;
        public decimal GrandTotal => OverallTotal * (1 + (OverheadPercent + ProfitPercent) / 100);

        private void RecomputeAll()
        {
            RaisePropertyChanged(nameof(TotalMaterialCost));
            RaisePropertyChanged(nameof(TotalLabourCost));
            RaisePropertyChanged(nameof(OverallTotal));
            RaisePropertyChanged(nameof(GrandTotal));
        }

        private decimal _overheadPercent = 10;
        public decimal OverheadPercent
        {
            get => _overheadPercent;
            set { if (_overheadPercent != value) { _overheadPercent = value; RaisePropertyChanged(nameof(OverheadPercent)); RaisePropertyChanged(nameof(GrandTotal)); } }
        }

        private decimal _profitPercent = 10;
        public decimal ProfitPercent
        {
            get => _profitPercent;
            set { if (_profitPercent != value) { _profitPercent = value; RaisePropertyChanged(nameof(ProfitPercent)); RaisePropertyChanged(nameof(GrandTotal)); } }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value; RaisePropertyChanged(nameof(Description)); } }
        }

        public ICommand AddMaterialItemCommand { get; }
        public ICommand AddLabourItemCommand { get; }
        public ICommand SaveCustomRateCommand { get; }
        public event Action<CustomRate> OnViewRateRequested;

        public CustomRateEntryViewModel()
        {
            // Build lookups after services are initialized (App.OnStartup)
            RefreshLookups();

            MaterialItems.CollectionChanged += OnMaterialCollectionChanged;
            LabourItems.CollectionChanged   += OnLabourCollectionChanged;

            AddMaterialItemCommand = new RelayCommand(_ => AddMaterialItem());
            AddLabourItemCommand   = new RelayCommand(_ => AddLabourItem());
            SaveCustomRateCommand  = new RelayCommand(_ => SaveCustomRate());

            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CurrencyService.Rate) or nameof(CurrencyService.Code))
                    RecomputeAll();
            };
        }

        public void RefreshLookups()
        {
            // MATERIALS
            AvailableMaterials.Clear();
            MaterialLibraryService.Initialize(); // safe; reloads the cached list
            foreach (var n in MaterialLibraryService.GetAllMaterialNames())
                AvailableMaterials.Add(n);

            // LABOUR
            AvailableLabourItems.Clear();
            LabourLibraryService.Initialize();   // ensure loaded
            foreach (var n in LabourLibraryService.GetAllLabourNames())
                AvailableLabourItems.Add(n);

            Debug.WriteLine($"[CustomRateEntryVM] materials={AvailableMaterials.Count}, labour={AvailableLabourItems.Count}");
            Debug.WriteLine($"[PATH] Labour file used by service: {AppPaths.LabourLibraryFile}");

        }

        private void OnMaterialCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (RateEntryItem i in e.NewItems) i.PropertyChanged += OnRateEntryItemChanged;
            if (e.OldItems != null) foreach (RateEntryItem i in e.OldItems) i.PropertyChanged -= OnRateEntryItemChanged;

            RaisePropertyChanged(nameof(TotalMaterialCost));
            RaisePropertyChanged(nameof(OverallTotal));
            RaisePropertyChanged(nameof(GrandTotal));
        }

        private void OnLabourCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (RateEntryItem i in e.NewItems) i.PropertyChanged += OnRateEntryItemChanged;
            if (e.OldItems != null) foreach (RateEntryItem i in e.OldItems) i.PropertyChanged -= OnRateEntryItemChanged;

            RaisePropertyChanged(nameof(TotalLabourCost));
            RaisePropertyChanged(nameof(OverallTotal));
            RaisePropertyChanged(nameof(GrandTotal));
        }

        private void OnRateEntryItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RateEntryItem.TotalCost) ||
                e.PropertyName == nameof(RateEntryItem.Quantity)  ||
                e.PropertyName == nameof(RateEntryItem.UnitPrice))
            {
                RaisePropertyChanged(nameof(TotalMaterialCost));
                RaisePropertyChanged(nameof(TotalLabourCost));
                RaisePropertyChanged(nameof(OverallTotal));
                RaisePropertyChanged(nameof(GrandTotal));
            }
        }

        public void LoadRate(CustomRate rate)
        {
            if (rate == null) return;
            IsEditing = true;
            _currentRate = rate;
            _originalId  = rate.Id;

            MaterialItems.Clear();
            LabourItems.Clear();

            RateName        = rate.Title;
            Description     = rate.Description;
            OverheadPercent = rate.OverheadPercent;
            ProfitPercent   = rate.ProfitPercent;

            foreach (var matItem in rate.MaterialItems)
                MaterialItems.Add(new RateEntryItem
                {
                    Description = matItem.Description,
                    Quantity    = matItem.Quantity,
                    Unit        = matItem.Unit,
                    UnitPrice   = matItem.UnitPrice,
                    RateType    = matItem.RateType
                });

            foreach (var labItem in rate.LabourItems)
                LabourItems.Add(new RateEntryItem
                {
                    Description = labItem.Description,
                    Quantity    = labItem.Quantity,
                    Unit        = labItem.Unit,
                    UnitPrice   = labItem.UnitPrice,
                    RateType    = labItem.RateType
                });
        }

        private void AddMaterialItem()
        {
            MaterialItems.Add(new RateEntryItem
            {
                RateType    = RateItemType.Material,
                Quantity    = 0,
                UnitPrice   = 0,
                Unit        = "",
                Description = ""
            });
            RaisePropertyChanged(nameof(TotalMaterialCost));
            RaisePropertyChanged(nameof(OverallTotal));
            RaisePropertyChanged(nameof(GrandTotal));
        }

        private void AddLabourItem()
        {
            LabourItems.Add(new RateEntryItem
            {
                RateType    = RateItemType.Labour,
                Quantity    = 0,
                UnitPrice   = 0,
                Unit        = "",
                Description = ""
            });
            RaisePropertyChanged(nameof(TotalLabourCost));
            RaisePropertyChanged(nameof(OverallTotal));
            RaisePropertyChanged(nameof(GrandTotal));
        }

        private void SaveCustomRate()
        {
            var newRate = new CustomRate
            {
                Id             = _originalId,
                Title          = RateName,
                Description    = Description,
                MaterialItems  = MaterialItems.ToList(),
                LabourItems    = LabourItems.ToList(),
                OverheadPercent= OverheadPercent,
                ProfitPercent  = ProfitPercent,
                CreatedDate    = IsEditing ? _currentRate.CreatedDate : DateTime.Now
            };

            if (!IsEditing)
            {
                newRate.Id = Guid.NewGuid();
                CustomRateServices.SaveCustomRate(newRate);
                MessageBox.Show("New Custom Rate saved successfully!", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                CustomRateServices.UpdateCustomRate(newRate);
                MessageBox.Show("Existing Custom Rate updated successfully!", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Saved?.Invoke();
            ClearForm();
        }

        public void StartNewRate()
        {
            ClearForm();
            IsEditing = false;
        }

        private void ClearForm()
        {
            RateName = string.Empty;
            Description = string.Empty;
            MaterialItems.Clear();
            LabourItems.Clear();
            OverheadPercent = 10;
            ProfitPercent = 10;
            IsEditing = false;
            _originalId = Guid.Empty;
        }

        private void ViewRate(CustomRate rate) => OnViewRateRequested?.Invoke(rate);
        private void UpdateTotals()
        {
            RaisePropertyChanged(nameof(TotalMaterialCost));
            RaisePropertyChanged(nameof(TotalLabourCost));
            RaisePropertyChanged(nameof(OverallTotal));
            RaisePropertyChanged(nameof(GrandTotal));
        }
    }
}
