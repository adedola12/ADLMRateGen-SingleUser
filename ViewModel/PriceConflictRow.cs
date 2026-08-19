using System;
using ADLMRateGen.Services;

namespace ADLMRateGen.ViewModel
{
    /// <summary>
    /// One row in the "your price vs the newly published one" review.
    ///
    /// Wraps <see cref="SyncBaseline.EditedRow"/> rather than extending it: the
    /// service DTO is written to disk and compared by value, and a tick box is a
    /// screen concern that has no business travelling with it.
    /// </summary>
    public sealed class PriceConflictRow : ViewModelBase
    {
        public PriceConflictRow(SyncBaseline.EditedRow source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public SyncBaseline.EditedRow Source { get; }

        public string Name => Source.Name;
        public string Unit => Source.Unit;
        public bool IsLabour => Source.IsLabour;

        /// <summary>What the user typed, and what the app is using right now.</summary>
        public decimal YourPrice => Source.YourPrice;

        /// <summary>What the library has since published.</summary>
        public decimal PublishedPrice => Source.ServerPrice;

        public decimal Difference => Source.ServerPrice - Source.YourPrice;
        public bool IsIncrease => Difference > 0m;

        /// <summary>Signed percentage, or a plain arrow when the old price was zero
        /// and a percentage would be meaningless.</summary>
        public string ChangeText
        {
            get
            {
                if (Difference == 0m) return "no change";
                if (YourPrice == 0m) return IsIncrease ? "increase" : "decrease";

                var pct = Difference / YourPrice * 100m;
                return (pct > 0 ? "+" : "") + pct.ToString("0.#") + "%";
            }
        }

        /// <summary>Identifies the row in the review, since a material and a labour
        /// line may legitimately share a name.</summary>
        public string Kind => IsLabour ? "Labour" : "Material";

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                RaisePropertyChanged();
                SelectionChanged?.Invoke();
            }
        }

        /// <summary>Raised so the panel can retally its "n of m selected" footer.</summary>
        public Action? SelectionChanged { get; set; }
    }
}
