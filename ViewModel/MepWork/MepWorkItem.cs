using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ADLMRateGen.ViewModel.MepWork
{
	public class MepWorkItem : INotifyPropertyChanged
	{
		private int _itemNo;
		private string? _description;
		private string? _unit;
		private string? _section;
		private double _netCost;
		private double _overheadValue;
		private double _profitValue;
		private double _totalCost;

		public int ItemNo
		{
			get => _itemNo;
			set { if (_itemNo != value) { _itemNo = value; OnPropertyChanged(nameof(ItemNo)); } }
		}
		public string Description
		{
			get => _description;
			set { if (_description != value) { _description = value; OnPropertyChanged(nameof(Description)); } }
		}
		public string Unit
		{
			get => _unit;
			set { if (_unit != value) { _unit = value; OnPropertyChanged(nameof(Unit)); } }
		}

		/// <summary>Trade grouping, e.g. "Lighting", "Sanitary". Used by the filter.</summary>
		public string Section
		{
			get => _section;
			set { if (_section != value) { _section = value; OnPropertyChanged(nameof(Section)); } }
		}
		public double NetCost
		{
			get => _netCost;
			set { if (_netCost != value) { _netCost = value; OnPropertyChanged(nameof(NetCost)); } }
		}
		public double OverheadValue
		{
			get => _overheadValue;
			set { if (_overheadValue != value) { _overheadValue = value; OnPropertyChanged(nameof(OverheadValue)); } }
		}
		public double ProfitValue
		{
			get => _profitValue;
			set { if (_profitValue != value) { _profitValue = value; OnPropertyChanged(nameof(ProfitValue)); } }
		}
		public double TotalCost
		{
			get => _totalCost;
			set { if (_totalCost != value) { _totalCost = value; OnPropertyChanged(nameof(TotalCost)); } }
		}

		private ObservableCollection<MepWorkBreakdownLine> _lines = new();
		public ObservableCollection<MepWorkBreakdownLine> MepBreakdownLine
		{
			get => _lines;
			set { if (_lines != value) { _lines = value; OnPropertyChanged(nameof(MepBreakdownLine)); } }
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged(string propertyName) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
