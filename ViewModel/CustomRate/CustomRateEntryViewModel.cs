using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Services;

namespace ADLMRateGen.ViewModel.CustomRate
{
    public class CustomRateEntryViewModel: ViewModelBase
    {
        public ObservableCollection<string> AvailableMaterials { get; } =
            new ObservableCollection<string>(MaterialLibraryService.GetAllMaterialNames());
        public ObservableCollection<string> AvailableLabourItems { get; } =
            new ObservableCollection<string>(LabourLibraryService.GetAllLabourNames());

        public ObservableCollection<RateEntryItem> MaterialItems { get; }=
            new ObservableCollection<RateEntryItem>();
        public ObservableCollection<RateEntryItem> LabourItems { get; } = 
            new ObservableCollection<RateEntryItem>();

        public decimal TotalMaterialCost => MaterialItems.Sum(item => item.TotalCost);
        public decimal TotalLabourCost => LabourItems.Sum(item => item.TotalCost);
        public decimal OverallTotal => TotalLabourCost + TotalMaterialCost;

        public decimal GrandTotal => OverallTotal * (1 + (OverheadPercent + ProfitPercent) / 100);

        private decimal _overheadPercent = 10;
        public decimal OverheadPercent
        {
            get => _overheadPercent;
            set
            {
                if (_overheadPercent != value)
                {
                    _overheadPercent = value;
                    RaisePropertyChanged(nameof(OverheadPercent));
                    RaisePropertyChanged(nameof(GrandTotal));
                }
            }
        }

        private decimal _profitPercent = 10;
        public decimal ProfitPercent
        {
            get => _profitPercent;
            set
            {
                if (_profitPercent != value)
                {
                    _profitPercent = value;
                    RaisePropertyChanged(nameof(ProfitPercent));
                    RaisePropertyChanged(nameof(GrandTotal));
                }
            }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    RaisePropertyChanged(nameof(Description));
                }
            }
        }

        public ICommand AddMaterialItemCommand { get; }
        public ICommand AddLabourItemCommand { get; }
        public ICommand SaveCustomRateCommand { get; }

        public CustomRateEntryViewModel()
        {
			AddMaterialItemCommand = new RelayCommand(AddMaterialItem);
			AddLabourItemCommand = new RelayCommand(AddLabourItem);
			SaveCustomRateCommand = new RelayCommand(SaveCustomRate);

		}

		private void AddMaterialItem()
        {
            MaterialItems.Add(new RateEntryItem());
            RaisePropertyChanged(nameof(TotalMaterialCost));
            RaisePropertyChanged(nameof(OverallTotal));
            RaisePropertyChanged(nameof(GrandTotal));
        }
        private void AddLabourItem()
        {
            LabourItems.Add(new RateEntryItem());
            RaisePropertyChanged(nameof (TotalLabourCost));
			RaisePropertyChanged(nameof(OverallTotal));
			RaisePropertyChanged(nameof(GrandTotal));
		}
        private void SaveCustomRate()
        {
            var newRate = new CustomRate
            {
				Description = Description,
				MaterialItems = MaterialItems.ToList(),
				LabourItems = LabourItems.ToList(),
				OverheadPercent = this.OverheadPercent,
				ProfitPercent = this.ProfitPercent,
				CreatedDate = DateTime.Now
			};

            CustomRateServices.SaveCustomRate(newRate);
        }
	}
}
