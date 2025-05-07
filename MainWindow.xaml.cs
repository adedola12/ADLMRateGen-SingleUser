using System.Windows;
using System.Windows.Controls;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel;
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

namespace ADLMRateGen
{

    public partial class MainWindow : Window
    {

		private readonly LibraryShellViewModel _shellVm;
		private readonly PopupHost _popup;

		public MainWindow()
		{
			InitializeComponent();

			// 1) Create your sub-VMs
			var priceVM = new MaterialPriceViewModel();
			var libraryVM = new MaterialLibraryViewModel();
			var labourVm = new LabourPriceViewModel();
			var labourLibraryVM = new LabourLibraryViewModel();
			_shellVm = new LibraryShellViewModel(libraryVM, labourLibraryVM);
			var groundworkVM = new GroundWorkViewModel(libraryVM, labourLibraryVM);
			var concreteWorkVM = new ConcreteViewModel(libraryVM, labourLibraryVM);
			var blockworkVM = new BlockworkViewModel(libraryVM, labourLibraryVM, concreteWorkVM);
			var finishesVM = new FinishesViewModel(libraryVM, labourLibraryVM, blockworkVM);
			var roofworkVM = new RoofWorkViewModel(libraryVM, labourLibraryVM);
			var windowAndDoorVM = new WindowAndDoorViewModel(libraryVM, labourLibraryVM);
			var paintVM = new PaintWorkViewModel(libraryVM, labourLibraryVM);
			var steelWorkVM = new SteelWorkViewModel(libraryVM, labourLibraryVM);
			var customInputVM = new CustomRateEntryViewModel();
			var customViewVM = new CustomRateListViewModel();

			// 2) Create the sign-in VM + Mongo service
			var connectionString = "mongodb+srv://dolapo836:[REDACTED]@adlmratedb.zeur8.mongodb.net/?retryWrites=true&w=majority&appName=ADLMRateDB";
			
			var databaseName = "ADLMRateDB";
			var collectionName = "Users";

			var mongoDbService = new MongoDbService(connectionString, databaseName, collectionName);
			var signInVM = new SignInViewModel(mongoDbService);

			// ───────── event wiring ─────────
			_shellVm.RequestAddMaterial += OnRequestAddMaterial;
			_shellVm.RequestEditMaterial += OnRequestEditMaterial;
			_shellVm.RequestAddLabour += OnRequestAddLabour;
			_popup = PopupHost;


			// 3) Create the MainViewModel
			var mainVM = new MainViewModel(
				priceVM,
				libraryVM,
				labourVm,
				labourLibraryVM,
				groundworkVM,
				concreteWorkVM,
				blockworkVM,
				finishesVM,
				roofworkVM,
				windowAndDoorVM,
				paintVM,
				steelWorkVM,
				_shellVm,
				customViewVM,
				customInputVM,
				mongoDbService,
				signInVM);

			// 4) Check if we have a valid Auth token => skip sign in
			var config = ConfigManager.LoadConfig();
			//if (config.AuthToken != null
			//	&& config.AuthExpiry.HasValue
			//	&& config.AuthExpiry > DateTime.Now)
			//{
			//	// user is already "authenticated"
			//	mainVM.IsLoggedIn = true;
			//	// default to e.g. Material Library or GroundWork
			//	mainVM.SelectedViewModel = mainVM.MaterialLibraryViewModel;

			//	/* NEW: pull user‑id from the token and fetch his document */
			//	var userId = JwtHelper.GetUserId(config.AuthToken);
			//	if (!string.IsNullOrEmpty(userId))
			//	{
			//		var user = mongoDbService.GetUserByIdAsync(userId)
			//								 .GetAwaiter()
			//								 .GetResult();   // sync wait – window ctor
			//		if (user != null)
			//			mainVM.CurrentUser = user;           // ⭐ notifies the banner
			//	}
			//}

			// 4) check persisted token … (unchanged lines omitted for brevity)
			if (config.AuthToken != null
				&& config.AuthExpiry is { } exp && exp > DateTime.Now)
			{
				mainVM.IsLoggedIn = true;
				mainVM.SelectedViewModel = mainVM.MaterialLibraryViewModel;

				/* fetch user synchronously – NO async‑await on UI thread */
				var userId = JwtHelper.GetUserId(config.AuthToken);
				if (!string.IsNullOrEmpty(userId))
				{
					var user = mongoDbService.GetUserById(userId);   // ← new sync helper
					if (user != null)
						mainVM.CurrentUser = user;                   // updates banner
				}
			}




			// 5) Finally set the DataContext so the UI sees mainVM
			this.DataContext = mainVM;
		}

		//private void OnRequestAddMaterial() => ShowMaterialPopup(null);

		//private void OnRequestEditMaterial(MaterialModel toEdit) => ShowMaterialPopup(toEdit);

		//private void ShowMaterialPopup(MaterialModel existing)
		//{
		//	var vm = new MaterialPriceViewModel();
		//	if (existing != null)
		//	{
		//		vm.MaterialName = existing.MaterialName;
		//		vm.MaterialUnit = existing.MaterialUnit;
		//		vm.MaterialPrice = existing.MaterialPrice;
		//		vm.NewMaterialCategory = existing.MaterialCategory;
		//		vm.EditingMaterial = existing;
		//	}

		//	vm.MaterialSaved += mat =>
		//	{
		//		var shell = ((MainViewModel)DataContext).LibraryShellViewModel;
		//		shell.MaterialLibraryViewModel.AddOrUpdateMaterial(mat);
		//		PopupHost.Hide();
		//	};

		//	// now SHOW via PopupHost
		//	var view = new MaterialPriceView { DataContext = vm };
		//	PopupHost.Show(view);
		//}

		// ========== MATERIAL POPUP ==========
		private void OnRequestAddMaterial() => ShowMaterialPopup(null);
		private void OnRequestEditMaterial(MaterialModel m) => ShowMaterialPopup(m);

		private void ShowMaterialPopup(MaterialModel existing)
		{
			var vm = new MaterialPriceViewModel();
			if (existing != null)
			{
				vm.EditingMaterial = existing;
				vm.MaterialName = existing.MaterialName;
				vm.MaterialUnit = existing.MaterialUnit;
				vm.MaterialPrice = existing.MaterialPrice;
				vm.NewMaterialCategory = existing.MaterialCategory;
			}

			MaterialPriceViewModel.MaterialSaved += mat =>
			{
				_shellVm.MaterialLibraryViewModel.AddOrUpdateMaterial(mat);
				_popup.Hide();
			};

			_popup.Show(new MaterialPriceView { DataContext = vm });
		}

		// ========== LABOUR POPUP ==========
		private void OnRequestAddLabour() => ShowLabourPopup(null);

		private void ShowLabourPopup(LabourModel existing)
		{
			var vm = new LabourPriceViewModel();
			if (existing != null)
			{
				vm.EditingLabour = existing;
				vm.LabourName = existing.LabourName;
				vm.LabourUnit = existing.LabourUnit;
				vm.LabourPrice = existing.LabourPrice;
				vm.NewLabourCategory = existing.LabourCategory;
			}

			vm.LabourSaved += lab =>
			{
				_shellVm.LabourLibraryViewModel.AddOrUpdateLabour(lab);
				_popup.Hide();
			};

			_popup.Show(new LabourPriceView { DataContext = vm });
		}

		// if you still wired up a close button inside PopupHost to call this...
		private void OnPopupClose(object sender, RoutedEventArgs e)
		{
			PopupHost.Hide();
		}

		/// <summary>
		/// Show the transparent overlay + host the given view in it.
		/// </summary>
		public void ShowPopup(UserControl content)
		{
			// this.Popup is your PopupHost field
			this.PopupHost.Show(content);
		}

	}
}