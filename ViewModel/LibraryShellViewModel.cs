using ADLMRateGen.Command;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.Model;
using ADLMRateGen.ViewModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
	public sealed class LibraryShellViewModel : ViewModelBase
	{
		// rename these so ShowMaterialPopup can find them:
		public MaterialLibraryViewModel MaterialLibraryViewModel { get; }
		public LabourLibraryViewModel LabourLibraryViewModel { get; }

		

		// events bubble up to MainWindow so it can show the popup
		public event Action RequestAddMaterial;
		public event Action RequestAddLabour;
		public event Action<MaterialModel> RequestEditMaterial;
		public event Action<LabourModel> RequestEditLabour;
		public ICommand AddCommand { get; }
	

		private readonly UserControl _materialView = new MaterialLibraryView();
		private readonly UserControl _labourView = new LabourLibraryView();

		private bool _isMaterialTab = true;
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

		private UserControl _currentContent;
		public UserControl CurrentContent
		{
			get => _currentContent;
			private set { _currentContent = value; RaisePropertyChanged(); }
		}

		public string AddButtonText => IsMaterialTab ? "Add Material  +" : "Add Labour  +";

		public LibraryShellViewModel(
			MaterialLibraryViewModel materialLibraryVm,
			LabourLibraryViewModel labourLibraryVm)
		{
			MaterialLibraryViewModel = materialLibraryVm;
			LabourLibraryViewModel = labourLibraryVm;

			// set the views’ data contexts
			_materialView.DataContext = MaterialLibraryViewModel;
			_labourView.DataContext = LabourLibraryViewModel;
			_currentContent = _materialView;

			AddCommand = new RelayCommand(_ =>
			{
				if (IsMaterialTab) RequestAddMaterial?.Invoke();
				else RequestAddLabour?.Invoke();     // ← **fixed**
			});

			MaterialLibraryViewModel.EditMaterialRequested += m => RequestEditMaterial?.Invoke(m);
			LabourLibraryViewModel.EditLabourRequested += l => RequestEditLabour?.Invoke(l);   // ← added

		}
	}
}
