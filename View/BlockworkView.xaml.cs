using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ADLMRateGen.ViewModel.BlockWork;

namespace ADLMRateGen.View
{
	/// <summary>
	/// Interaction logic for BlockworkView.xaml
	/// </summary>
	public partial class BlockworkView : UserControl
	{
		public BlockworkView()
		{
			InitializeComponent();

			// whenever DataContext changes, subscribe to its PropertyChanged
			DataContextChanged += OnDataContextChanged;
		}

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
			if (e.PropertyName != nameof(BlockworkViewModel.SelectedDetail))
				return;
			Dispatcher.Invoke(() =>
			{
				var vm = (BlockworkViewModel)sender!;
				if (vm.SelectedDetail is UserControl detailView)
					GlobalPopup.Show(detailView);   // ⬅ show in window‑level host
				else
					GlobalPopup.Hide();             // ⬅ hide when null
			});
		}
	}
}
