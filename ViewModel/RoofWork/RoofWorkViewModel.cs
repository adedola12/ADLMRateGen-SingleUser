using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;

namespace ADLMRateGen.ViewModel.RoofWork
{
    public class RoofWorkViewModel: ViewModelBase
    {
        private readonly GetItemsFromDB _helper;

        private double _overheadPercent = 10.0;
        private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;

        public double OverheadPercent
        {
            get => _overheadPercent;
            set
            {
                if (_overheadPercent != value)
                {
                    _overheadPercent = value;
                    RaisePropertyChanged();
                }
            }
        }
        public double ProfitPercent
        {
            get => _profitPercent;
            set
            {
                if (_profitPercent != value)
                {
                    _profitPercent = value;
                    RaisePropertyChanged();
                }
            }
        }
        public ObservableCollection<RoofWorkItem> RoofWorkItems { get; set; } =
            new ObservableCollection<RoofWorkItem>();
        public ICollectionView RoofworkCollectionView { get; private set; }
		public string SearchTerm
		{
			get => _searchTerm;
			set
			{
				if (_searchTerm != value)
				{
					_searchTerm = value;
					RaisePropertyChanged();
					RoofworkCollectionView.Refresh();
				}
			}
		}
		public object SelectedDetail
		{
			get => _selectedDetail;
			set
			{
				if (_selectedDetail != value)
				{
					_selectedDetail = value;
					RaisePropertyChanged();
				}
			}
		}
		public ICommand RecomputeCommand { get; }
		public ICommand ShowDetailsCommand { get; }
        public RoofWorkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
        {
            _helper = new GetItemsFromDB(matLib, labourLib);
            matLib.LibraryChanged += OnLibraryChange;
            labourLib.LibraryChanged += OnLibraryChange;

            BuildRoofworkItem();

            RoofworkCollectionView = CollectionViewSource.GetDefaultView(RoofWorkItems);
            RoofworkCollectionView.Filter = FilterRoofItem;

			RecomputeCommand = new DelegateCommand(o => RecomputeAll());
            ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
        }

		#region Function Method
		private void ShowDetails(object o)
		{
			throw new NotImplementedException();
		}

		private void RecomputeAll()
		{
			throw new NotImplementedException();
		}

		private bool FilterRoofItem(object obj)
		{
			throw new NotImplementedException();
		}

		private void BuildRoofworkItem()
		{
			throw new NotImplementedException();
		}

		private void OnLibraryChange()
		{
			throw new NotImplementedException();
		}
		#endregion
	}
}
