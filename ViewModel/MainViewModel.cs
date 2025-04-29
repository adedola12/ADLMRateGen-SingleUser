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
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using ADLMRateGen.Helpers;

namespace ADLMRateGen.ViewModel
{
	public class MainViewModel : ViewModelBase
	{
		private readonly MongoDbService _mongoDbService;
		private UserModel _currentUser;
		public UserModel CurrentUser
		{
			get => _currentUser;
			set { _currentUser = value; RaisePropertyChanged(); }
		}

		private bool _isLoggedIn;

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

		public ICommand LogoutCommand { get; }

		public SignInViewModel SignInViewModel { get; }

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

		public bool IsLoggedIn
		{
			get => _isLoggedIn;
			set { _isLoggedIn = value; RaisePropertyChanged(); }
		}

		private ViewModelBase _selectedViewModel;
		public ViewModelBase SelectedViewModel
		{
			get => _selectedViewModel;
			set
			{
				_selectedViewModel = value;
				IsMaterialInputActive = value == MaterialPriceViewModel;
				IsMaterialLibraryActive = value == MaterialLibraryViewModel;
				IsLabourInputActive = value == LabourPriceViewModel;
				IsLabourLibraryActive = value == LabourLibraryViewModel;
				IsGroundworkActive = value == GroundWorkViewModel;
				IsConcreteViewActive = value == ConcreteViewModel;
				IsBlockworkActive = value == BlockworkViewModel;
				IsFinishesActive = value == FinishesViewModel;
				IsRoofworkActive = value == RoofWorkViewModel;
				IsWindowAndDoorActive = value == WindowAndDoorViewModel;
				IsPaintingActive = value == PaintWorkViewModel;
				IsSteelworkActive = value == SteelWorkViewModel;
				IsCustomRateInputActive = value == CustomRateListViewModel;
				RaisePropertyChanged();
			}
		}

		private bool _isMaterialInputActive;
		public bool IsMaterialInputActive { get => _isMaterialInputActive; set { _isMaterialInputActive = value; RaisePropertyChanged(); } }

		private bool _isMaterialLibraryActive;
		public bool IsMaterialLibraryActive { get => _isMaterialLibraryActive; set { _isMaterialLibraryActive = value; RaisePropertyChanged(); } }

		private bool _isLabourInputActive;
		public bool IsLabourInputActive { get => _isLabourInputActive; set { _isLabourInputActive = value; RaisePropertyChanged(); } }

		private bool _isLabourLibraryActive;
		public bool IsLabourLibraryActive { get => _isLabourLibraryActive; set { _isLabourLibraryActive = value; RaisePropertyChanged(); } }

		private bool _isGroundworkActive;
		public bool IsGroundworkActive { get => _isGroundworkActive; set { _isGroundworkActive = value; RaisePropertyChanged(); } }

		private bool _isConcreteViewActive;
		public bool IsConcreteViewActive { get => _isConcreteViewActive; set { _isConcreteViewActive = value; RaisePropertyChanged(); } }

		private bool _isBlockworkActive;
		public bool IsBlockworkActive { get => _isBlockworkActive; set { _isBlockworkActive = value; RaisePropertyChanged(); } }

		private bool _isFinishesActive;
		public bool IsFinishesActive { get => _isFinishesActive; set { _isFinishesActive = value; RaisePropertyChanged(); } }

		private bool _isRoofworkActive;
		public bool IsRoofworkActive { get => _isRoofworkActive; set { _isRoofworkActive = value; RaisePropertyChanged(); } }

		private bool _isWindowAndDoorActive;
		public bool IsWindowAndDoorActive { get => _isWindowAndDoorActive; set { _isWindowAndDoorActive = value; RaisePropertyChanged(); } }

		private bool _isPaintingActive;
		public bool IsPaintingActive { get => _isPaintingActive; set { _isPaintingActive = value; RaisePropertyChanged(); } }

		private bool _isSteelworkActive;
		public bool IsSteelworkActive { get => _isSteelworkActive; set { _isSteelworkActive = value; RaisePropertyChanged(); } }

		private bool _isCustomRateInputActive;
		public bool IsCustomRateInputActive { get => _isCustomRateInputActive; set { _isCustomRateInputActive = value; RaisePropertyChanged(); } }

		public MainViewModel(MaterialPriceViewModel priceVM, MaterialLibraryViewModel libraryVM, LabourPriceViewModel labourVM,
			LabourLibraryViewModel labourLibraryVM, GroundWorkViewModel groundworkVM, ConcreteViewModel concreteWorkViewModel,
			BlockworkViewModel blockworkViewModel, FinishesViewModel finishesViewModel, RoofWorkViewModel roofworkVM,
			WindowAndDoorViewModel windowAndDoorVM, PaintWorkViewModel paintWorkViewModel, SteelWorkViewModel steelWorkViewModel,
			CustomRateListViewModel customRateListViewModel, CustomRateEntryViewModel customRateEntryViewModel, MongoDbService mongoDbService,
			SignInViewModel signinVM)
		{
			IsLoggedIn = false;
			_mongoDbService = mongoDbService;

			SignInViewModel = signinVM;

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

			CustomRateListViewModel.OnViewRequested += (rate) =>
			{
				CustomRateEntryViewModel.LoadRate(rate);
				SelectedViewModel = CustomRateEntryViewModel;
			};

			MaterialPriceViewModel.MaterialSaved += OnMaterialSaved;
			MaterialLibraryViewModel.EditMaterialRequested += OnEditMaterialRequested;

			LabourPriceViewModel.LabourSaved += OnLabourSaved;
			LabourLibraryViewModel.EditLabourRequested += OnEditLabourRequested;

			SelectedViewModel = SignInViewModel;
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
			LogoutCommand = new RelayCommand(_ => Logout());
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
			MaterialPriceViewModel.EditingMaterial = material;
			MaterialPriceViewModel.MaterialName = material.MaterialName;
			MaterialPriceViewModel.MaterialUnit = material.MaterialUnit;
			MaterialPriceViewModel.MaterialPrice = material.MaterialPrice;
			MaterialPriceViewModel.NewMaterialCategory = material.MaterialCategory;
			SelectedViewModel = MaterialPriceViewModel;
		}

		private void OnEditLabourRequested(LabourModel labour)
		{
			LabourPriceViewModel.EditingLabour = labour;
			LabourPriceViewModel.LabourName = labour.LabourName;
			LabourPriceViewModel.LabourUnit = labour.LabourUnit;
			LabourPriceViewModel.LabourPrice = labour.LabourPrice;
			LabourPriceViewModel.NewLabourCategory = labour.LabourCategory;
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
				IsLoggedIn = true;
				string deviceIpAddress = GetUserIpAddress();

				var existingUserIp = await _mongoDbService.GetUserIpAddressByUserIdAsync(e.LoggedInUser.Id);
				if (existingUserIp != null && existingUserIp != deviceIpAddress)
				{
					MessageBox.Show("Error: User already logged in from another device.");
					return;
				}

				_currentUser = e.LoggedInUser;
				_currentUser.IpAddress = deviceIpAddress;

				try
				{
					await _mongoDbService.UpdateUserIpAddressAsync(_currentUser.Id, deviceIpAddress);
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Error Updating IP address: {ex.Message}");
				}

				var authTok = new AuthTok();
				var config = new AppConfig { AuthToken = authTok.GenerateAuthToken(_currentUser), AuthExpiry = DateTime.Now.AddDays(15) };
				ConfigManager.SaveConfig(config);

				SelectedViewModel = MaterialLibraryViewModel;
				MessageBox.Show($"Welcome, {_currentUser.Username}! Your Account Secured");
			}
			else
			{
				MessageBox.Show("Unexpected login error.");
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

		// -------- LOG-OUT IMPLEMENTATION --------
		private async void Logout()
		{
			try
			{
				// 1) clear the IP lock on this user (optional but polite)
				if (_currentUser != null)
				{
					await _mongoDbService.UpdateUserIpAddressAsync(_currentUser.Id, "");
				}

				// 2) wipe any persisted token / cached config
				ConfigManager.ClearConfig();          // implement as needed

				// 3) reset local state
				_currentUser = null;
				IsLoggedIn = false;                 // hides the main shell
				SelectedViewModel = SignInViewModel;  // shows the sign-in screen

				// 4) optionally clear the textboxes
				if (SignInViewModel is { } vm)
				{
					vm.Username = string.Empty;
					vm.Password = string.Empty;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error while logging out: {ex.Message}");
			}
		}
	}
}
