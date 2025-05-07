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
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.View
{
	/// <summary>
	/// Interaction logic for MaterialLibraryView.xaml
	/// </summary>
	public partial class MaterialLibraryView : UserControl
	{
		private bool _isHooked;          // makes sure we subscribe only once

		public MaterialLibraryView()
		{
			InitializeComponent();

			//// once DataContext is set, hook the edit request
			//this.Loaded += (s, e) =>
			//{
			//	if (this.DataContext is MaterialLibraryViewModel vm)
			//	{
			//		vm.EditMaterialRequested += OnEditMaterialRequested;
			//	}
			//};

			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
		}

		/* ---------- view life‑cycle ---------------------------------------------------- */

		private void OnLoaded(object? s, RoutedEventArgs e)
		{
			if (_isHooked) return;

			if (DataContext is MaterialLibraryViewModel vm)
			{
				vm.EditMaterialRequested += OnEditMaterialRequested;
				_isHooked = true;
			}
		}

		private void OnUnloaded(object? s, RoutedEventArgs e)
		{
			if (DataContext is MaterialLibraryViewModel vm)
				vm.EditMaterialRequested -= OnEditMaterialRequested;

			// guard against memory‑leaks on the static event
			MaterialPriceViewModel.MaterialSaved -= OnMaterialSaved;
			_isHooked = false;
		}


		/* ---------- popup handling ----------------------------------------------------- */

		private void OnEditMaterialRequested(MaterialModel mat)
		{
			// build a fresh ViewModel for the popup – populate with the row’s data
			var editVm = new MaterialPriceViewModel
			{
				EditingMaterial = mat,
				MaterialName = mat.MaterialName,
				MaterialUnit = mat.MaterialUnit,
				MaterialPrice = mat.MaterialPrice,
				NewMaterialCategory = mat.MaterialCategory
			};

			// listen ONCE for the static save event – will fire when user clicks Update
			MaterialPriceViewModel.MaterialSaved += OnMaterialSaved;

			var popupContent = new MaterialPriceView { DataContext = editVm };
			(Application.Current.MainWindow as MainWindow)?.ShowPopup(popupContent);
		}

		private void OnMaterialSaved(MaterialModel _)
		{
			// close the popup – the library VM already updated / persisted the change
			(Application.Current.MainWindow as MainWindow)?.PopupHost.Hide();

			// unsubscribe immediately to avoid multiple calls next time
			MaterialPriceViewModel.MaterialSaved -= OnMaterialSaved;
		}

		//private void OnEditMaterialRequested(ADLMRateGen.ViewModel.Model.MaterialModel material)
		//{
		//	// build a fresh price‐entry VM, prefill from the model
		//	var editVm = new MaterialPriceViewModel
		//	{
		//		MaterialName = material.MaterialName,
		//		MaterialPrice = material.MaterialPrice,
		//		MaterialUnit = material.MaterialUnit,
		//		NewMaterialCategory = material.MaterialCategory,
		//		EditingMaterial = material
		//	};

		//	// when the price VM saves or updates, push it back into the library
		//	editVm.MaterialSaved += saved =>
		// {
		//  var win = Application.Current.MainWindow as MainWindow;
		//  win?.PopupHost.Hide();
		// }
		//	;

		//	var editView = new MaterialPriceView
		//	{
		//		DataContext = editVm
		//	};

		//	// hand off to the main window’s popup host
		//	var main = Application.Current.MainWindow as MainWindow;
		//	main?.ShowPopup(editView);
		//}
	}
}
