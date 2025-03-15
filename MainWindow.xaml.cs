using System.Windows;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel;
using ADLMRateGen.ViewModel.BlockWork;
using ADLMRateGen.ViewModel.ConcreteWork;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Finishes;
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.Painting;
using ADLMRateGen.ViewModel.RoofWork;
using ADLMRateGen.ViewModel.SteelWork;
using ADLMRateGen.ViewModel.WindowAndDoor;

namespace ADLMRateGen
{

    public partial class MainWindow : Window
    {

		public MainWindow()
		{
			InitializeComponent();

			// 1) Create your sub-VMs
			var priceVM = new MaterialPriceViewModel();
			var libraryVM = new MaterialLibraryViewModel();
			var labourVm = new LabourPriceViewModel();
			var labourLibraryVM = new LabourLibraryViewModel();
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
				customViewVM,
				customInputVM,
				mongoDbService,
				signInVM);

			// 4) Check if we have a valid Auth token => skip sign in
			var config = ConfigManager.LoadConfig();
			if (config.AuthToken != null
				&& config.AuthExpiry.HasValue
				&& config.AuthExpiry > DateTime.Now)
			{
				// user is already "authenticated"
				mainVM.IsLoggedIn = true;
				// default to e.g. Material Library or GroundWork
				mainVM.SelectedViewModel = mainVM.MaterialLibraryViewModel;
			}

			// 5) Finally set the DataContext so the UI sees mainVM
			this.DataContext = mainVM;
		}
	}
}