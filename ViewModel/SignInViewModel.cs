using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;
using MongoDB.Driver;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
    public class SignInViewModel : ViewModelBase
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

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Please enter your username and password.");
                IsLoading = false;
                return;
            }

            if (!IsNetworkAvailable())
            {
                MessageBox.Show("An internet connection is required to login. Please check your network and try again.");
                IsLoading = false;
                return;
            }

            try
            {
                // 1) Authenticate
                var user = await _mongoDbService.GetUserAsync(Username, Password);
                if (user == null)
                {
                    MessageBox.Show("Invalid username or password.", "Sign in failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2) Compute current device fingerprint
                string currentFingerprint = DeviceFingerprint.Generate();
                if (string.IsNullOrWhiteSpace(currentFingerprint))
                {
                    MessageBox.Show("Unable to create device fingerprint. Sign-in blocked.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 3) Compare (or set) stored fingerprint
                bool storedMissing = string.IsNullOrWhiteSpace(user.HardwareFingerprint);

                if (!storedMissing)
                {
                    string savedDecrypted;
                    try
                    {
                        savedDecrypted = EncryptionHelper.Decrypt(user.HardwareFingerprint);
                    }
                    catch
                    {
                        MessageBox.Show("Corrupted fingerprint record. Contact support.",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (!string.Equals(savedDecrypted, currentFingerprint, StringComparison.Ordinal))
                    {
                        MessageBox.Show("This device is not authorized for this account (fingerprint mismatch).",
                            "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    // First successful sign-in on this account → persist encrypted fingerprint
                    string encrypted = EncryptionHelper.Encrypt(currentFingerprint);
                    await _mongoDbService.SetHardwareFingerprintAsync(user.Id, encrypted);
                }

                // 4) Success
                LoginSucceeded?.Invoke(this, new LoginEventArgs(user));
                MessageBox.Show("Sign in successful!");
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

        private bool IsNetworkAvailable() => NetworkInterface.GetIsNetworkAvailable();

        public class LoginEventArgs : EventArgs
        {
            public UserModel LoggedInUser { get; }
            public LoginEventArgs(UserModel user) => LoggedInUser = user;
        }
    }
}
