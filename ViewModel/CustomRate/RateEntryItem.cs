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

            // NEW: library price changes
            MaterialLibraryService.LibraryChanged += OnAnyLibraryChanged;
            LabourLibraryService.LibraryChanged   += OnAnyLibraryChanged;
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
					ResolveUnitPrice(clearWhenUnknown: true);   // re‑query the right library
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
					ResolveUnitPrice(clearWhenUnknown: true);
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

        private void OnAnyLibraryChanged()
        {
            // clearWhenUnknown: false — a library refresh must never wipe a price
            // the user or the AI put there. Saving a rate harvests new entries and
            // reloads the libraries, which fires this for every line; with the old
            // unconditional clear that turned every unmatched line into 0.00.
            if (!string.IsNullOrWhiteSpace(_description))
                ResolveUnitPrice(clearWhenUnknown: false);
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

        /// <summary>
        /// Re-prices this line from the library.
        /// </summary>
        /// <param name="clearWhenUnknown">
        /// When the library has no entry for this description: true zeroes the
        /// price (the user just picked a different item, so the old price is
        /// meaningless), false leaves it alone (a background library reload must
        /// not destroy an AI or hand-entered price).
        /// </param>
        private void ResolveUnitPrice(bool clearWhenUnknown)
        {
            if (string.IsNullOrWhiteSpace(_description)) return;

            // Match on the library-facing name so provenance tags don't block the
            // lookup: an AI line reads "Cement (Portland 42.5R) [AI]" but the
            // library entry — including the one Harvest creates on save — is
            // stored under the clean name.
            var name = RateLineLibrary.CleanName(_description);
            if (name.Length == 0) return;

            switch (RateType)
            {
                case RateItemType.Material:
                    var mat = MaterialLibraryService.FindByName(name);
                    if (mat != null) { UnitPrice = mat.MaterialPrice; Unit = mat.MaterialUnit; }
                    else if (clearWhenUnknown) UnitPrice = 0m;
                    break;

                case RateItemType.Labour:
                    var lab = LabourLibraryService.FindByName(name);
                    if (lab != null) { UnitPrice = lab.LabourPrice; Unit = lab.LabourUnit; }
                    else if (clearWhenUnknown) UnitPrice = 0m;
                    break;
            }
        }

        public void RefreshFromLibrary() => ResolveUnitPrice(clearWhenUnknown: false);


        /* ────────── INotifyPropertyChanged boilerplate ────────── */

        public event PropertyChangedEventHandler? PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string? name = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
