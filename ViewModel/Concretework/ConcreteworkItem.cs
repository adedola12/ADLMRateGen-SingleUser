using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ADLMRateGen.ViewModel.ConcreteWork
{
	public class ConcreteworkItem : INotifyPropertyChanged
	{
		private int _itemNo;
		private string? _description;
		private double _netCost;
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

		private ObservableCollection<ConcreteworkBreakdownLine> _concreteBreakdownLines =
			new ObservableCollection<ConcreteworkBreakdownLine>();

		public ObservableCollection<ConcreteworkBreakdownLine> ConcreteBreakdownLine
		{
			get => _concreteBreakdownLines;
			set
			{
				if (_concreteBreakdownLines != value)
				{
					_concreteBreakdownLines = value;
					OnPropertyChanged(nameof(ConcreteBreakdownLine));
				}
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged(string propertyName) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}



