using System;

namespace ADLMRateGen.Helpers
{
    internal static class AppEnvironment
    {
        public static string ApiBaseUrl => Get("ADLM_RATEGEN_API_BASE_URL", "https://adlmweb.onrender.com");

        public static string ProductKey => Get("ADLM_RATEGEN_PRODUCT_KEY", "rategen");

        public static string MongoSrvConnectionString => Get("ADLM_RATEGEN_MONGO_SRV", string.Empty);

        public static string MongoStandardConnectionString => Get("ADLM_RATEGEN_MONGO_STANDARD", string.Empty);

        public static string MongoDatabaseName => Get("ADLM_RATEGEN_MONGO_DATABASE", "ADLMRateDB");

        public static string MongoUsersCollection => Get("ADLM_RATEGEN_MONGO_USERS_COLLECTION", "Users");

        public static string MongoMaterialsCollection => Get("ADLM_RATEGEN_MONGO_MATERIALS_COLLECTION", "Materials");

        public static string MongoLabourCollection => Get("ADLM_RATEGEN_MONGO_LABOUR_COLLECTION", "labours");

        public static string? OfflineLicenseSharedSecret => GetOptional("ADLM_RATEGEN_OFFLINE_LICENSE_SECRET");

        public static string? LocalJwtSecret => GetOptional("ADLM_RATEGEN_LOCAL_JWT_SECRET");

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
