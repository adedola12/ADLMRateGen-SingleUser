using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ADLMRateGen.Command;

namespace ADLMRateGen.ViewModel
{
	public sealed class LibraryShellViewModel : ViewModelBase
	{
		public MaterialLibraryViewModel MaterialVM { get; }
		public LabourLibraryViewModel LabourVM { get; }

		private readonly UserControl _materialView = new View.MaterialLibraryView();
		private readonly UserControl _labourView = new View.LabourLibraryView();

		public LibraryShellViewModel()
			: this(new MaterialLibraryViewModel(),
				   new LabourLibraryViewModel())
		{ }

		public LibraryShellViewModel(MaterialLibraryViewModel mvm,
									 LabourLibraryViewModel lvm)
		{
			MaterialVM = mvm;
			LabourVM = lvm;

			_materialView.DataContext = MaterialVM;
			_labourView.DataContext = LabourVM;

			_currentContent = _materialView;
			_isMaterialTab = true;

			AddCommand = new RelayCommand(_ =>
			{
				// find our PopupHost in MainWindow and show the correct form
				var wnd = Application.Current.MainWindow as MainWindow;
				if (IsMaterialTab)
					wnd!.ShowPopup(new View.MaterialPriceView { DataContext = MaterialVM });
				else
					wnd!.ShowPopup(new View.LabourPriceView { DataContext = LabourVM });
			});
		}

		/*── TAB STATE ──*/
		private bool _isMaterialTab;
		public bool IsMaterialTab
		{
			get => _isMaterialTab;
			set
			{
				if (_isMaterialTab == value) return;
				_isMaterialTab = value;
				RaisePropertyChanged();
				if (value)
				{
					IsLabourTab = false;
					CurrentContent = _materialView;
					RaisePropertyChanged(nameof(AddButtonText));
				}
			}
		}

		private bool _isLabourTab;
		public bool IsLabourTab
		{
			get => _isLabourTab;
			set
			{
				if (_isLabourTab == value) return;
				_isLabourTab = value;
				RaisePropertyChanged();
				if (value)
				{
					IsMaterialTab = false;
					CurrentContent = _labourView;
					RaisePropertyChanged(nameof(AddButtonText));
				}
			}
		}

		/*── HOSTED CONTENT ──*/
		private UserControl _currentContent;
		public UserControl CurrentContent
		{
			get => _currentContent;
			private set { _currentContent = value; RaisePropertyChanged(); }
		}

		/*── ADD BUTTON ──*/
		public string AddButtonText => IsMaterialTab ? "Add Material  +" : "Add Labour  +";
		public ICommand AddCommand { get; }
	}
}
