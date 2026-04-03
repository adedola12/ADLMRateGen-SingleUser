using System.Windows;
using System.Windows.Controls;

namespace ADLMRateGen.View
{
    public partial class LibrarySuccessView : UserControl
    {
        public LibrarySuccessView(string message = "Library Updated")
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
                mw.PopupHost.Hide();
        }
    }
}
