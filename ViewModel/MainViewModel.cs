using ADLMRateGen.Command;
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.ConcreteWork;
using ADLMRateGen.ViewModel.Model;
using ADLMRateGen.ViewModel.BlockWork;
using ADLMRateGen.ViewModel.Finishes;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.RoofWork;
using ADLMRateGen.ViewModel.WindowAndDoor;
using ADLMRateGen.ViewModel.Painting;
using ADLMRateGen.ViewModel.SteelWork;
using ADLMRateGen.Services;
using System.Net;

namespace ADLMRateGen.ViewModel
{
	public class MainViewModel : ViewModelBase
	{
		private readonly MongoDbService _mongoDbService;
		public UserModel _currentUser;
		private bool _isLoggedIn;

		// Commands for switching between different sections of the application.
		public DelegateCommand SelectedMaterialInputViewCommand { get; }
		public DelegateCommand SelectedMaterialLibraryViewCommand { get; }
		public DelegateCommand SelectedLabourInputViewCommand { get; }
		public DelegateCommand SelectedLabourLibraryViewCommand { get; }
		public DelegateCommand SelectedGroundworkViewCommand { get; }
		public DelegateCommand SelectedConcreteWorkViewCommand { get; }
		public DelegateCommand SelectedBlockworkViewCommand { get; }
		public DelegateCommand SelectedFinishesViewCommand { get; }
		public DelegateCommand SelectedRoofworkViewCommand { get; }
		public DelegateCommand SelectedWindowAndDoorViewCommand { get; }
		public DelegateCommand SelectedPaintworkViewCommand { get; }
		public DelegateCommand SelectedSteelworkViewCommand { get; }
		public DelegateCommand SelectedCustomRateInputViewCommand { get; }
		public DelegateCommand SelectedCustomRateViewCommand { get; }


		// ViewModels for the different sections of the application.
		public MaterialPriceViewModel MaterialPriceViewModel { get; }
		public MaterialLibraryViewModel MaterialLibraryViewModel { get; }
		public LabourPriceViewModel LabourPriceViewModel { get; }
		public LabourLibraryViewModel LabourLibraryViewModel { get; }
		public GroundWorkViewModel GroundWorkViewModel { get; }
		public ConcreteViewModel ConcreteViewModel { get; }
		public BlockworkViewModel BlockworkViewModel { get; }
		public FinishesViewModel FinishesViewModel { get; }
		public RoofWorkViewModel RoofWorkViewModel { get; }
		public WindowAndDoorViewModel WindowAndDoorViewModel { get; }
		public PaintWorkViewModel PaintWorkViewModel { get; }
		public SteelWorkViewModel SteelWorkViewModel { get; }
		public CustomRateEntryViewModel CustomRateEntryViewModel { get; }
		public CustomRateListViewModel CustomRateListViewModel { get; }
		public SignInViewModel SignInViewModel { get; }

		public bool IsLoggedIn
		{
			get => _isLoggedIn;
			set
			{
				if (_isLoggedIn != value)
				{
					_isLoggedIn = value;
					RaisePropertyChanged();
				}
			}
		}

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
			BlockworkViewModel blockworkViewModel, FinishesViewModel finishesViewModel, RoofWorkViewModel roofworkVM, 
			WindowAndDoorViewModel windowAndDoorVM, PaintWorkViewModel paintWorkViewModel, SteelWorkViewModel steelWorkViewModel,
			CustomRateListViewModel customRateListViewModel, CustomRateEntryViewModel customRateEntryViewModel, SignInViewModel signinVM, MongoDbService mongoDbService)
		{
			//IsLoggedIn = false;

			MaterialPriceViewModel = priceVM;
			MaterialLibraryViewModel = libraryVM;
			LabourPriceViewModel = labourVM;
			LabourLibraryViewModel = labourLibraryVM;
			GroundWorkViewModel = groundworkVM;
			ConcreteViewModel = concreteWorkViewModel;
			BlockworkViewModel = blockworkViewModel;
			FinishesViewModel = finishesViewModel;
			RoofWorkViewModel = roofworkVM;
			WindowAndDoorViewModel = windowAndDoorVM;
			PaintWorkViewModel = paintWorkViewModel;
			SteelWorkViewModel = steelWorkViewModel;
			CustomRateListViewModel = customRateListViewModel;
			CustomRateEntryViewModel = customRateEntryViewModel;

			SignInViewModel = signinVM;
			_mongoDbService = mongoDbService;

			CustomRateListViewModel.OnViewRequested += (rate) =>
			{
				CustomRateEntryViewModel.LoadRate(rate);
				SelectedViewModel = CustomRateEntryViewModel;
			};

			// Wire events.
			MaterialPriceViewModel.MaterialSaved += OnMaterialSaved;
			MaterialLibraryViewModel.EditMaterialRequested += OnEditMaterialRequested;

			LabourPriceViewModel.LabourSaved += OnLabourSaved;
			LabourLibraryViewModel.EditLabourRequested += OnEditLabourRequested;

			// Set default view.
			SelectedViewModel = MaterialPriceViewModel;
			SignInViewModel.LoginSucceeded += OnLoginSucceeded;

			SelectedMaterialInputViewCommand = new DelegateCommand(param => SelectViewModel(MaterialPriceViewModel));
			SelectedMaterialLibraryViewCommand = new DelegateCommand(param => SelectViewModel(MaterialLibraryViewModel));
			SelectedLabourInputViewCommand = new DelegateCommand(param => SelectViewModel(LabourPriceViewModel));
			SelectedLabourLibraryViewCommand = new DelegateCommand(param => SelectViewModel(LabourLibraryViewModel));
			SelectedGroundworkViewCommand = new DelegateCommand(param => SelectViewModel(GroundWorkViewModel));
			SelectedConcreteWorkViewCommand = new DelegateCommand(param => SelectViewModel(ConcreteViewModel));
			SelectedBlockworkViewCommand = new DelegateCommand(param => SelectViewModel(BlockworkViewModel));
			SelectedFinishesViewCommand = new DelegateCommand(param => SelectViewModel(FinishesViewModel));
			SelectedRoofworkViewCommand = new DelegateCommand(param => SelectViewModel(RoofWorkViewModel));
			SelectedWindowAndDoorViewCommand = new DelegateCommand(param => SelectViewModel(WindowAndDoorViewModel));
			SelectedPaintworkViewCommand = new DelegateCommand(param => SelectViewModel(PaintWorkViewModel));
			SelectedSteelworkViewCommand = new DelegateCommand(param => SelectViewModel(SteelWorkViewModel));
			SelectedCustomRateInputViewCommand = new DelegateCommand(param => SelectViewModel(CustomRateListViewModel));
			SelectedCustomRateViewCommand = new DelegateCommand(param => SelectViewModel(CustomRateEntryViewModel));


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

		private async void OnLoginSucceeded(object sender, SignInViewModel.LoginEventArgs e)
		{
			if (e.LoggedInUser != null)
			{
				string deviceIpAddress = GetUserIpAddress();

				var existingUserIp = await _mongoDbService.GetUserIpAddressAsync(e.LoggedInUser.Id);
			}
		}

		private string GetUserIpAddress()
		{
			try
			{
				using (var webClient = new WebClient())
				{
					string ip = webClient.DownloadString("https://api.ipify.org/");
					return ip.Trim();
				}
			}
			catch
			{
				return "IP Unavailable";
			}
		}
	}

}
