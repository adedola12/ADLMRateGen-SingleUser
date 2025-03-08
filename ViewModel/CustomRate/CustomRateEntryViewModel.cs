using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Services;

namespace ADLMRateGen.ViewModel.CustomRate
{
    public class CustomRateEntryViewModel: ViewModelBase
    {
		private string _rateName;
		public string RateName
		{
			get => _rateName;
			set
			{
				if (_rateName != value)
				{
					_rateName = value;
					RaisePropertyChanged(nameof(RateName));
				}
			}
		}

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
			var item = new RateEntryItem
			{
				RateType = RateItemType.Material,
				Quantity = 0,
				UnitPrice = 0,
				Unit = "",
				Description = ""
			};
			MaterialItems.Add(item);
			RaisePropertyChanged(nameof(TotalMaterialCost));
            RaisePropertyChanged(nameof(OverallTotal));
            RaisePropertyChanged(nameof(GrandTotal));
        }
        private void AddLabourItem()
        {
			var item = new RateEntryItem
			{
				RateType = RateItemType.Labour,
				Quantity = 0,
				UnitPrice = 0,
				Unit = "",
				Description = ""
			};
			LabourItems.Add(item);
			RaisePropertyChanged(nameof (TotalLabourCost));
			RaisePropertyChanged(nameof(OverallTotal));
			RaisePropertyChanged(nameof(GrandTotal));
		}
        private void SaveCustomRate()
        {
            var newRate = new CustomRate
            {
				Title = RateName,
				Description = Description,
				MaterialItems = MaterialItems.ToList(),
				LabourItems = LabourItems.ToList(),
				OverheadPercent = this.OverheadPercent,
				ProfitPercent = this.ProfitPercent,
				CreatedDate = DateTime.Now
			};

            CustomRateServices.SaveCustomRate(newRate);

			MessageBox.Show("Custom Rate saved successfully!", "Save", MessageBoxButton.OK, MessageBoxImage.Information);

			// Clear out the fields
			RateName = string.Empty;
			Description = string.Empty;
			MaterialItems.Clear();
			LabourItems.Clear();
			OverheadPercent = 10;
			ProfitPercent = 10;

			RaisePropertyChanged(nameof(TotalMaterialCost));
			RaisePropertyChanged(nameof(TotalLabourCost));
			RaisePropertyChanged(nameof(OverallTotal));
			RaisePropertyChanged(nameof(GrandTotal));
		}
	}
}
