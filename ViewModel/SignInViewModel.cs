using ADLMRateGen.ADLM.Auth;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Properties;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;
using System;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
    public class SignInViewModel : ViewModelBase
    {
        private const string ProductKey = "rategen";
        private const string HS256_SHARED = "[REDACTED-JWT-LICENSE-SECRET]";

        private AuthClient _auth { get { return AuthProvider.Instance.Client; } }

        private string _email = "";
        private string _password = "";
        private bool _isLoading;

        public event Action<string> ZonePricesApplied;
        public event EventHandler<LoginEventArgs> LoginSucceeded;

        public ICommand LoginCommand { get; }

        public SignInViewModel()
        {
            LoginCommand = new RelayCommand(async _ => await LoginAsync());
        }

        public SignInViewModel(object _) : this() { }

        public string Email
        {
            get { return _email; }
            set { _email = value; RaisePropertyChanged(); }
        }

        public string Password
        {
            get { return _password; }
            set { _password = value; RaisePropertyChanged(); }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            set { _isLoading = value; RaisePropertyChanged(); }
        }

        private static string DeriveUsername(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;
            var parts = email.Split('@');
            return parts.Length > 0 ? parts[0] : email;
        }

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

                    var args = new LoginEventArgs(user) { AccessToken = "" };
                    var ev = LoginSucceeded;
                    if (ev != null) ev(this, args);

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

                var userOnline = new UserModel
                {
                    Email = Email,
                    Username = DeriveUsername(Email)
                };

                // ✅ server-issued token is now stored by AuthClient
                var accessTokenFromServer = _auth.AccessToken;

                var args2 = new LoginEventArgs(userOnline)
                {
                    AccessToken = accessTokenFromServer ?? ""
                };

                var ev2 = LoginSucceeded;
                if (ev2 != null) ev2(this, args2);

                MessageBox.Show("Sign in successful!");

                // Fetch and sync zone-based pricing
                await SyncZonePricesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Login Error: " + ex.Message, "Unexpected error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                using (var profDoc = await _auth.GetJsonAsync("/me/profile"))
                {
                    JsonElement profRoot = profDoc.RootElement;

                    string serverZone = profRoot.TryGetProperty("zone", out var zEl)
                        ? (zEl.GetString() ?? "")
                        : "";

                    if (string.IsNullOrWhiteSpace(serverZone))
                    {
                        MessageBox.Show("Your profile does not have a zone assigned.");
                        return;
                    }

                    string localZone = AppSettings.Zone ?? "";
                    if (!string.Equals(serverZone, localZone, StringComparison.OrdinalIgnoreCase))
                    {
                        var resp = MessageBox.Show(
                            "Your account zone is '" + serverZone + "'. Update your RateGen prices to this zone now?",
                            "Update prices for location",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (resp == MessageBoxResult.Yes)
                        {
                            using (var masterDoc = await _auth.GetJsonAsync("/rategen/master?zone=" + Uri.EscapeDataString(serverZone)))
                            {
                                JsonElement root = masterDoc.RootElement;

                                if (root.TryGetProperty("materials", out var mats))
                                    DataSourceCloudSync.SaveMaterialsFromDto(mats);

                                if (root.TryGetProperty("labour", out var labs))
                                    DataSourceCloudSync.SaveLaboursFromDto(labs);
                            }

                            AppSettings.Zone = serverZone;
                            var ev = ZonePricesApplied;
                            if (ev != null) ev(serverZone);

                            MessageBox.Show("Prices updated for zone: " + serverZone);
                        }
                        else
                        {
                            // Still store the server zone so next sessions are consistent
                            AppSettings.Zone = serverZone;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Zone sync failed: " + ex.Message, "Zone Sync",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool TryOfflineLicense(string jwt)
        {
            JsonElement payload;
            string err;

            if (!JwtLicenseValidator.TryValidateHS256(jwt, HS256_SHARED, out payload, out err))
                return false;

            var dfp = Helpers.DeviceFingerprint.Generate();
            return JwtLicenseValidator.IsEntitledForDevice(payload, ProductKey, dfp);
        }

        private UserModel BuildUserFromLicense(string jwt)
        {
            JsonElement payload;
            string err;

            if (!JwtLicenseValidator.TryValidateHS256(jwt, HS256_SHARED, out payload, out err))
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
            public UserModel LoggedInUser { get; private set; }
            public string AccessToken { get; set; }

            public LoginEventArgs(UserModel user)
            {
                if (user == null) throw new ArgumentNullException(nameof(user));
                LoggedInUser = user;
                AccessToken = "";
            }
        }
    }
}
