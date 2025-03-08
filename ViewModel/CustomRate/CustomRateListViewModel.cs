using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using ADLMRateGen.Command;
using System.Windows.Input;
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

		private CustomRate _selectedRate;
		public CustomRate SelectedRate
		{
			get => _selectedRate;
			set
			{
				_selectedRate = value;
				RaisePropertyChanged(nameof(SelectedRate));
				(DeleteRateCommand as RelayCommand)?.RaiseCanExecuteChanged();
			}
		}

		public ICollectionView RatesView { get; }

		public ICommand DeleteRateCommand { get; }

		public CustomRateListViewModel()
		{
			// Load custom rates
			var rates = CustomRateServices.LoadCustomRates();
			foreach (var rate in rates)
				CustomRates.Add(rate);

			RatesView = CollectionViewSource.GetDefaultView(CustomRates);
			RatesView.Filter = RateFilter;

			// Command
			DeleteRateCommand = new RelayCommand(DeleteRate, CanDeleteRate);

			CustomRateServices.OnCustomRateSaved += CustomRateServices_OnCustomRateSaved;
		}

		private void CustomRateServices_OnCustomRateSaved(CustomRate newRate)
		{
			// Add to local collection so user sees it
			App.Current.Dispatcher.Invoke(() =>
			{
				CustomRates.Add(newRate);
				RatesView.Refresh();
			});
		}

		private bool RateFilter(object item)
		{
			if (item is CustomRate rate)
			{
				// Safely coalesce Title and Description
				var desc = rate.Description ?? string.Empty;
				var title = rate.Title ?? string.Empty;

				return string.IsNullOrEmpty(SearchTerm)
					|| desc.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0
					|| title.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			return false;
		}


		private void FilterRates()
		{
			RatesView.Refresh();
		}

		private bool CanDeleteRate() => SelectedRate != null;

		private void DeleteRate()
		{
			if (SelectedRate == null) return;

			// Remove from collection
			CustomRates.Remove(SelectedRate);

			// Re-save to file
			CustomRateServices.SaveRates(CustomRates); 

			// Refresh the view
			RatesView.Refresh();
		}
	}
}
