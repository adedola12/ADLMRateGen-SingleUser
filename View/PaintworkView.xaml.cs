using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ADLMRateGen.ViewModel.Painting;

namespace ADLMRateGen.View
{
    /// <summary>
    /// Interaction logic for PaintworkView.xaml
    /// </summary>
    public partial class PaintworkView : UserControl
    {
        public PaintworkView()
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

		private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(PaintWorkViewModel.SelectedDetail))
				return;
			Dispatcher.Invoke(() =>
			{
				var vm = (PaintWorkViewModel)sender!;
				if (vm.SelectedDetail is UserControl detailView)
					GlobalPopup.Show(detailView);   // ⬅ show in window‑level host
				else
					GlobalPopup.Hide();             // ⬅ hide when null
			});
		}
	}
}
