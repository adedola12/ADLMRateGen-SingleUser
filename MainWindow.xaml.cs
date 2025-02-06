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
            this.DataContext = new MainViewModel(priceVM, libraryVM);

        }
    }
}