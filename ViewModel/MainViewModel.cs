using ADLMRateGen.Command;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public DelegateCommand SelectedMaterialInputViewCommand { get; }
        public DelegateCommand SelectedMaterialLibraryViewCommand { get; }
        public MaterialPriceViewModel MaterialPriceViewModel { get; }
        public MaterialLibraryViewModel MaterialLibraryViewModel { get; }

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

        public MainViewModel(MaterialPriceViewModel priceVM, MaterialLibraryViewModel libraryVM)
        {
            MaterialPriceViewModel = priceVM;
            MaterialLibraryViewModel = libraryVM;

            // Wire events.
            MaterialPriceViewModel.MaterialSaved += OnMaterialSaved;
            MaterialLibraryViewModel.EditMaterialRequested += OnEditMaterialRequested;

            // Set default view.
            SelectedViewModel = MaterialPriceViewModel;
            SelectedMaterialInputViewCommand = new DelegateCommand(param => SelectViewModel(MaterialPriceViewModel));
            SelectedMaterialLibraryViewCommand = new DelegateCommand(param => SelectViewModel(MaterialLibraryViewModel));
        }

        private void OnMaterialSaved(MaterialModel material)
        {
            MaterialLibraryViewModel.AddOrUpdateMaterial(material);
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

        private void SelectViewModel(object parameter)
        {
            SelectedViewModel = parameter as ViewModelBase;
        }
    }
}
