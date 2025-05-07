using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
    public class MainMaterialViewModel : ViewModelBase
    {
        public MaterialPriceViewModel PriceViewModel { get; }
        public MaterialLibraryViewModel LibraryViewModel { get; }

        public MainMaterialViewModel(MaterialPriceViewModel priceViewModel, MaterialLibraryViewModel libraryViewModel)
        {
            PriceViewModel = priceViewModel;
            LibraryViewModel = libraryViewModel;

            // When a new material is saved or updated, update the library.
            MaterialPriceViewModel.MaterialSaved += OnMaterialSaved;
            // When an edit is requested from the library, load that item into the price view.
            LibraryViewModel.EditMaterialRequested += OnEditMaterialRequested;
        }

        private void OnMaterialSaved(MaterialModel material)
        {
            LibraryViewModel.AddOrUpdateMaterial(material);
        }

        private void OnEditMaterialRequested(MaterialModel material)
        {
            // Load the material into the price view model for editing.
            PriceViewModel.EditingMaterial = material;
            PriceViewModel.MaterialName = material.MaterialName;
            PriceViewModel.MaterialUnit = material.MaterialUnit;
            PriceViewModel.MaterialPrice = material.MaterialPrice;
            PriceViewModel.NewMaterialCategory = material.MaterialCategory;
        }
    }
}
