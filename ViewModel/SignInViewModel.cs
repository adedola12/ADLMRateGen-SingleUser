// ViewModel/SignInViewModel.cs
using ADLMRateGen.ADLM.Auth;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Properties;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;
using System;
using System.Net;
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

        private readonly AuthClient _auth = new AuthClient(
            new AuthOptions
            {
                //BaseUrl = "http://localhost:4000",
                BaseUrl = "https://adlmweb.onrender.com",
                ProductKey = ProductKey,
                DeviceFingerprintProvider = Helpers.DeviceFingerprint.Generate
            }
        );

        private string _email = "";
        private string _password = "";
        private bool _isLoading;

        private static string DeriveUsername(string email) =>
            string.IsNullOrWhiteSpace(email) ? string.Empty : email.Split('@')[0];

        public string Email { get => _email; set { _email = value; RaisePropertyChanged(); } }

        // Legacy binding alias
        public string Username
        {
            get => Email;
            set { Email = value; RaisePropertyChanged(); RaisePropertyChanged(nameof(Email)); }
        }

        public string Password { get => _password; set { _password = value; RaisePropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; RaisePropertyChanged(); } }

        public event EventHandler<LoginEventArgs>? LoginSucceeded;
        public ICommand LoginCommand { get; }

        public SignInViewModel() => LoginCommand = new RelayCommand(async _ => await LoginAsync());
        public SignInViewModel(object? _unused) : this() { }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Please enter your username/email and password.");
                return;
            }

            // ------ OFFLINE PATH ------
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

                    if (string.IsNullOrWhiteSpace(user.Username))
                        user.Username = DeriveUsername(user.Email ?? Email);

                    LoginSucceeded?.Invoke(this, new LoginEventArgs(user));
                    MessageBox.Show("Signed in (offline) via cached license.");
                    return;
                }

                MessageBox.Show("Internet required for first sign-in (no valid offline license found).");
                return;
            }

            // ------ ONLINE PATH ------
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

                // NOTE: Zone-sync prompt removed for now (needs public AuthClient API).
                // We'll reintroduce it once AuthClient exposes a public Get/Put JSON helper.
                // AppSettings.Zone can still be used by other parts of the app if needed.
            }
            catch (UnauthorizedAccessException uae)
            {
                MessageBox.Show(uae.Message, "Access blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (InvalidOperationException ioe)
            {
                MessageBox.Show(ioe.Message, "Request error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {

                MessageBox.Show($"Login Error: {ex.Message}", "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            //catch (WebException ex) when (ex.Status == WebExceptionStatus.Timeout)
            //{
            //    UiErrorHelpers.ShowNetworkTimeoutDialog(
            //        verb: "POST",
            //        path: "/auth/login",
            //        baseUrl: "https://adlmweb.onrender.com/",
            //        timeoutMs: 30000,
            //        ex: ex);
            //}

            finally
            {
                IsLoading = false;
            }

            // After successful login + entitlement
            // 1) read profile (to get zone)
            var profDoc = await _auth.GetJsonAsync("/me/profile");   // uses our new public helper
            var profRoot = profDoc.RootElement;
            var serverZone = profRoot.TryGetProperty("zone", out var zEl) ? (zEl.GetString() ?? "") : "";

            // 2) compare with local zone
            var localZone = ADLMRateGen.Properties.AppSettings.Zone ?? "";
            if (!string.Equals(serverZone, localZone, StringComparison.OrdinalIgnoreCase))
            {
                var resp = MessageBox.Show(
                    string.IsNullOrWhiteSpace(serverZone)
                        ? "Your account has no location zone set. Keep current local prices?"
                        : $"Your account zone is '{serverZone}'. Update your RateGen prices to this zone now?",
                    "Update prices for location",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resp == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(serverZone))
                {
                    // 3) pull master for that zone
                    var masterDoc = await _auth.GetJsonAsync($"/rategen/master?zone={Uri.EscapeDataString(serverZone)}");
                    var root = masterDoc.RootElement;

                    if (root.TryGetProperty("materials", out var mats))
                        ADLMRateGen.Services.DataSourceCloudSync.SaveMaterialsFromDto(mats);

                    if (root.TryGetProperty("labour", out var labs))
                        ADLMRateGen.Services.DataSourceCloudSync.SaveLaboursFromDto(labs);

                    MessageBox.Show("Prices updated for selected zone.");

                }
                else
                {
                    // Keep local, but still remember server zone to avoid nagging
                    ADLMRateGen.Properties.AppSettings.Zone = serverZone;
                }
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
            if (string.IsNullOrWhiteSpace(jwt)) return null;
            if (!JwtLicenseValidator.TryValidateHS256(jwt, HS256_SHARED, out var payload, out _))
                return null;

            var user = new UserModel();

            if (payload.TryGetProperty("sub", out var sub))
                user.Id = sub.GetString();

            if (payload.TryGetProperty("email", out var emailProp))
                user.Email = emailProp.GetString() ?? Email;

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
