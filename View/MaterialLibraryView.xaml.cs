using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ADLMRateGen.ViewModel;

namespace ADLMRateGen.View
{
    /// <summary>
    /// Interaction logic for MaterialLibraryView.xaml
    /// </summary>
    public partial class MaterialLibraryView : UserControl
    {
		public MaterialLibraryView()
		{
			InitializeComponent();

			// once DataContext is set, hook the edit request
			this.Loaded += (s, e) =>
			{
				if (this.DataContext is MaterialLibraryViewModel vm)
				{
					vm.EditMaterialRequested += OnEditMaterialRequested;
				}
			};
		}
		private void OnEditMaterialRequested(ADLMRateGen.ViewModel.Model.MaterialModel material)
		{
			// build a fresh price‐entry VM, prefill from the model
			var editVm = new MaterialPriceViewModel
			{
				MaterialName = material.MaterialName,
				MaterialPrice = material.MaterialPrice,
				MaterialUnit = material.MaterialUnit,
				NewMaterialCategory = material.MaterialCategory,
				EditingMaterial = material
			};

			// when the price VM saves or updates, push it back into the library
			editVm.MaterialSaved += saved =>
   {
				var win = Application.Current.MainWindow as MainWindow;
				win?.PopupHost.Hide();
				   }
			;

			var editView = new MaterialPriceView
			{
				DataContext = editVm
			};

			// hand off to the main window’s popup host
			var main = Application.Current.MainWindow as MainWindow;
			main?.ShowPopup(editView);
		}
	}
}
