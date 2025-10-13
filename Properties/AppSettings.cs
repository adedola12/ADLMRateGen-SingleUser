// Properties/AppSettings.cs
using System;
using System.IO;
using System.Text.Json;

namespace ADLMRateGen.Properties
{
    /// <summary>
    /// Tiny file-backed settings store (AppData\ADLMRateGen\settings.json).
    /// Only "Zone" for now; easy to extend.
    /// </summary>
    public static class AppSettings
    {
        private static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ADLMRateGen");

        private static readonly string FilePath = Path.Combine(Folder, "settings.json");

        private static SettingsModel _cache = Load();

        public static string? Zone
        {
            get => _cache.Zone;
            set { _cache.Zone = value ?? string.Empty; Save(); }
        }

        private static SettingsModel Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
                }
            }
            catch { /* ignore */ }
            return new SettingsModel();
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { /* ignore */ }
        }

        private class SettingsModel
        {
            public string Zone { get; set; } = string.Empty;
        }
    }
}
