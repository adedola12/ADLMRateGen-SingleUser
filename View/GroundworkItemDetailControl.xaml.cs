using System.Windows;
using System.Windows.Controls;

namespace ADLMRateGen.View
{
    /// <summary>
    /// Interaction logic for GroundworkItemDetailControl.xaml
    /// </summary>
    public partial class GroundworkItemDetailControl : UserControl
    {
        public event Action BackRequested;
        public GroundworkItemDetailControl()
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
