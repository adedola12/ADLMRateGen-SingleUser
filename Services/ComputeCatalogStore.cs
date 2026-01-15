using ADLMRateGen.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Disk-backed store for admin-pushed compute items.
    /// Now also supports API refresh -> saves into compute-items.json.
    /// </summary>
    public static class ComputeCatalogStore
    {
        public static event Action? Changed;

        public static IReadOnlyList<ComputeItemDefinition> Items { get; private set; }
            = Array.Empty<ComputeItemDefinition>();

        public static string FilePath =>
            Path.Combine(UserLibrarySync.UserDataFolder, "compute-items.json");

        // ✅ Configure once from MainViewModel (recommended)
        private static string _apiBaseUrl = "https://adlmweb.onrender.com";
        private static string _computeApiPath = "/api/rates/compute-items"; // 🔁 CHANGE THIS if your backend uses a different route

        public static void ConfigureApi(string baseUrl, string? computeApiPath = null)
        {
            if (!string.IsNullOrWhiteSpace(baseUrl))
                _apiBaseUrl = baseUrl.TrimEnd('/');

            if (!string.IsNullOrWhiteSpace(computeApiPath))
                _computeApiPath = computeApiPath.StartsWith("/") ? computeApiPath : "/" + computeApiPath;
        }

        // ✅ last API sync diagnostics (so you can confirm it fired)
        public static DateTime? LastApiSyncUtc { get; private set; }
        public static int LastApiStatusCode { get; private set; }
        public static int LastApiItemCount { get; private set; }
        public static string LastApiMessage { get; private set; } = "";

        /// <summary>
        /// Ensures folder + file exist. If file doesn't exist, creates with "[]".
        /// </summary>
        public static void EnsureStoreExists()
        {
            Directory.CreateDirectory(UserLibrarySync.UserDataFolder);

            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath, "[]");
            }
        }

        public static void ReloadFromDisk()
        {
            try
            {
                EnsureStoreExists();

                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Items = Array.Empty<ComputeItemDefinition>();
                    Changed?.Invoke();
                    return;
                }

                var items = JsonConvert.DeserializeObject<List<ComputeItemDefinition>>(json)
                            ?? new List<ComputeItemDefinition>();

                Items = items;
                Changed?.Invoke();
            }
            catch
            {
                // Keep last known items (best-effort). Still notify so UI can show warnings if it wants.
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Saves items to disk and reloads (fires Changed).
        /// </summary>
        public static void SaveToDisk(IEnumerable<ComputeItemDefinition> items)
        {
            EnsureStoreExists();

            var json = JsonConvert.SerializeObject(items, Formatting.Indented);
            File.WriteAllText(FilePath, json);

            ReloadFromDisk();
        }

        // -------------------- ✅ API refresh --------------------

        public static Task<bool> RefreshFromApiAsync(CancellationToken ct = default)
            => RefreshFromApiInternalAsync(sectionKey: null, ct);

        public static Task<bool> RefreshFromApiAsync(string sectionKey, CancellationToken ct = default)
            => RefreshFromApiInternalAsync(sectionKey, ct);

        private static async Task<bool> RefreshFromApiInternalAsync(string? sectionKey, CancellationToken ct)
        {
            LastApiSyncUtc = null;
            LastApiStatusCode = 0;
            LastApiItemCount = 0;
            LastApiMessage = "";

            // 🔐 Load token/zone from config
            var cfg = ConfigManager.LoadConfig();
            var token = cfg?.AuthToken ?? "";
            var zone = cfg?.Zone ?? "Lagos";

            if (string.IsNullOrWhiteSpace(token))
            {
                LastApiMessage = "Skipped compute API refresh: missing AuthToken (user not logged in).";
                return false;
            }

            // Build URL: /api/compute-items?zone=...&section=...
            var url = $"{_apiBaseUrl}{_computeApiPath}";
            var q = new List<string>();

            if (!string.IsNullOrWhiteSpace(zone))
                q.Add("zone=" + Uri.EscapeDataString(zone));

            if (!string.IsNullOrWhiteSpace(sectionKey))
                q.Add("section=" + Uri.EscapeDataString(sectionKey.Trim()));

            if (q.Count > 0)
                url += "?" + string.Join("&", q);

            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                {
                    http.DefaultRequestHeaders.Accept.Clear();
                    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var resp = await http.GetAsync(url, ct).ConfigureAwait(false);

                    LastApiStatusCode = (int)resp.StatusCode;
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!resp.IsSuccessStatusCode)
                    {
                        LastApiMessage = $"Compute API failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {Trim(body, 250)}";
                        return false;
                    }

                    // Accept both:
                    // 1) [ ... ]
                    // 2) { items: [ ... ] } or { data: [ ... ] }
                    var items = TryParseItems(body);

                    if (items == null)
                    {
                        LastApiMessage = "Compute API returned JSON, but could not parse into compute items list.";
                        return false;
                    }

                    LastApiItemCount = items.Count;
                    LastApiSyncUtc = DateTime.UtcNow;
                    LastApiMessage = $"Compute API OK. Items={items.Count}. Saved to disk.";

                    // Save + reload triggers Changed event
                    SaveToDisk(items);
                    return true;
                }
            }
            catch (TaskCanceledException)
            {
                LastApiMessage = "Compute API timed out or was cancelled.";
                return false;
            }
            catch (Exception ex)
            {
                LastApiMessage = $"Compute API exception: {ex.Message}";
                return false;
            }
        }

        private static List<ComputeItemDefinition>? TryParseItems(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<ComputeItemDefinition>();

            try
            {
                // list format
                var list = JsonConvert.DeserializeObject<List<ComputeItemDefinition>>(json);
                if (list != null) return list;
            }
            catch { }

            try
            {
                var wrap1 = JsonConvert.DeserializeObject<ComputeItemsWrapperItems>(json);
                if (wrap1?.items != null) return wrap1.items;
            }
            catch { }

            try
            {
                var wrap2 = JsonConvert.DeserializeObject<ComputeItemsWrapperData>(json);
                if (wrap2?.data != null) return wrap2.data;
            }
            catch { }

            return null;
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private sealed class ComputeItemsWrapperItems
        {
            public List<ComputeItemDefinition> items { get; set; } = new List<ComputeItemDefinition>();
        }

        private sealed class ComputeItemsWrapperData
        {
            public List<ComputeItemDefinition> data { get; set; } = new List<ComputeItemDefinition>();
        }
    }

    // Keep this schema stable between web admin + desktop app
    public sealed class ComputeItemDefinition
    {
        public string id { get; set; } = "";          // server id or slug
        public string section { get; set; } = "";     // e.g. "Finishes"
        public string name { get; set; } = "";
        public string outputUnit { get; set; } = "m2";
        public decimal poPercent { get; set; } = 0;   // optional uplift at compute-item level
        public bool enabled { get; set; } = true;

        public List<ComputeLine> lines { get; set; } = new List<ComputeLine>();
    }

    public sealed class ComputeLine
    {
        public string kind { get; set; } = "material";   // "material" | "labour" | "constant"
        public int? refSn { get; set; }                  // optional (if you later want SN lookup)
        public string description { get; set; } = "";    // must match library name if using name lookup
        public string unit { get; set; } = "";
        public decimal? unitPriceAtBuild { get; set; }   // used for constant or cached preview

        public decimal qtyPerUnit { get; set; } = 0;
        public decimal factor { get; set; } = 1;
    }
}
