namespace ADLMRateGen.ViewModel.Groundwork
{
    public class GroundworkBreakdownLine: ViewModelBase
    {
        private string _componentName;
        private double _quantity;
        private string _unit;
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

    }
}
