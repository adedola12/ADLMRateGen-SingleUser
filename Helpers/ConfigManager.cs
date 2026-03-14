using System;
using System.IO;
using ADLMRateGen.Properties;
using Newtonsoft.Json;

namespace ADLMRateGen.Helpers
{
    public static class ConfigManager
    {
        private static readonly string ConfigFilePath = Path.Combine(AppPaths.UserDataDir, "app.config.json");
        private static readonly string LegacyConfigFilePath = Path.Combine(AppContext.BaseDirectory, "app.config.json");

        public static AppConfig LoadConfig()
        {
            TryMigrateLegacyConfig();

            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    return Normalize(JsonConvert.DeserializeObject<AppConfig>(json));
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error loading config: {ex.Message}");
                }
            }

            return Normalize(new AppConfig());
        }

        public static void SaveConfig(AppConfig config)
        {
            try
            {
                var normalized = Normalize(config);
                string json = JsonConvert.SerializeObject(normalized, Formatting.Indented);
                AppPaths.AtomicWrite(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        /* ───────── CLEAR ───────── */
        /// <summary>
        /// Deletes the persisted configuration file (if present) and
        /// optionally returns a fresh, empty AppConfig instance.
        /// </summary>
        public static AppConfig ClearConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    File.Delete(ConfigFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing config: {ex.Message}");
            }

            return Normalize(new AppConfig());
        }

        private static void TryMigrateLegacyConfig()
        {
            try
            {
                if (!File.Exists(LegacyConfigFilePath) || File.Exists(ConfigFilePath))
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath)!);
                File.Copy(LegacyConfigFilePath, ConfigFilePath, overwrite: false);
            }
            catch
            {
                // Best-effort migration from the legacy install folder.
            }
        }

        private static AppConfig Normalize(AppConfig? config)
        {
            var normalized = config ?? new AppConfig();
            var zone = !string.IsNullOrWhiteSpace(AppSettings.Zone)
                ? AppSettings.Zone
                : normalized.Zone;

            normalized.Zone = string.IsNullOrWhiteSpace(zone) ? "Lagos" : zone;
            AppSettings.Zone = normalized.Zone;

            return normalized;
        }
    }
}
