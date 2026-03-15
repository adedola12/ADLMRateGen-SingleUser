using System.Windows;
using System.Windows.Input;

namespace ADLMRateGen
{
    public partial class SignInWindow : Window
    {
        public SignInWindow(object? signInViewModel)
        {
            InitializeComponent();
            DataContext = signInViewModel;
        }

        private void TitlePill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
