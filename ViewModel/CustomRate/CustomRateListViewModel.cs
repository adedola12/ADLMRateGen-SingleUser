using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel.CustomRate
{
    public class CustomRateListViewModel: ViewModelBase
    {
		// CustomRateListViewModel.cs  ❯  add at the top of the class
		public event Action LibraryChanged;

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
		public ICommand ViewRateCommand { get; }
		public event Action<CustomRate> OnViewRequested;


		public CustomRateListViewModel()
		{
			// Load custom rates
			var rates = CustomRateServices.LoadCustomRates();
			foreach (var rate in rates)
				CustomRates.Add(rate);

			RatesView = CollectionViewSource.GetDefaultView(CustomRates);
			RatesView.Filter = RateFilter;

			CustomRates.CollectionChanged += (_, __) => LibraryChanged?.Invoke();

			/* ----------  fire ONCE for the initial load  ---------- */       // ② NEW
			LibraryChanged?.Invoke();

			// Command
			ViewRateCommand = new RelayCommand(
				param => ViewRateExecute(param),        // We'll pass the row item
				param => param is CustomRate            // Only enabled if param is a CustomRate
			);
			DeleteRateCommand = new RelayCommand(
				_ => DeleteRate(),
				_ => CanDeleteRate()
			);


			CustomRateServices.OnCustomRateSaved += CustomRateServices_OnCustomRateSaved;

			CustomRateServices.OnCustomRateUpdated += CustomRateServices_OnCustomRateUpdated;

            // NEW: auto-recalc when libraries change
            MaterialLibraryService.LibraryChanged += OnAnyLibraryChanged;
            LabourLibraryService.LibraryChanged   += OnAnyLibraryChanged;

            Debug.WriteLine($"[PATH] Material file used by service: {AppPaths.MaterialLibraryFile}");

        }

        private void ViewRateExecute(object parameter)
		{
			var rate = parameter as CustomRate;
			if (rate != null)
			{
				OnViewRequested?.Invoke(rate);
			}
		}



		private void CustomRateServices_OnCustomRateSaved(CustomRate newRate)
		{
			// Add to local collection so user sees it
			App.Current.Dispatcher.Invoke(() =>
			{
				CustomRates.Add(newRate);
				RatesView.Refresh();
				LibraryChanged?.Invoke();
			});
		}

		private void CustomRateServices_OnCustomRateUpdated(CustomRate updatedRate)
		{
			App.Current.Dispatcher.Invoke(() =>
			{
				var existing = CustomRates.FirstOrDefault(r => r.Id == updatedRate.Id);
				if (existing != null)
				{
					var index = CustomRates.IndexOf(existing);
					CustomRates[index] = updatedRate;
				}
				else
				{
					// If not found, you can add or ignore
					CustomRates.Add(updatedRate);
				}
				RatesView.Refresh();
				LibraryChanged?.Invoke();
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
			LibraryChanged?.Invoke();
		}

        private void OnAnyLibraryChanged()
        {
            // RateEntryItem instances inside CustomRates update themselves (step 2).
            // We just refresh the grid and persist the new numbers.
            App.Current.Dispatcher.Invoke(() =>
            {
                RatesView.Refresh();
                CustomRateServices.SaveRates(CustomRates);   // keep file in sync with library prices
                LibraryChanged?.Invoke();
            });
        }

    }
}
