using System;
using System.Windows;
using System.Windows.Controls;
using ADLMRateGen.ViewModel;

namespace ADLMRateGen.View
{
	public partial class SignInUserControl : UserControl
	{
		public event EventHandler<SignInViewModel.LoginEventArgs> LoginSucceeded;

		public SignInUserControl()
		{
			InitializeComponent();
		}

		// If you keep a manual “Log in” Click handler:
		private void LoginButton_Click(object sender, RoutedEventArgs e)
		{
			if (DataContext is SignInViewModel vm)
			{
				// push the secure string into the VM _before_ executing the command
				vm.Password = PasswordBox.Password;
			}
		}

		// Toggle show / hide (simple example)
		private bool _pwdVisible = false;
		private void TogglePwd_Click(object sender, RoutedEventArgs e)
		{
			_pwdVisible = !_pwdVisible;
			PasswordBox.Visibility = _pwdVisible ? Visibility.Hidden : Visibility.Visible;

			// quick & dirty: swap to a TextBox overlay if you need plain-text reveal
			// (or use a proper attached behaviour)
		}

		private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
		{
			if (DataContext is SignInViewModel vm)
				vm.Password = PasswordBox.Password;
		}

	}
}
