using ADLMRateGen.ADLM.Auth;
using ADLMRateGen.Helpers;

namespace ADLMRateGen.Services
{
    /// <summary>Single shared AuthClient for the whole app.</summary>
    public sealed class AuthProvider
    {
        public static AuthProvider Instance { get; } = new AuthProvider();
        public AuthClient Client { get; }

        private AuthProvider()
        {
            Client = new AuthClient(new AuthOptions
            {
                BaseUrl = AppEnvironment.ApiBaseUrl,
                ProductKey = AppEnvironment.ProductKey,
                TimeoutMs = 90000,

                DeviceFingerprintProvider = () =>
                {
                    try { return ADLMRateGen.ADLM.Auth.DeviceFingerprint.Generate(); }
                    catch { return System.Environment.MachineName; }
                }
            });
        }
    }
}
