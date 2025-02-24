using System.Windows;
using System.Windows.Controls;

namespace ADLMRateGen.View
{
	/// <summary>
	/// Interaction logic for ConcreteworkItemDetailControl.xaml
	/// </summary>
	public partial class ConcreteworkItemDetailControl : UserControl
	{
		public event Action BackRequested;
		public ConcreteworkItemDetailControl()
		{
			InitializeComponent();
		}

		private void BackButton_Click(object sender, RoutedEventArgs e)
		{
			// Raise an event so the parent can hide this detail view.
			BackRequested?.Invoke();
		}
	}
}
