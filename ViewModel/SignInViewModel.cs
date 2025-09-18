using ADLMRateGen.ADLM.Auth;
using ADLMRateGen.Command;
using ADLMRateGen.ViewModel.Model;
using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
    public class SignInViewModel : ViewModelBase
    {
        private const string ProductKey = "rategen";

        private readonly AuthClient _auth = new AuthClient(
            new AuthOptions
            {
                BaseUrl = "http://localhost:4000",
                ProductKey = ProductKey,
                DeviceFingerprintProvider = DeviceFingerprint.Generate
            }
        );

        private string _email;
        private string _password;
        private bool _isLoading;

        public string Email { get => _email; set { _email = value; RaisePropertyChanged(); } }

        public string Username
        {
            get => Email;
            set
            {
                Email = value;
                RaisePropertyChanged();              // notifies Username (this property)
                RaisePropertyChanged(nameof(Email)); // if anything else binds to Email
            }
        }

        public string Password { get => _password; set { _password = value; RaisePropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; RaisePropertyChanged(); } }

        public event EventHandler<LoginEventArgs> LoginSucceeded;
        public ICommand LoginCommand { get; }

        public SignInViewModel()
        {
            LoginCommand = new RelayCommand(async _ => await LoginAsync());
        }

        // NEW: satisfy callers that pass one argument (see your compile error)
        public SignInViewModel(object? _unused) : this() { }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Please enter your username/email and password.");
                return;
            }

            // OFFLINE PATH
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                var cached = _auth.GetCachedLicenseToken();
                if (!string.IsNullOrEmpty(cached) && TryOfflineLicense(cached))
                {
                    // Build a minimal UserModel for the rest of the app
                    var user = new UserModel
                    {
                        Email = Email,
                        Username = Email,    // or derive a nicer display name if you have one
                    };

                    LoginSucceeded?.Invoke(this, new LoginEventArgs(user));
                    MessageBox.Show("Signed in (offline) via cached license.");
                    return;
                }

                MessageBox.Show("Internet required for first sign-in (no valid offline license found).");
                return;
            }

            // ONLINE PATH
            IsLoading = true;
            try
            {
                // Tell the auth client what to send
                var ok = await _auth.LoginAsync(identifier: Email, password: Password); // see overload below
                if (!ok)
                {
                    MessageBox.Show("Invalid username/email or password.", "Sign in failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await _auth.EnsureEntitledAsync(ProductKey);

                var user = new UserModel { Email = Email, Username = Email }; // UI banner; real username comes from server if you fetch profile
                LoginSucceeded?.Invoke(this, new LoginEventArgs(user));
                MessageBox.Show("Sign in successful!");
            }
            catch (UnauthorizedAccessException uae)
            {
                MessageBox.Show(uae.Message, "Subscription required", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        private bool TryOfflineLicense(string jwt)
        {
            const string HsSharedSecret = "[REDACTED-JWT-LICENSE-SECRET]"; // must match server JWT_LICENSE_SECRET
            if (!JwtLicenseValidator.TryValidateHS256(jwt, HsSharedSecret, out var payload, out _))
                return false;

            // IMPORTANT: validate the device-bound entitlement for rategen
            var dfp = DeviceFingerprint.Generate();
            return JwtLicenseValidator.IsEntitledForDevice(payload, ProductKey, dfp);
        }

        // UPDATED event args to expose LoggedInUser
        public class LoginEventArgs : EventArgs
        {
            public UserModel LoggedInUser { get; }
            public LoginEventArgs(UserModel user) => LoggedInUser = user;
        }
    }
}
