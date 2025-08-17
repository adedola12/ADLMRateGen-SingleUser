using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ADLMRateGen.Services;

namespace ADLMRateGen.ViewModel.CustomRate
{
	public enum RateItemType { Material, Labour }

	/// <summary>
	/// One line inside a Custom‑Rate: keeps prices in NGN internally and
	/// exposes converted “Display” properties that track
	/// <see cref="CurrencyService.Rate"/> automatically.
	/// </summary>
	public class RateEntryItem : INotifyPropertyChanged
	{
		/* ────────── backing fields ────────── */
		private RateItemType _rateType;
		private string? _description;
		private decimal _quantity;
		private string? _unit;
		private decimal _unitPriceNgn;      // always stored in ₦

		public RateEntryItem()
		{
			// when the global rate changes -> refresh the derived props
			CurrencyService.Instance.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(CurrencyService.Rate) or nameof(CurrencyService.Code))
				{
					OnPropertyChanged(nameof(DisplayUnitPrice));
					OnPropertyChanged(nameof(TotalCostDisplay));
				}
			};
		}

		/* ────────── editable fields ────────── */

		public RateItemType RateType
		{
			get => _rateType;
			set
			{
				if (_rateType != value)
				{
					_rateType = value;
					OnPropertyChanged();
					ResolveUnitPrice();          // re‑query the right library
				}
			}
		}

		public string? Description
		{
			get => _description;
			set
			{
				if (_description != value)
				{
					_description = value;
					OnPropertyChanged();
					ResolveUnitPrice();
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
					OnPropertyChanged(nameof(TotalCost));
					OnPropertyChanged(nameof(TotalCostDisplay));
				}
			}
		}

		public string? Unit
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

		/// <summary>Unit price **stored in NGN**.</summary>
		public decimal UnitPrice
		{
			get => _unitPriceNgn;
			set
			{
				if (_unitPriceNgn != value)
				{
					_unitPriceNgn = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(DisplayUnitPrice));
					OnPropertyChanged(nameof(TotalCost));
					OnPropertyChanged(nameof(TotalCostDisplay));
				}
			}
		}

		/* ────────── derived, live‑converted props ────────── */

		public decimal DisplayUnitPrice => UnitPrice * CurrentRate;
		public decimal TotalCost => Quantity * UnitPrice;          // ₦
		public decimal TotalCostDisplay => TotalCost * CurrentRate;        // chosen currency

		private static decimal CurrentRate => (decimal)CurrencyService.Instance.Rate;

        /* ────────── helpers ────────── */

        private void ResolveUnitPrice()
        {
            if (string.IsNullOrWhiteSpace(_description)) return;

            switch (RateType)
            {
                case RateItemType.Material:
                    var mat = MaterialLibraryService.FindByName(_description);
                    if (mat != null) { UnitPrice = mat.MaterialPrice; Unit = mat.MaterialUnit; }
                    else UnitPrice = 0m;
                    break;

                case RateItemType.Labour:
                    var lab = LabourLibraryService.FindByName(_description);
                    if (lab != null) { UnitPrice = lab.LabourPrice; Unit = lab.LabourUnit; }
                    else UnitPrice = 0m;
                    break;
            }
        }



        /* ────────── INotifyPropertyChanged boilerplate ────────── */

        public event PropertyChangedEventHandler? PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string? name = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
