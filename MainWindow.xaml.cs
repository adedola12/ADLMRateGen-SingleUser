using System.Windows;
using ADLMRateGen.ViewModel;
using ADLMRateGen.ViewModel.BlockWork;
using ADLMRateGen.ViewModel.ConcreteWork;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Finishes;
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.RoofWork;
using ADLMRateGen.ViewModel.WindowAndDoor;

namespace ADLMRateGen
{

    public partial class MainWindow : Window
    {
        
        public MainWindow()
        {
            InitializeComponent();
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
            var customInputVM = new CustomRateEntryViewModel();
            var customViewVM = new CustomRateListViewModel();

			this.DataContext = new MainViewModel(priceVM, libraryVM, labourVm, labourLibraryVM, groundworkVM, concreteWorkVM, 
                blockworkVM, finishesVM,roofworkVM, windowAndDoorVM, customViewVM,customInputVM);

        }
    }
}