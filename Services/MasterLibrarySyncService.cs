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
            public string State { get; set; } = string.Empty;
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

        /// <summary>
        /// The state key to price against, normalised to something the server
        /// recognises.
        ///
        /// The setting has always been called Zone and has always held a STATE
        /// name: it ships defaulted to "Lagos", which is not one of the six
        /// geopolitical zone keys. So the server's normalizeZone rejected it and
        /// fell back to the account zone, and the value the user could see was
        /// never the value that priced their work. Reading it as a state is what
        /// it always meant.
        /// </summary>
        public static string ResolveState()
            => NigerianStates.Normalize(ResolveZone()) ?? NigerianStates.DefaultKey;

        /// <summary>Persist the chosen state and return its key.</summary>
        public static string SetState(string keyOrLabel)
        {
            var key = NigerianStates.Normalize(keyOrLabel) ?? NigerianStates.DefaultKey;
            var label = NigerianStates.Find(key)?.Label ?? key;

            AppSettings.Zone = label;
            try
            {
                var cfg = ConfigManager.LoadConfig();
                if (cfg != null) { cfg.Zone = label; ConfigManager.SaveConfig(cfg); }
            }
            catch { }
            return key;
        }

        /// <summary>
        /// Adopt a state chosen on the website.
        ///
        /// The rule is "a change on the account wins once". If the account's state
        /// differs from the one this install last saw there, the user changed it on
        /// the website and the app follows. If it matches, the app keeps whatever
        /// was picked here. Comparing against the account's PREVIOUS value rather
        /// than the local one is what stops the two fighting: without it, every
        /// sync would drag the app back to the website's state and the in-app
        /// picker would appear to do nothing.
        /// </summary>
        private static bool ShouldFollowAccount(string accountState, out string key)
        {
            key = NigerianStates.Normalize(accountState);
            if (key == null) return false;

            var lastSeen = NigerianStates.Normalize(AppSettings.LastKnownAccountState);
            AppSettings.LastKnownAccountState = key;

            if (lastSeen == null) return false;   // first sight, do not override a local choice
            if (lastSeen == key) return false;    // account unchanged
            return key != ResolveState();         // changed, and we are not already on it
        }

        public static async Task<SyncResult> SyncAsync(CancellationToken ct = default)
            => await SyncAsync(followAccount: true, ct);

        private static async Task<SyncResult> SyncAsync(bool followAccount, CancellationToken ct)
        {
            var result = new SyncResult { Zone = ResolveZone(), State = ResolveState() };

            var auth = AuthProvider.Instance.Client;
            if (auth == null || !auth.HasSession)
            {
                result.Message = "Sign in to sync master prices.";
                return result;
            }

            // Send both. `state` is what the server prices on now; `zone` is kept so
            // an older server that predates state support still answers correctly
            // rather than falling through to the account default.
            var zoneKey = NigerianStates.ZoneFor(result.State) ?? result.Zone;
            var query = "/rategen/master?state=" + Uri.EscapeDataString(result.State)
                      + "&zone=" + Uri.EscapeDataString(zoneKey);

            using (var doc = await auth.GetJsonAsync(query, ct))
            {
                var root = doc.RootElement;

                // Follow a location the user changed on the website. Checked before
                // anything is written, so the library is only ever saved once, with
                // the right state's prices.
                if (followAccount &&
                    root.TryGetProperty("accountState", out var acct) &&
                    acct.ValueKind == JsonValueKind.String &&
                    ShouldFollowAccount(acct.GetString(), out var followKey))
                {
                    SetState(followKey);
                    return await SyncAsync(followAccount: false, ct);
                }

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
