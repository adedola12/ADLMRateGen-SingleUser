using System.Windows;
using System.Windows.Controls;

namespace ADLMRateGen.View
{
    /// <summary>
    /// Interaction logic for MepWorkItemDetailControl.xaml
    /// </summary>
    public partial class MepWorkItemDetailControl : UserControl
    {
		public event Action BackRequested;

		public MepWorkItemDetailControl()
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
