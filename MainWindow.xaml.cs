using System.Windows;
using ADLMRateGen.ViewModel;

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
            this.DataContext = new MainViewModel(priceVM, libraryVM, labourVm, labourLibraryVM);

        }
    }
}