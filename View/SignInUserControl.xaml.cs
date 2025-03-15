using System.Windows;
using System.Windows.Controls;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel;
using static ADLMRateGen.ViewModel.SignInViewModel;

namespace ADLMRateGen.View
{
	public partial class SignInUserControl : UserControl
	{
		public event EventHandler<LoginEventArgs> LoginSucceeded;

		public SignInUserControl()
		{
			InitializeComponent();

		
		}

		private void LoginButton_Click(object sender, RoutedEventArgs e)
		{
			if (DataContext is SignInViewModel vm)
			{
				// Transfer the PasswordBox value to the VM before command
				vm.Password = PasswordBox.Password;
			}
		}
	}
}
