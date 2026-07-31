using System;

namespace ADLMRateGen.Helpers
{
    internal static class AppEnvironment
    {
        public const string DefaultApiBaseUrl = "https://api.adlmstudio.net";

        private const string RetiredApiHost = "adlmweb.onrender.com";

        /// <summary>
        /// API host, resolved from configuration rather than compiled in.
        ///
        /// Order: the product-specific override, then the fleet-wide variable
        /// the InstallerHub writes, then the current default. A value still
        /// pointing at the retired Render host is skipped rather than honoured:
        /// older RateGen installers wrote that host into HKCU\Environment, so on
        /// exactly the machines that are broken a stale variable would otherwise
        /// outrank the new default and the update would change nothing.
        /// </summary>
        public static string ApiBaseUrl =>
            (FirstLive("ADLM_RATEGEN_API_BASE_URL", "ADLM_API_BASE_URL") ?? DefaultApiBaseUrl)
                .TrimEnd('/');

        public static string ProductKey => Get("ADLM_RATEGEN_PRODUCT_KEY", "rategen");

        public static string MongoSrvConnectionString => Get("ADLM_RATEGEN_MONGO_SRV", string.Empty);

        public static string MongoStandardConnectionString => Get("ADLM_RATEGEN_MONGO_STANDARD", string.Empty);

        public static string MongoDatabaseName => Get("ADLM_RATEGEN_MONGO_DATABASE", "ADLMRateDB");

        public static string MongoUsersCollection => Get("ADLM_RATEGEN_MONGO_USERS_COLLECTION", "Users");

        public static string MongoMaterialsCollection => Get("ADLM_RATEGEN_MONGO_MATERIALS_COLLECTION", "Materials");

        public static string MongoLabourCollection => Get("ADLM_RATEGEN_MONGO_LABOUR_COLLECTION", "labours");

        public static string? OfflineLicenseSharedSecret => GetOptional("ADLM_RATEGEN_OFFLINE_LICENSE_SECRET");

        // ADLM AI Service. No default URL — AI features stay hidden until the
        // operator configures the endpoint. Token normally comes from the
        // signed-in licence token; ADLM_AI_TOKEN is a dev/test override.
        public static string? AiServiceUrl => GetOptional("ADLM_AI_URL");

        public static string? AiServiceTokenOverride => GetOptional("ADLM_AI_TOKEN");

        public static string? LocalJwtSecret => GetOptional("ADLM_RATEGEN_LOCAL_JWT_SECRET");

        /// <summary>
        /// First variable that is set and does not name the retired host.
        /// </summary>
        private static string? FirstLive(params string[] names)
        {
            foreach (var name in names)
            {
                var value = GetOptional(name);
                if (value == null) continue;
                if (value.IndexOf(RetiredApiHost, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return value;
            }

            return null;
        }

        private static string Get(string name, string fallback)
        {
            return GetOptional(name) ?? fallback;
        }

        private static string? GetOptional(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
