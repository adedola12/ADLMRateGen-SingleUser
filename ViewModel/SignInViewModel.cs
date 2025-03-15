using ADLMRateGen.Command;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;
using System.Net.NetworkInformation;
using System.Net;
using System.Windows.Input;
using System.Windows;

namespace ADLMRateGen.ViewModel
{
    public class SignInViewModel: ViewModelBase
    {
		private readonly MongoDbService _mongoDbService;
		private string _username;
		private string _password;
		private bool _isLoading;

		public string Username
		{
			get => _username;
			set { _username = value; RaisePropertyChanged(nameof(Username)); }
		}
		public string Password
		{
			get => _password;
			set { _password = value; RaisePropertyChanged(nameof(Password)); }
		}
		public bool IsLoading
		{
			get => _isLoading;
			set { _isLoading = value; RaisePropertyChanged(nameof(IsLoading)); }
		}

		public ICommand LoginCommand { get; }

		public event EventHandler<LoginEventArgs> LoginSucceeded;

		public SignInViewModel(MongoDbService mongoDbService)
		{
			_mongoDbService = mongoDbService;
			LoginCommand = new RelayCommand(async _ => await LoginAsync());
		}

		private async Task LoginAsync()
		{
			IsLoading = true;
			if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
			{
				MessageBox.Show("Please enter your username and password");
				IsLoading = false;
				return;
			}

			if (!IsNetworkAvailable())
			{
				MessageBox.Show("An internet connection is required to login. Please check your network connection and try again.");
				IsLoading = false;
				return;
			}

			try
			{
				
				var user = await _mongoDbService.GetUserAsync(Username, Password);
				if (user != null)
				{
					// Update IP address if different and update expiration (set 30 days from now)
					//string currentIp = GetUserIpAddress();
					//if (user.IpAddress != currentIp)
					//{
					//	user.IpAddress = currentIp;
					//	user.UpdatedAt = DateTime.UtcNow;
					//	user.ExpirationDate = DateTime.UtcNow.AddDays(30);
					//	await _mongoDbService.UpdateUserAsync(user);
					//}
					// Show a success message when sign-in is successful
					LoginSucceeded?.Invoke(this, new LoginEventArgs(user));
					MessageBox.Show("Sign in successful!");
				}
				else
				{
					MessageBox.Show("Invalid username or password");
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Login Error: {ex.Message}");
			}
			finally
			{
				IsLoading = false;
			}
		}

		private bool IsNetworkAvailable()
		{
			return NetworkInterface.GetIsNetworkAvailable();
		}

		public class LoginEventArgs : EventArgs
		{
			public UserModel LoggedInUser { get; }
			public LoginEventArgs(UserModel user)
			{
				LoggedInUser = user;
			}
		}

		
	}

	
}

