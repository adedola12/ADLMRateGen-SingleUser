using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using ADLMRateGen.ViewModel.Groundwork;

namespace ADLMRateGen.View
{
    /// <summary>
    /// Interaction logic for GroundWorkView.xaml
    /// </summary>
    public partial class GroundWorkView : UserControl
    {
        public GroundWorkView()
        {
            InitializeComponent();


			// whenever DataContext changes, subscribe to its PropertyChanged
			DataContextChanged += OnDataContextChanged;
		}

		/* ───────────────── helpers ───────────────── */

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue is INotifyPropertyChanged oldVm)
				oldVm.PropertyChanged -= Vm_PropertyChanged;

			if (e.NewValue is INotifyPropertyChanged newVm)
				newVm.PropertyChanged += Vm_PropertyChanged;
		}

		/// <summary>
		/// React to <see cref="GroundWorkViewModel.SelectedDetail"/> changes:
		/// show the popup when a UserControl is supplied, hide when null.
		/// </summary>
		private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(GroundWorkViewModel.SelectedDetail)) return;

			Dispatcher.Invoke(() =>
			{
				var vm = (GroundWorkViewModel)sender!;

				if (vm.SelectedDetail is UserControl detailView)
					Popup.Show(detailView);   // display in the overlay
				else
					Popup.Hide();             // nothing selected – hide
			});
		}
	}
}
