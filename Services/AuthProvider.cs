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

                // Stable hardware fingerprint (v2). The old MAC-based value moved
                // whenever a VPN/dock/USB adapter changed the fastest active NIC,
                // which is what produced DEVICE_MISMATCH on repeat sign-ins.
                DeviceFingerprintProvider = () =>
                {
                    try { return HardwareFingerprint.Get(); }
                    catch { return string.Empty; }
                },

                // Old v1 value, sent alongside the v2 one so the server can find an
                // existing binding and migrate it instead of rejecting the login.
                LegacyDeviceFingerprintProvider = () =>
                {
                    try { return ADLMRateGen.ADLM.Auth.DeviceFingerprint.Generate(); }
                    catch { return string.Empty; }
                }
            });
        }
    }
}
