using ADLMRateGen.View;            // ← class PopupHost is here
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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


		private PopupHost GlobalPopup =>
			((MainWindow)Application.Current.MainWindow).PopupHost;

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
			if (e.PropertyName != nameof(ViewModel.Groundwork.GroundWorkViewModel.SelectedDetail))
				return;

			Dispatcher.Invoke(() =>
			{
				var vm = (ViewModel.Groundwork.GroundWorkViewModel)sender!;

				if (vm.SelectedDetail is UserControl detailView)
					GlobalPopup.Show(detailView);   // ⬅ show in window‑level host
				else
					GlobalPopup.Hide();             // ⬅ hide when null
			});
		}
	}
}
