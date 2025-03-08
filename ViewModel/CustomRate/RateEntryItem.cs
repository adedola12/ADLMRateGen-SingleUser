using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using ADLMRateGen.Services;

namespace ADLMRateGen.ViewModel.CustomRate
{
	public enum RateItemType
	{
		Material,Labour
	}
	public class RateEntryItem : INotifyPropertyChanged
	{
		private RateItemType _rateType;
		private string? _description;
		private decimal _quantity;
		private string? _unit;
		private decimal _unitPrice;

		public RateItemType RateType
		{
			get => _rateType;
			set
			{
				if (_rateType != value)
				{
					_rateType = value;
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
					OnPropertyChanged(nameof(Description));
					// Decide which library to use based on RateType:
					if (RateType == RateItemType.Material)
					{
						UnitPrice = MaterialLibraryService.GetPrice(_description);
					}
					else // RateType == RateItemType.Labour
					{
						UnitPrice = LabourLibraryService.GetPrice(_description);
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
					OnPropertyChanged(nameof(Quantity));
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
		public decimal UnitPrice
		{
			get => _unitPrice;
			set
			{
				if (_unitPrice != value)
				{
					_unitPrice = value;
					OnPropertyChanged(nameof(UnitPrice));
					OnPropertyChanged(nameof(TotalCost));
				}
			}
		}

		public decimal TotalCost => Quantity * UnitPrice;

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	}

	
}
