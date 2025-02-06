using System.ComponentModel;

namespace ADLMRateGen.ViewModel.Model
{
    public class MaterialModel: INotifyPropertyChanged
    {
        private int _serialNumber;
        private string _materialName;
        private string _materialUnit;
        private decimal _materialPrice;
        private string _materialCategory;

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

        public string MaterialName
        {
            get => _materialName;
            set
            {
                if (_materialName != value)
                {
                    _materialName = value;
                    OnPropertyChanged(nameof(MaterialName));
                }
            }
        }
        public string MaterialUnit
        {
            get => _materialUnit;
            set
            {
                if (_materialUnit != value)
                {
                    _materialUnit = value;
                    OnPropertyChanged(nameof(MaterialUnit));
                }
            }
        }
        public decimal MaterialPrice
        {
            get => _materialPrice;
            set
            {
                if(_materialPrice != value)
                {
                    _materialPrice = value;
                    OnPropertyChanged(nameof(MaterialPrice));
                }
            }
        }
        public string MaterialCategory
        {
            get => _materialCategory;
            set
            {
                if(_materialCategory != value)
                {
                    _materialCategory = value;
                    OnPropertyChanged(nameof(MaterialCategory));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
