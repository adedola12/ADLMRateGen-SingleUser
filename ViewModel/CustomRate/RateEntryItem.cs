using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using ADLMRateGen.Services;

namespace ADLMRateGen.ViewModel.CustomRate
{
	public class RateEntryItem : INotifyPropertyChanged
	{
		private int _itemNo;
		private string? _description;
		private decimal _quantity;
		private string? _unit;
		private decimal _unitPrice;

		public int ItemNo
		{
			get => _itemNo;
			set
			{
				if (_itemNo != value)
				{
					_itemNo = value;
					OnPropertyChanged();
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
					OnPropertyChanged();
					if (!string.IsNullOrWhiteSpace(_description))
					{
						UnitPrice = MaterialLibraryService.GetPrice(_description);
					}
				}
			}
		}
		public decimal Quantity
		{
			get => _quantity;
			set
			{
				if (_quantity != value)
				{
					_quantity = value;
					OnPropertyChanged();
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
					OnPropertyChanged();
				}
			}
		}
		public decimal UnitPrice
		{
			get => _unitPrice;
			set
			{
				if (_unitPrice != value)
				{
					_unitPrice = value;
					OnPropertyChanged();
				}
			}
		}

		[JsonIgnore]
		public decimal TotalCost => Quantity * UnitPrice;

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	}

	
}
