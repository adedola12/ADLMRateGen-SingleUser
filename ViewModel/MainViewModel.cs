using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.BlockWork;
using ADLMRateGen.ViewModel.ConcreteWork;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Finishes;
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.Model;
using ADLMRateGen.ViewModel.Painting;
using ADLMRateGen.ViewModel.RoofWork;
using ADLMRateGen.ViewModel.SteelWork;
using ADLMRateGen.ViewModel.WindowAndDoor;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Windows;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
	public class MainViewModel : ViewModelBase
	{
		/* ───────── injected services ───────── */
		private readonly MongoDbService _mongoDbService;

		/* ───────── current user / auth ───────── */

private UserModel? _currentUser;
		public UserModel? CurrentUser
		{
			get => _currentUser;
			set
			{
				_currentUser = value;
				RaisePropertyChanged();             // ↺ notifies <Run …>
				RaisePropertyChanged(nameof(CurrentUsername));
			}
		}

		/* used directly by the banner if you prefer */
		public string CurrentUsername => _currentUser?.Username ?? string.Empty;

		/* ───────── login state ───────── */
		private bool _isLoggedIn;
		public bool IsLoggedIn
		{
			get => _isLoggedIn;
			set { _isLoggedIn = value; RaisePropertyChanged(); }
		}

		/* ───────── sidebar “active” flags ───────── */
		private bool _isLibraryShellActive;
		public bool IsLibraryShellActive
		{
			get => _isLibraryShellActive;
			set { _isLibraryShellActive = value; RaisePropertyChanged(); }
		}

		private bool _isMaterialInputActive;
		public bool IsMaterialInputActive
		{
			get => _isMaterialInputActive;
			set { _isMaterialInputActive = value; RaisePropertyChanged(); }
		}

		private bool _isMaterialLibraryActive;
		public bool IsMaterialLibraryActive
		{
			get => _isMaterialLibraryActive;
			set { _isMaterialLibraryActive = value; RaisePropertyChanged(); }
		}

		private bool _isLabourInputActive;
		public bool IsLabourInputActive
		{
			get => _isLabourInputActive;
			set { _isLabourInputActive = value; RaisePropertyChanged(); }
		}

		private bool _isLabourLibraryActive;
		public bool IsLabourLibraryActive
		{
			get => _isLabourLibraryActive;
			set { _isLabourLibraryActive = value; RaisePropertyChanged(); }
		}

		private bool _isGroundworkActive;
		public bool IsGroundworkActive
		{
			get => _isGroundworkActive;
			set { _isGroundworkActive = value; RaisePropertyChanged(); }
		}

		private bool _isConcreteViewActive;
		public bool IsConcreteViewActive
		{
			get => _isConcreteViewActive;
			set { _isConcreteViewActive = value; RaisePropertyChanged(); }
		}

		private bool _isBlockworkActive;
		public bool IsBlockworkActive
		{
			get => _isBlockworkActive;
			set { _isBlockworkActive = value; RaisePropertyChanged(); }
		}

		private bool _isFinishesActive;
		public bool IsFinishesActive
		{
			get => _isFinishesActive;
			set { _isFinishesActive = value; RaisePropertyChanged(); }
		}

		private bool _isRoofworkActive;
		public bool IsRoofworkActive
		{
			get => _isRoofworkActive;
			set { _isRoofworkActive = value; RaisePropertyChanged(); }
		}

		private bool _isWindowAndDoorActive;
		public bool IsWindowAndDoorActive
		{
			get => _isWindowAndDoorActive;
			set { _isWindowAndDoorActive = value; RaisePropertyChanged(); }
		}

		private bool _isPaintingActive;
		public bool IsPaintingActive
		{
			get => _isPaintingActive;
			set { _isPaintingActive = value; RaisePropertyChanged(); }
		}

		private bool _isSteelworkActive;
		public bool IsSteelworkActive
		{
			get => _isSteelworkActive;
			set { _isSteelworkActive = value; RaisePropertyChanged(); }
		}

		private bool _isCustomRateInputActive;
		public bool IsCustomRateInputActive
		{
			get => _isCustomRateInputActive;
			set { _isCustomRateInputActive = value; RaisePropertyChanged(); }
		}

		/* ───────── child view-models (public for binding) ───────── */
		public SignInViewModel SignInViewModel { get; }
		public MaterialPriceViewModel MaterialPriceViewModel { get; }
		public MaterialLibraryViewModel MaterialLibraryViewModel { get; }
		public LabourPriceViewModel LabourPriceViewModel { get; }
		public LabourLibraryViewModel LabourLibraryViewModel { get; }
		public LibraryShellViewModel LibraryShellViewModel { get; }
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
		public SearchBoxViewModel GlobalSearch { get; }


		private readonly SearchIndex _index = new();

		/* ───────── View switching ───────── */
		private ViewModelBase _selectedViewModel;
		public ViewModelBase SelectedViewModel
		{
			get => _selectedViewModel;
			set
			{
				_selectedViewModel = value;

				/* update all “active” flags */
				IsLibraryShellActive = value == LibraryShellViewModel;
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

		/* ───────── commands exposed to XAML ───────── */
		public ICommand SelectedMaterialInputViewCommand { get; }
		public ICommand SelectedMaterialLibraryViewCommand { get; }
		public ICommand SelectedLabourInputViewCommand { get; }
		public ICommand SelectedLabourLibraryViewCommand { get; }
		public ICommand SelectedLibraryShellViewCommand { get; }
		public ICommand SelectedGroundworkViewCommand { get; }
		public ICommand SelectedConcreteWorkViewCommand { get; }
		public ICommand SelectedBlockworkViewCommand { get; }
		public ICommand SelectedFinishesViewCommand { get; }
		public ICommand SelectedRoofworkViewCommand { get; }
		public ICommand SelectedWindowAndDoorViewCommand { get; }
		public ICommand SelectedPaintworkViewCommand { get; }
		public ICommand SelectedSteelworkViewCommand { get; }
		public ICommand SelectedCustomRateInputViewCommand { get; }
		public ICommand SelectedCustomRateViewCommand { get; }
		public ICommand LogoutCommand { get; }
		public ICommand OpenYoutubeCommand { get; }
		public ICommand HelpCommand { get; }

		/* ───────── ctor ───────── */
		public MainViewModel(
			MaterialPriceViewModel priceVM,
			MaterialLibraryViewModel libraryVM,
			LabourPriceViewModel labourVM,
			LabourLibraryViewModel labourLibVM,
			GroundWorkViewModel groundworkVM,
			ConcreteViewModel concreteVM,
			BlockworkViewModel blockworkVM,
			FinishesViewModel finishesVM,
			RoofWorkViewModel roofVM,
			WindowAndDoorViewModel winDoorVM,
			PaintWorkViewModel paintVM,
			SteelWorkViewModel steelVM,
			LibraryShellViewModel libraryShellVM,
			CustomRateListViewModel customListVM,
			CustomRateEntryViewModel customEntryVM,
			MongoDbService mongoDbService,
			SignInViewModel signInVM)
		{
			/* store deps */
			_mongoDbService = mongoDbService;

			/* ---------- create empty index & search VM ---------- */
			_index = new SearchIndex();          // stays empty for now
			GlobalSearch = new SearchBoxViewModel(_index);


			/* assign child VMs */
			MaterialPriceViewModel = priceVM;
			MaterialLibraryViewModel = libraryVM;
			LabourPriceViewModel = labourVM;
			LabourLibraryViewModel = labourLibVM;
			LibraryShellViewModel = libraryShellVM;
			GroundWorkViewModel = groundworkVM;
			ConcreteViewModel = concreteVM;
			BlockworkViewModel = blockworkVM;
			FinishesViewModel = finishesVM;
			RoofWorkViewModel = roofVM;
			WindowAndDoorViewModel = winDoorVM;
			PaintWorkViewModel = paintVM;
			SteelWorkViewModel = steelVM;
			CustomRateListViewModel = customListVM;
			CustomRateEntryViewModel = customEntryVM;
			SignInViewModel = signInVM;

			MaterialLibraryViewModel.LibraryChanged += () => _index.Rebuild(this);
			LabourLibraryViewModel.LibraryChanged += () => _index.Rebuild(this);
			CustomRateListViewModel.LibraryChanged += () => _index.Rebuild(this);



			/* wire events (material / labour edit-flow etc.) */
			MaterialPriceViewModel.MaterialSaved += m => libraryVM.AddOrUpdateMaterial(m);
			libraryVM.EditMaterialRequested += OnEditMaterialRequested;
			labourVM.LabourSaved += l => labourLibVM.AddOrUpdateLabour(l);
			labourLibVM.EditLabourRequested += OnEditLabourRequested;
			customListVM.OnViewRequested += rate =>
			{
				customEntryVM.LoadRate(rate);
			};
			signInVM.LoginSucceeded += OnLoginSucceeded;

			/* default screen */
			SelectedViewModel = SignInViewModel;

			/* command implementations */
			SelectedMaterialInputViewCommand = new RelayCommand(_ => SelectedViewModel = priceVM);

			//SelectedMaterialLibraryViewCommand = new RelayCommand(_ => SelectedViewModel = LibraryShellViewModel);
			//SelectedLibraryShellViewCommand = new RelayCommand(_ => SelectedViewModel = LibraryShellViewModel);      // new command

			SelectedMaterialLibraryViewCommand = new RelayCommand(_ => SelectedViewModel = LibraryShellViewModel);  // ★ correct target
			SelectedLibraryShellViewCommand = new RelayCommand(_ => SelectedViewModel = LibraryShellViewModel);



			SelectedLabourInputViewCommand = new RelayCommand(_ => SelectedViewModel = labourVM);
			SelectedLabourLibraryViewCommand = new RelayCommand(_ => SelectedViewModel = labourLibVM);
			SelectedGroundworkViewCommand = new RelayCommand(_ => SelectedViewModel = groundworkVM);
			SelectedConcreteWorkViewCommand = new RelayCommand(_ => SelectedViewModel = concreteVM);
			SelectedBlockworkViewCommand = new RelayCommand(_ => SelectedViewModel = blockworkVM);
			SelectedFinishesViewCommand = new RelayCommand(_ => SelectedViewModel = finishesVM);
			SelectedRoofworkViewCommand = new RelayCommand(_ => SelectedViewModel = roofVM);
			SelectedWindowAndDoorViewCommand = new RelayCommand(_ => SelectedViewModel = winDoorVM);
			SelectedPaintworkViewCommand = new RelayCommand(_ => SelectedViewModel = paintVM);
			SelectedSteelworkViewCommand = new RelayCommand(_ => SelectedViewModel = steelVM);
			SelectedCustomRateInputViewCommand = new RelayCommand(_ => SelectedViewModel = customListVM);
			SelectedCustomRateViewCommand = new RelayCommand(_ => SelectedViewModel = customEntryVM);
			LogoutCommand = new RelayCommand(_ => Logout());
			OpenYoutubeCommand = new RelayCommand(_ => OpenYoutube());
			HelpCommand = new RelayCommand(_ => SendHelpEmail());


			_index.Rebuild(this);

			/* whenever a library changes, rebuild */


			GroundWorkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
			ConcreteViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
			BlockworkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
			FinishesViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
			RoofWorkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
			WindowAndDoorViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
			PaintWorkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
			SteelWorkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);

		}

		/* ───────── auto‑login helper ───────── */
		private bool TryAutoLogin(out UserModel? user)
		{
			user = null;

			// read cached config
			var cfg = ConfigManager.LoadConfig();
			if (cfg == null || string.IsNullOrWhiteSpace(cfg.AuthToken))
				return false;

			// basic expiry check
			if (cfg.AuthExpiry < DateTime.Now)
				return false;

			// validate / decode the token
			var authTok = new AuthTok();
			var decoded = authTok.ValidateToken(cfg.AuthToken);
			if (decoded == null)          // invalid signature etc.
				return false;

			user = decoded;               // you may want to map to UserModel
			return true;
		}



		/* ───────── edit helpers ───────── */
		private void OnEditMaterialRequested(MaterialModel m)
		{
			//MaterialPriceViewModel.LoadForEdit(m);
			//SelectedViewModel = MaterialPriceViewModel;
		}

		private void OnEditLabourRequested(LabourModel l)
		{
			//LabourPriceViewModel.LoadForEdit(l);
			//SelectedViewModel = LabourPriceViewModel;
		}

		/* ───────── sign-in result ───────── */
		private async void OnLoginSucceeded(object? s, SignInViewModel.LoginEventArgs e)
		{
			if (e.LoggedInUser == null) return;

			IsLoggedIn = true;
			CurrentUser = e.LoggedInUser;

			var ip = GetUserIpAddress();
			_currentUser.IpAddress = ip;
			await _mongoDbService.UpdateUserIpAddressAsync(_currentUser.Id, ip);

			var authTok = new AuthTok();
			ConfigManager.SaveConfig(new AppConfig
			{
				AuthToken = authTok.GenerateAuthToken(_currentUser),
				AuthExpiry = DateTime.Now.AddDays(15)
			});

			// somewhere in the constructor or in your auto‑login helper
			if (TryAutoLogin(out var loggedInUser))
			{
				IsLoggedIn = true;
				CurrentUser = loggedInUser;
				SelectedViewModel = LibraryShellViewModel;   // land on shell, not material library
			}


			SelectedViewModel = LibraryShellViewModel;   // land on library tabs
		}

		/* ───────── misc helpers ───────── */
		private static string GetUserIpAddress()
		{
			try { return new WebClient().DownloadString("https://api.ipify.org/").Trim(); }
			catch { return "IP-Unavailable"; }
		}

		private void SendHelpEmail()
		{
			if (CurrentUser?.Email == null) return;

			var to = "admin@adlmstudio.net";
			var subject = Uri.EscapeDataString("Need help with ADLM Rate Gen");
			var body = Uri.EscapeDataString(
				$"Hello ADLM, my name is {CurrentUser.Email} and I need help with the ADLM Rate Gen.");
			var mailto = $"mailto:{to}?subject={subject}&body={body}";

			Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
		}


		private async void Logout()
		{
			try
			{
				if (_currentUser != null)
					await _mongoDbService.UpdateUserIpAddressAsync(_currentUser.Id, "");

				ConfigManager.ClearConfig();
				_currentUser = null;

				IsLoggedIn = false;
				SelectedViewModel = SignInViewModel;
				//SignInViewModel.ClearFields();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Log-out failed\n{ex.Message}");
			}
		}

		private static void OpenYoutube()
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "https://youtube.com/playlist?list=PLk1KkUNE9ZrO5IPh7p3-5zxfDFs1Dl9b-&si=xi4dt-Fmy17_2KsM",
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Unable to open browser.\n{ex.Message}");
			}
		}
	}
}
