using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
    public class LabourLibraryViewModel: ViewModelBase
    {
        private readonly JsonDataServices _dataServices;
        private readonly string _filePath = "labour.json";
        private readonly string _defaultFilePath = "Data\\defaultLabours.json";

        public ObservableCollection<LabourModel> LabourLibrary { get; set; }
        public ICollectionView LabourCollectionView { get; set; }
        public ObservableCollection<string> LabourCategory {  get; set; }
        private string _selectedLabourcategory;
        public string SelecctedLabourCategory
        {
            get => _selectedLabourcategory;
            set
            {
                if (_selectedLabourcategory != value)
                {
                    _selectedLabourcategory = value;
                    RaisePropertyChanged();
					ApplyFilter();
				}
			}
        }

        private string _searchTerm = string.Empty;
		public string SearchTerm
		{
			get => _searchTerm;
			set
			{
				if (_searchTerm != value)
				{
					_searchTerm = value;
					RaisePropertyChanged();
					ApplyFilter();
				}
			}
		}

		public ICommand SearchLabourCommand { get;}
        public ICommand ClearDatabaseCommand { get;}
        public ICommand DeleteLabourCommand { get;}
        public ICommand EditLabourCommand { get;}
        public event Action<LabourModel> EditLabourRequested;
        public event Action LibraryChanged;

        public LabourLibraryViewModel()
        {
            _dataServices = new JsonDataServices(_filePath, _defaultFilePath);

            LabourLibrary = _dataServices.LoadData<ObservableCollection<LabourModel>>()
                ?? new ObservableCollection<LabourModel>();
            LabourCollectionView = CollectionViewSource.GetDefaultView(LabourLibrary);
            LabourCategory = new ObservableCollection<string> { "All", "Labour", "Plant", "Small Plant" };
            _selectedLabourcategory = "All";

            SearchLabourCommand = new DelegateCommand(o => ApplyFilter());
            ClearDatabaseCommand = new DelegateCommand(o => ClearDatabase());
            DeleteLabourCommand = new DelegateCommand(o => DeleteLabour(o));
            EditLabourCommand = new DelegateCommand(o => EditLabour(o));
        }

		private void ApplyFilter()
		{
			if (LabourCollectionView != null)
			{
				LabourCollectionView.Filter = o =>
				{
					if (o is LabourModel labour)
					{
						// Filter by category if not "All"
						bool matchesCategory = SelecctedLabourCategory == "All" ||
											   string.IsNullOrEmpty(SelecctedLabourCategory) ||
											   labour.LabourCategory == SelecctedLabourCategory;
						// Filter by text if search term is provided.
						// Assuming LabourModel has a LabourName property (or similar)
						bool matchesText = string.IsNullOrEmpty(SearchTerm) ||
										   (!string.IsNullOrEmpty(labour.LabourName) &&
										   labour.LabourName.IndexOf(SearchTerm, System.StringComparison.OrdinalIgnoreCase) >= 0);
						return matchesCategory && matchesText;
					}
					return false;
				};
				LabourCollectionView.Refresh();
			}
		}


		private void ClearDatabase()
        {
            LabourLibrary.Clear();
            _dataServices.SaveData(LabourLibrary);
            ApplyFilter();
        }

        private void DeleteLabour(object o)
        {
            if(o is LabourModel labour)
            {
                LabourLibrary.Remove(labour);
                ReassignSerialNumbers();
                _dataServices.SaveData(LabourLibrary);
                LibraryChanged?.Invoke();
                ApplyFilter();
            }
        }

        private void ReassignSerialNumbers()
        {
            int serial = 1;
            foreach(var labour in LabourLibrary)
            {
                labour.SerialNumber = serial++;
            }
        }

        private void EditLabour(object o)
        {
          if(o is LabourModel labour)
            {
                EditLabourRequested?.Invoke(labour);
            }
        }

        public void AddOrUpdateLabour(LabourModel labour)
        {
            if(labour.SerialNumber == 0)
            {
                int newSerial = LabourLibrary.Count > 0 ? LabourLibrary[^1].SerialNumber + 1 : 1;
                labour.SerialNumber = newSerial;
                LabourLibrary.Add(labour);
            }

            _dataServices.SaveData(LabourLibrary);
            LibraryChanged?.Invoke();
            ApplyFilter();
        }
    }
}
