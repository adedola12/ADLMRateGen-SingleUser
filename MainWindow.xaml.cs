using System;
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

        // ✅ keep a reference so we can unsubscribe from the STATIC event
        private Action<MaterialModel>? _materialPopupSavedHandler;

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
            var matColName = "Materials";
            var labColName = "labours";

            var mongoDbService = new MongoDbService(connectionString, databaseName, collectionName, matColName, labColName);
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

            // 4) Persisted token auto login
            var config = ConfigManager.LoadConfig();
            if (config.AuthToken != null && config.AuthExpiry is { } exp && exp > DateTime.Now)
            {
                mainVM.IsLoggedIn = true;
                mainVM.SelectedViewModel = mainVM.MaterialLibraryViewModel;

                var userId = JwtHelper.GetUserId(config.AuthToken);
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = mongoDbService.GetUserById(userId);
                    if (user != null)
                        mainVM.CurrentUser = user;
                }
            }

            // 5) Set DataContext
            DataContext = mainVM;
        }

        // ========== MATERIAL POPUP ==========
        private void OnRequestAddMaterial() => ShowMaterialPopup(null);
        private void OnRequestEditMaterial(MaterialModel m) => ShowMaterialPopup(m);

        private void ShowMaterialPopup(MaterialModel? existing)
        {
            // Always unsubscribe previous handler (prevents duplicates)
            if (_materialPopupSavedHandler != null)
            {
                MaterialPriceViewModel.MaterialSaved -= _materialPopupSavedHandler;
                _materialPopupSavedHandler = null;
            }

            var vm = new MaterialPriceViewModel();

            if (existing != null)
            {
                vm.EditingMaterial = existing;
                vm.MaterialName = existing.MaterialName;
                vm.MaterialUnit = existing.MaterialUnit;
                vm.MaterialPrice = existing.MaterialPrice;
                vm.NewMaterialCategory = existing.MaterialCategory;
            }

            // ✅ because MaterialSaved is STATIC, subscribe using the TYPE NAME
            _materialPopupSavedHandler = mat =>
            {
                // unsubscribe immediately after first save (important)
                if (_materialPopupSavedHandler != null)
                {
                    MaterialPriceViewModel.MaterialSaved -= _materialPopupSavedHandler;
                    _materialPopupSavedHandler = null;
                }

                _shellVm.MaterialLibraryViewModel.AddOrUpdateMaterial(mat);
                _popup.Hide();
            };

            MaterialPriceViewModel.MaterialSaved += _materialPopupSavedHandler;

            _popup.Show(new MaterialPriceView { DataContext = vm });
        }

        // ========== LABOUR POPUP ==========
        private void OnRequestAddLabour() => ShowLabourPopup(null);

        private void ShowLabourPopup(LabourModel? existing)
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

        private void OnPopupClose(object sender, RoutedEventArgs e)
        {
            // cleanup static handler if user closes without saving
            if (_materialPopupSavedHandler != null)
            {
                MaterialPriceViewModel.MaterialSaved -= _materialPopupSavedHandler;
                _materialPopupSavedHandler = null;
            }

            PopupHost.Hide();
        }

        private void ToCmApp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                this,
                "Coming soon.",
                "ADLM Rate Gen",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        public void ShowPopup(UserControl content)
        {
            PopupHost.Show(content);
        }
    }
}
