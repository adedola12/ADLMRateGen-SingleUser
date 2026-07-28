using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADLMRateGen.Helpers;
using ADLMRateGen.Properties;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Syncs the material/labour libraries from the single source of truth:
    /// the ADLM Cloud master library (/rategen/master, zone-priced from the
    /// RateGen admin database — the same data the website admin edits, QUIV
    /// budget pricing reads, and the ADLM AI Service grounds on).
    ///
    /// Fetches the signed-in user's zone prices and persists them through
    /// DataSourceCloudSync (which merges the user's own rows and refreshes
    /// MaterialLibraryService / LabourLibraryService in place).
    /// </summary>
    public static class MasterLibrarySyncService
    {
        public sealed class SyncResult
        {
            public bool Ok { get; set; }
            public string Zone { get; set; } = string.Empty;
            public int Materials { get; set; }
            public int Labours { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public static string ResolveZone()
        {
            var zone = AppSettings.Zone;
            if (string.IsNullOrWhiteSpace(zone))
            {
                try { zone = ConfigManager.LoadConfig()?.Zone; } catch { }
            }
            return string.IsNullOrWhiteSpace(zone) ? "Lagos" : zone.Trim();
        }

        public static async Task<SyncResult> SyncAsync(CancellationToken ct = default)
        {
            var result = new SyncResult { Zone = ResolveZone() };

            var auth = AuthProvider.Instance.Client;
            if (auth == null || !auth.HasSession)
            {
                result.Message = "Sign in to sync master prices.";
                return result;
            }

            using (var doc = await auth.GetJsonAsync(
                "/rategen/master?zone=" + Uri.EscapeDataString(result.Zone), ct))
            {
                var root = doc.RootElement;

                if (root.TryGetProperty("materials", out var mats) &&
                    mats.ValueKind == JsonValueKind.Array)
                {
                    DataSourceCloudSync.SaveMaterialsFromDto(mats);
                    result.Materials = mats.GetArrayLength();
                }

                JsonElement labs;
                var hasLabs =
                    (root.TryGetProperty("labour", out labs) && labs.ValueKind == JsonValueKind.Array) ||
                    (root.TryGetProperty("labours", out labs) && labs.ValueKind == JsonValueKind.Array);
                if (hasLabs)
                {
                    DataSourceCloudSync.SaveLaboursFromDto(labs);
                    result.Labours = labs.GetArrayLength();
                }
            }

            result.Ok = result.Materials > 0 || result.Labours > 0;
            result.Message = result.Ok
                ? $"Master prices for '{result.Zone}': {result.Materials} materials, {result.Labours} labour rates."
                : $"No master prices returned for zone '{result.Zone}'.";
            return result;
        }
    }
}
