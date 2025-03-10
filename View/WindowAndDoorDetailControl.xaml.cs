using System.Windows;
using System.Windows.Controls;

namespace ADLMRateGen.View
{
    /// <summary>
    /// Interaction logic for WindowAndDoorDetailControl.xaml
    /// </summary>
    public partial class WindowAndDoorDetailControl : UserControl
    {
		public event Action BackRequested;

		public WindowAndDoorDetailControl()
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
