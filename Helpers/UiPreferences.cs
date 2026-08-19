using System.IO;
using Newtonsoft.Json;

namespace ADLMRateGen.Helpers
{
    /// <summary>
    /// Small view-preferences store. Kept apart from <see cref="AppConfig"/> because
    /// that file is rewritten on sign-in and cleared on sign-out, which would throw
    /// the user's layout choices away every session.
    /// </summary>
    public class UiPreferences
    {
        /// <summary>Welcome banner on the dashboard. Users working through long
        /// rate tables collapse it to get the vertical space back.</summary>
        public bool ShowWelcomeBanner { get; set; } = true;

        private static string FilePath =>
            Path.Combine(AppPaths.UserDataDir, "ui.preferences.json");

        public static UiPreferences Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonConvert.DeserializeObject<UiPreferences>(File.ReadAllText(FilePath))
                           ?? new UiPreferences();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading UI preferences: {ex.Message}");
            }

            return new UiPreferences();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving UI preferences: {ex.Message}");
            }
        }
    }
}
