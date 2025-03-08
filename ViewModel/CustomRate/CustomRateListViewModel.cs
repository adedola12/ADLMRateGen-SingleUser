using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using ADLMRateGen.Services;

namespace ADLMRateGen.ViewModel.CustomRate
{
    public class CustomRateListViewModel: ViewModelBase
    {
		public ObservableCollection<CustomRate> CustomRates { get; set; } = new ObservableCollection<CustomRate>();

		private string _searchTerm;
		public string SearchTerm
		{
			get => _searchTerm;
			set
			{
				_searchTerm = value;
				RaisePropertyChanged();
				FilterRates();
			}
		}

		public ICollectionView RatesView { get; }

		public CustomRateListViewModel()
		{
			// Load custom rates from persistent storage (e.g., JSON file)
			var rates = CustomRateServices.LoadCustomRates();
			foreach (var rate in rates)
				CustomRates.Add(rate);

			RatesView = CollectionViewSource.GetDefaultView(CustomRates);
			RatesView.Filter = RateFilter;
		}

		private bool RateFilter(object item)
		{
			if (item is CustomRate rate)
			{
				return string.IsNullOrEmpty(SearchTerm) ||
					   rate.Description.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			return false;
		}

		private void FilterRates()
		{
			RatesView.Refresh();
		}
	}
}
