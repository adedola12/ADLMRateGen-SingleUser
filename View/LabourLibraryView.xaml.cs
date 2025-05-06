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
    /// Interaction logic for LabourLibraryView.xaml
    /// </summary>
    public partial class LabourLibraryView : UserControl
    {
        public LabourLibraryView()
        {
            InitializeComponent();

			// once DataContext is set, hook the edit request
			this.Loaded += (s, e) =>
			{
				if (this.DataContext is LabourLibraryViewModel vm)
				{
					vm.EditLabourRequested += OnEditLabourRequested;
				}
			};
		}

		private void OnEditLabourRequested(ADLMRateGen.ViewModel.Model.LabourModel labour)
		{
			var editVm = new LabourPriceViewModel
			{
				LabourName = labour.LabourName,
				LabourPrice = labour.LabourPrice,
				LabourUnit = labour.LabourUnit,
				NewLabourCategory = labour.LabourCategory,
				EditingLabour = labour
			};

			editVm.LabourSaved += saved =>
			{
				var win = Application.Current.MainWindow as MainWindow;
				win?.PopupHost.Hide();
			};

			var editView = new LabourPriceView
			{
				DataContext = editVm,
			};

			var main = Application.Current.MainWindow as MainWindow;
			main?.ShowPopup(editView);
		}

	}
}
