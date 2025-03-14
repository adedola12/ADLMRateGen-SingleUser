using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ADLMRateGen.ViewModel.SteelWork
{
   public class SteelworkItem: ViewModelBase
    {
		private int _itemNo;
		private string? _description;
		private double _netCost;
		private double _outVal;
		private double _overheadValue;
		private double _profitValue;
		private double _totalCost;
		private string? _unit;
		public int ItemNo
		{
			get => _itemNo;
			set
			{
				if (_itemNo != value)
				{
					_itemNo = value;
					OnPropertyChanged(nameof(ItemNo));
				}
			}
		}
		public string Description
		{
			get => _description;
			set
			{
				if (_description != value)
				{
					_description = value;
					OnPropertyChanged(nameof(Description));
				}
			}
		}
		public double NetCost
		{
			get => _netCost;
			set
			{
				if (_netCost != value)
				{
					_netCost = value;
					OnPropertyChanged(nameof(NetCost));
				}
			}
		}
		public double OutVal
		{
			get => _outVal;
			set
			{
				if (_outVal != value)
				{
					_outVal = value;
					OnPropertyChanged(nameof(OutVal));
				}
			}
		}
		public double OverheadValue
		{
			get => _overheadValue;
			set
			{
				if (_overheadValue != value)
				{
					_overheadValue = value;
					OnPropertyChanged(nameof(OverheadValue));
				}
			}
		}
		public double ProfitValue
		{
			get => _profitValue;
			set
			{
				if (_profitValue != value)
				{
					_profitValue = value;
					OnPropertyChanged(nameof(ProfitValue));
				}
			}
		}
		public double TotalCost
		{
			get => _totalCost;
			set
			{
				if (_totalCost != value)
				{
					_totalCost = value;
					OnPropertyChanged(nameof(TotalCost));
				}
			}
		}
		public string Unit
		{
			get => _unit;
			set
			{
				if (_unit != value)
				{
					_unit = value;
					OnPropertyChanged(nameof(Unit));
				}
			}
		}

		private ObservableCollection<SteelWorkBreakdownLine> _steelWorkBreakdownLines =
			new ObservableCollection<SteelWorkBreakdownLine>();
		public ObservableCollection<SteelWorkBreakdownLine> SteelWorkBreakdownLines
		{
			get => _steelWorkBreakdownLines;
			set
			{
				_steelWorkBreakdownLines = value;
				OnPropertyChanged(nameof(SteelWorkBreakdownLines));
			}
		}


		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged(string propertyName) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	}
}
