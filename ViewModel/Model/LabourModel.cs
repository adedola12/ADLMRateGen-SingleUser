using System.ComponentModel;

namespace ADLMRateGen.ViewModel.Model
{
    public class LabourModel: INotifyPropertyChanged
    {
        private int _serialNumber;
        private string? _labourName;
        private string? _labourUnit;
        private decimal _labourPrice;
        private string? _labourcategory;

        public int SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (_serialNumber != value)
                {
                    _serialNumber = value;
                    OnPropertyChanged(nameof(SerialNumber));
                }
            }
        }
        public string LabourName
        {
            get => _labourName;
            set
            {
                if (_labourName != value)
                {
                    _labourName = value;
                    OnPropertyChanged(nameof(LabourName));
                }
            }
        }
        public string LabourUnit
        {
            get => _labourUnit;
            set
            {
                if (_labourUnit != value)
                {
                    _labourUnit = value;
                    OnPropertyChanged(nameof(LabourUnit));
                }
            }
        }
        public decimal LabourPrice
        {
            get => _labourPrice;
            set
            {
                if (_labourPrice != value)
                {
                    _labourPrice = value;
                    OnPropertyChanged(nameof(LabourPrice));
                }
            }
        }
        public string LabourCategory
        {
            get => _labourcategory;
            set
            {
                if(_labourcategory != value)
                {
                    _labourcategory = value;
                    OnPropertyChanged(nameof(LabourCategory));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
