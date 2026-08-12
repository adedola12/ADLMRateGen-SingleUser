namespace ADLMRateGen.ViewModel.Painting
{
    public class PaintingBreakdownLine: ViewModelBase
    {
		private string? _componentName;
		private double _quantity;
		private string? _unit;
		private double _unitPrice;
		private double _totalPrice;
		public string ComponentName
		{
			get => _componentName;
			set
			{
				_componentName = value;
				RaisePropertyChanged();
			}
		}
		public double Quantity
		{
			get => _quantity;
			set
			{
				_quantity = value;
				RaisePropertyChanged();
			}
		}
		public string Unit
		{
			get => _unit;
			set
			{
				_unit = value;
				RaisePropertyChanged();
			}
		}
		public double UnitPrice
		{
			get => _unitPrice;
			set
			{
				_unitPrice = value;
				RaisePropertyChanged();
			}
		}
		public double TotalPrice
		{
			get => _totalPrice;
			set
			{
				_totalPrice = value;
				RaisePropertyChanged();
			}
		}
		public bool IsTotalLine
		{
			get
			{
				return !string.IsNullOrEmpty(ComponentName)
					&& ComponentName.IndexOf("total", StringComparison.OrdinalIgnoreCase) >= 0;
			}
		}

		/// <summary>True when the Quantity cell should accept user edits in the breakdown popup.
		/// Sub-total / total rows are read-only (they are computed sums).</summary>
		public bool IsEditableQuantity => !IsTotalLine;
	
        /// <summary>
        /// Opens the library at the row this line is priced from. Shared across
        /// every section so there is one implementation rather than ten.
        /// </summary>
        public System.Windows.Input.ICommand OpenInLibraryCommand
            => ADLMRateGen.Helpers.LibraryLink.OpenCommand;

        /// <summary>False for totals, warnings and percentage rows, which are not
        /// library items and must not render as links.</summary>
        public bool CanOpenInLibrary
            => ADLMRateGen.Helpers.LibraryLink.CanOpen(ComponentName);
}
}
