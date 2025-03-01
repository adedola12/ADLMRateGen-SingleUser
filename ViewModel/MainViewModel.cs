using ADLMRateGen.Command;
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.ConcreteWork;
using ADLMRateGen.ViewModel.Model;
using ADLMRateGen.ViewModel.BlockWork;

namespace ADLMRateGen.ViewModel
{
	public class MainViewModel : ViewModelBase
	{
		// Commands for switching between different sections of the application.
		public DelegateCommand SelectedMaterialInputViewCommand { get; }
		public DelegateCommand SelectedMaterialLibraryViewCommand { get; }
		public DelegateCommand SelectedLabourInputViewCommand { get; }
		public DelegateCommand SelectedLabourLibraryViewCommand { get; }
		public DelegateCommand SelectedGroundworkViewCommand { get; }
		public DelegateCommand SelectedConcreteWorkViewCommand { get; }
		public DelegateCommand SelectedBlockworkViewCommand { get; }

		// ViewModels for the different sections of the application.
		public MaterialPriceViewModel MaterialPriceViewModel { get; }
		public MaterialLibraryViewModel MaterialLibraryViewModel { get; }
		public LabourPriceViewModel LabourPriceViewModel { get; }
		public LabourLibraryViewModel LabourLibraryViewModel { get; }
		public GroundWorkViewModel GroundWorkViewModel { get; }
		public ConcreteViewModel ConcreteViewModel { get; }
		public BlockworkViewModel BlockworkViewModel { get; }
		private ViewModelBase _selectedViewModel;

		public ViewModelBase SelectedViewModel
		{
			get => _selectedViewModel;
			set
			{
				_selectedViewModel = value;
				RaisePropertyChanged();
			}
		}

		public MainViewModel(MaterialPriceViewModel priceVM, MaterialLibraryViewModel libraryVM, LabourPriceViewModel labourVM,
			LabourLibraryViewModel labourLibraryVM, GroundWorkViewModel groundworkVM, ConcreteViewModel concreteWorkViewModel,
			BlockworkViewModel blockworkViewModel)
		{
			MaterialPriceViewModel = priceVM;
			MaterialLibraryViewModel = libraryVM;
			LabourPriceViewModel = labourVM;
			LabourLibraryViewModel = labourLibraryVM;
			GroundWorkViewModel = groundworkVM;
			ConcreteViewModel = concreteWorkViewModel;
			BlockworkViewModel = blockworkViewModel;

			// Wire events.
			MaterialPriceViewModel.MaterialSaved += OnMaterialSaved;
			MaterialLibraryViewModel.EditMaterialRequested += OnEditMaterialRequested;

			LabourPriceViewModel.LabourSaved += OnLabourSaved;
			LabourLibraryViewModel.EditLabourRequested += OnEditLabourRequested;

			// Set default view.
			SelectedViewModel = MaterialPriceViewModel;
			SelectedMaterialInputViewCommand = new DelegateCommand(param => SelectViewModel(MaterialPriceViewModel));
			SelectedMaterialLibraryViewCommand = new DelegateCommand(param => SelectViewModel(MaterialLibraryViewModel));
			SelectedLabourInputViewCommand = new DelegateCommand(param => SelectViewModel(LabourPriceViewModel));
			SelectedLabourLibraryViewCommand = new DelegateCommand(param => SelectViewModel(LabourLibraryViewModel));
			SelectedGroundworkViewCommand = new DelegateCommand(param => SelectViewModel(GroundWorkViewModel));
			SelectedConcreteWorkViewCommand = new DelegateCommand(param => SelectViewModel(ConcreteViewModel));
			SelectedBlockworkViewCommand = new DelegateCommand(param => SelectViewModel(BlockworkViewModel));
		}

		private void OnMaterialSaved(MaterialModel material)
		{
			MaterialLibraryViewModel.AddOrUpdateMaterial(material);
		}

		private void OnLabourSaved(LabourModel labour)
		{
			LabourLibraryViewModel.AddOrUpdateLabour(labour);
		}

		private void OnEditMaterialRequested(MaterialModel material)
		{
			// Load the material into the price view for editing.
			MaterialPriceViewModel.EditingMaterial = material;
			MaterialPriceViewModel.MaterialName = material.MaterialName;
			MaterialPriceViewModel.MaterialUnit = material.MaterialUnit;
			MaterialPriceViewModel.MaterialPrice = material.MaterialPrice;
			MaterialPriceViewModel.NewMaterialCategory = material.MaterialCategory;
			// Switch to the input view.
			SelectedViewModel = MaterialPriceViewModel;
		}

		private void OnEditLabourRequested(LabourModel labour)
		{
			// Load the labour into the price view for editing.
			LabourPriceViewModel.EditingLabour = labour;
			LabourPriceViewModel.LabourName = labour.LabourName;
			LabourPriceViewModel.LabourUnit = labour.LabourUnit;
			LabourPriceViewModel.LabourPrice = labour.LabourPrice;
			LabourPriceViewModel.NewLabourCategory = labour.LabourCategory;
			// Switch to the input view.
			SelectedViewModel = LabourPriceViewModel;
		}

		private void SelectViewModel(object parameter)
		{
			SelectedViewModel = parameter as ViewModelBase;
		}
	}

}
