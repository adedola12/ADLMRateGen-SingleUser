using System.ComponentModel;

namespace ADLMRateGen.ViewModel.Model
{
   public class RateItemModel: INotifyPropertyChanged
    {
        private string _description;
        private decimal _calculatedRate;

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }
        public decimal CalculatedRate
        {
            get => _calculatedRate;
            set { _calculatedRate = value; OnPropertyChanged(nameof(CalculatedRate)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
