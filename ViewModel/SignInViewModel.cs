using ADLMRateGen.ADLM.Auth;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Properties;
using ADLMRateGen.Services;
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
        private const string HS256_SHARED = "[REDACTED-JWT-LICENSE-SECRET]";

        //private readonly AuthClient _auth = new AuthClient(
        //    new AuthOptions
        //    {
        //        BaseUrl = "https://adlmweb.onrender.com",
        //        ProductKey = ProductKey,
        //        DeviceFingerprintProvider = Helpers.DeviceFingerprint.Generate
        //    }
        //);

        private AuthClient _auth => AuthProvider.Instance.Client;

        private string _email = "";
        private string _password = "";
        private bool _isLoading;

        public event Action<string>? ZonePricesApplied;
        public event EventHandler<LoginEventArgs>? LoginSucceeded;

        public ICommand LoginCommand { get; }

        public SignInViewModel() => LoginCommand = new RelayCommand(async _ => await LoginAsync());
        public SignInViewModel(object? _) : this() { }

        public string Email { get => _email; set { _email = value; RaisePropertyChanged(); } }
        public string Password { get => _password; set { _password = value; RaisePropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; RaisePropertyChanged(); } }

        private static string DeriveUsername(string email) =>
            string.IsNullOrWhiteSpace(email) ? string.Empty : email.Split('@')[0];

        

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Please enter your username/email and password.");
                return;
            }

            // Offline login
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                var cached = _auth.GetCachedLicenseToken();
                if (!string.IsNullOrEmpty(cached) && TryOfflineLicense(cached))
                {
                    var user = BuildUserFromLicense(cached) ?? new UserModel
                    {
                        Email = Email,
                        Username = DeriveUsername(Email)
                    };
                    LoginSucceeded?.Invoke(this, new LoginEventArgs(user));
                    MessageBox.Show("Signed in (offline) via cached license.");
                    return;
                }

                MessageBox.Show("Internet required for first sign-in (no valid offline license found).");
                return;
            }

            // Online login
            IsLoading = true;
            try
            {
                await _auth.PingAsync();
                var ok = await _auth.LoginAsync(identifier: Email, password: Password);
                if (!ok)
                {
                    MessageBox.Show("Invalid username/email or password.", "Sign in failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await _auth.EnsureEntitledAsync(ProductKey);

                var user = new UserModel
                {
                    Email = Email,
                    Username = DeriveUsername(Email)
                };

                LoginSucceeded?.Invoke(this, new LoginEventArgs(user));
                MessageBox.Show("Sign in successful!");

                // Fetch and sync zone-based pricing
                await SyncZonePricesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login Error: {ex.Message}", "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SyncZonePricesAsync()
        {
            try
            {
                var profDoc = await _auth.GetJsonAsync("/me/profile");
                var profRoot = profDoc.RootElement;
                var serverZone = profRoot.TryGetProperty("zone", out var zEl)
                    ? (zEl.GetString() ?? "")
                    : "";

                if (string.IsNullOrWhiteSpace(serverZone))
                {
                    MessageBox.Show("Your profile does not have a zone assigned.");
                    return;
                }

                var localZone = AppSettings.Zone ?? "";
                if (!string.Equals(serverZone, localZone, StringComparison.OrdinalIgnoreCase))
                {
                    var resp = MessageBox.Show(
                        $"Your account zone is '{serverZone}'. Update your RateGen prices to this zone now?",
                        "Update prices for location",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (resp == MessageBoxResult.Yes)
                    {
                        var masterDoc = await _auth.GetJsonAsync($"/rategen/master?zone={Uri.EscapeDataString(serverZone)}");
                        var root = masterDoc.RootElement;

                        if (root.TryGetProperty("materials", out var mats))
                            DataSourceCloudSync.SaveMaterialsFromDto(mats);

                        if (root.TryGetProperty("labour", out var labs))
                            DataSourceCloudSync.SaveLaboursFromDto(labs);

                        AppSettings.Zone = serverZone;
                        ZonePricesApplied?.Invoke(serverZone);

                        MessageBox.Show($"Prices updated for zone: {serverZone}");
                    }
                    else
                    {
                        AppSettings.Zone = serverZone;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Zone sync failed: {ex.Message}", "Zone Sync", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool TryOfflineLicense(string jwt)
        {
            if (!JwtLicenseValidator.TryValidateHS256(jwt, HS256_SHARED, out var payload, out _))
                return false;

            var dfp = Helpers.DeviceFingerprint.Generate();
            return JwtLicenseValidator.IsEntitledForDevice(payload, ProductKey, dfp);
        }

        private UserModel? BuildUserFromLicense(string jwt)
        {
            if (!JwtLicenseValidator.TryValidateHS256(jwt, HS256_SHARED, out var payload, out _))
                return null;

            var user = new UserModel();

            if (payload.TryGetProperty("email", out var emailProp))
                user.Email = emailProp.GetString();

            if (payload.TryGetProperty("username", out var unProp))
                user.Username = unProp.GetString();

            if (string.IsNullOrWhiteSpace(user.Username))
                user.Username = DeriveUsername(user.Email ?? Email);

            return user;
        }

        public class LoginEventArgs : EventArgs
        {
            public UserModel LoggedInUser { get; }
            public LoginEventArgs(UserModel user) => LoggedInUser = user;
        }
    }
}
