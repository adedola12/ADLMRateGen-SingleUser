using System;

namespace ADLMRateGen.Helpers
{
    internal static class AppEnvironment
    {
        public const string DefaultApiBaseUrl = "https://api.adlmstudio.net";

        public const string DefaultAiServiceUrl =
            "https://rced0gx4rj.execute-api.us-east-1.amazonaws.com";

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

        /// <summary>
        /// ADLM AI Service endpoint — the one thing that decides whether the
        /// "Build with AI" panel exists.
        ///
        /// It has a default, and must keep one. The endpoint is configuration,
        /// not a secret: it is the same public API Gateway host every ADLM
        /// product calls, and it refuses any request that arrives without a
        /// licence token. Resolving it from ADLM_AI_URL alone meant it resolved
        /// to nothing on an installed machine — nothing writes that variable, so
        /// the only box where the panel appeared was the development one that had
        /// it exported by hand, and every install hid the feature it had just
        /// shipped.
        ///
        /// Order: the product-specific override, then the fleet-wide variable,
        /// then the current default. Setting either to "off" (or none/disabled/
        /// false/0) disables the feature deliberately and hides the panel, which
        /// is now the only way it can disappear.
        ///
        /// The token normally comes from the signed-in licence token;
        /// ADLM_AI_TOKEN is a dev/test override.
        /// </summary>
        public static string? AiServiceUrl
        {
            get
            {
                var configured = GetOptional("ADLM_RATEGEN_AI_URL") ?? GetOptional("ADLM_AI_URL");
                if (configured == null) return DefaultAiServiceUrl;
                return IsOff(configured) ? null : configured.TrimEnd('/');
            }
        }

        public static string? AiServiceTokenOverride => GetOptional("ADLM_AI_TOKEN");

        public static string? LocalJwtSecret => GetOptional("ADLM_RATEGEN_LOCAL_JWT_SECRET");

        /// <summary>
        /// Delete any user environment variable still pointing at the retired
        /// Render host.
        ///
        /// FirstLive already ignores those values, so nothing depends on this to
        /// work today. The difference is that ignoring is permanent: the stale
        /// variable stays on the machine, and the guard above has to live forever
        /// to keep stepping over it. Removing it fixes the cause, so once enough
        /// installs have run this, the guard can go.
        ///
        /// Deletes rather than corrects, because the right value is not this
        /// method's to decide: with the variable gone, ApiBaseUrl falls through to
        /// the current default, and the InstallerHub rewrites the fleet-wide one
        /// when it next needs to.
        ///
        /// User scope only. A machine-scope value would need administrator rights,
        /// and a normal launch does not have them.
        /// </summary>
        public static void RemoveRetiredHostOverrides()
        {
            foreach (var name in new[] { "ADLM_RATEGEN_API_BASE_URL", "ADLM_API_BASE_URL" })
            {
                try
                {
                    var value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    if (value.IndexOf(RetiredApiHost, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.User);

                    // The process copy was inherited at launch and is not affected
                    // by the change above, so clear it too. Without this the app
                    // would keep reading the stale value until it restarted.
                    Environment.SetEnvironmentVariable(name, null);

                    System.Diagnostics.Debug.WriteLine(
                        $"[AppEnvironment] Removed {name}, which pointed at the retired host {RetiredApiHost}.");
                }
                catch (System.Security.SecurityException)
                {
                    // No permission to write user environment. The guard in
                    // FirstLive still protects this launch, so carry on.
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppEnvironment] Could not clean {name}: {ex.Message}");
                }
            }
        }

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

        /// <summary>A value that asks for a feature to be off, rather than a URL.</summary>
        private static bool IsOff(string value) =>
            value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("disabled", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("0", StringComparison.Ordinal);

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
