using System.ComponentModel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ADLMRateGen.ViewModel.Model
{
    [BsonIgnoreExtraElements]
	public class MaterialModel: INotifyPropertyChanged
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

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
