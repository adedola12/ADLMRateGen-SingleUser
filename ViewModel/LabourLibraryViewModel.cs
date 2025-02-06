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
                }
            }
        }

        public ICommand SearchLabourCommand { get;}
        public ICommand ClearDatabaseCommand { get;}
        public ICommand DeleteLabourCommand { get;}
        public ICommand EditLabourCommand { get;}
        public event Action<LabourModel> EditLabourRequested;

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
            throw new NotImplementedException();
        }

        private void ClearDatabase()
        {
            throw new NotImplementedException();
        }

        private void DeleteLabour(object o)
        {
            throw new NotImplementedException();
        }

        private void EditLabour(object o)
        {
            throw new NotImplementedException();
        }
    }
}
