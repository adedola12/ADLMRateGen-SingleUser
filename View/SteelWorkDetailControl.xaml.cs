using System.Windows;
using System.Windows.Controls;

namespace ADLMRateGen.View
{
    /// <summary>
    /// Interaction logic for SteelWorkDetailControl.xaml
    /// </summary>
    public partial class SteelWorkDetailControl : UserControl
    {
		public event Action BackRequested;
		public SteelWorkDetailControl()
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
