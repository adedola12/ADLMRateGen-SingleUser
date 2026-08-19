using System.ComponentModel;
using System.Runtime.CompilerServices;
using ADLMRateGen.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ADLMRateGen.ViewModel.Model
{
    [BsonIgnoreExtraElements]
	public class LabourModel: INotifyPropertyChanged
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }


		private int _serialNumber;
        private string? _labourName;
        private string? _labourUnit;
        private decimal _labourPrice;
        private string? _labourcategory;
        private string? _labourNote;

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

        /// <summary>
        /// The estimator's own specification for this rate: what it is and what it
        /// produces. Only rates a user adds carry one — the shipped catalogue has
        /// its reference text in Data/labourSpecs.json instead, which is why the
        /// popup shows whichever of the two exists.
        /// </summary>
        public string LabourNote
        {
            get => _labourNote;
            set
            {
                if (_labourNote != value)
                {
                    _labourNote = value;
                    OnPropertyChanged(nameof(LabourNote));
                }
            }
        }

		public decimal DisplayPrice =>
		decimal.Round(LabourPrice * (decimal)CurrencyService.Instance.Rate, 2);

		public event PropertyChangedEventHandler? PropertyChanged;
		//protected void OnPropertyChanged(string propName) =>
		//    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

		private void OnPropertyChanged([CallerMemberName] string p = null) =>
	   PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
	}
}
