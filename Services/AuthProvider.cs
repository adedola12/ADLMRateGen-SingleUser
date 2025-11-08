using ADLMRateGen.ADLM.Auth;

namespace ADLMRateGen.Services
{
    /// <summary>Single shared AuthClient for the whole app.</summary>
    public sealed class AuthProvider
    {
        private static readonly Lazy<AuthProvider> _lazy = new(() => new AuthProvider());
        public static AuthProvider Instance => _lazy.Value;

        private AuthProvider()
        {
            Client = new AuthClient(new AuthOptions
            {
                BaseUrl = "https://adlmweb.onrender.com",
                ProductKey = "rategen",
                DeviceFingerprintProvider = DeviceFingerprint.Generate,
                TimeoutMs = 90000
            });
        }

        public AuthClient Client { get; private set; }
    }
}
