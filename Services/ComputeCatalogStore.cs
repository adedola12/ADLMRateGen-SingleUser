using ADLMRateGen.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Disk-backed store for compute items (admin/cloud).
    /// Fetches from: /rategen-v2/library/compute-items/sync (paged by cursor).
    /// Saves to: compute-items.json
    /// </summary>
    public static class ComputeCatalogStore
    {
        public static event Action? Changed;

        public static IReadOnlyList<ComputeItemDefinition> Items { get; private set; }
            = Array.Empty<ComputeItemDefinition>();

        public static string FilePath =>
            Path.Combine(UserLibrarySync.UserDataFolder, "compute-items.json");

        // ✅ Configure once from MainViewModel
        private static string _apiBaseUrl = "https://adlmweb.onrender.com";

        // ✅ MUST match server route in rategen.library.js
        // router.get("/library/compute-items/sync", ...)
        private static string _computeSyncPath = "/rategen-v2/library/compute-items/sync";

        public static void ConfigureApi(string baseUrl, string? computeSyncPath = null)
        {
            if (!string.IsNullOrWhiteSpace(baseUrl))
                _apiBaseUrl = baseUrl.TrimEnd('/');

            if (!string.IsNullOrWhiteSpace(computeSyncPath))
                _computeSyncPath = computeSyncPath.StartsWith("/") ? computeSyncPath : "/" + computeSyncPath;
        }

        // diagnostics
        public static DateTime? LastApiSyncUtc { get; private set; }
        public static int LastApiStatusCode { get; private set; }
        public static int LastApiItemCount { get; private set; }
        public static string LastApiMessage { get; private set; } = "";

        private static readonly HttpClient _http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return http;
        }

        public static void EnsureStoreExists()
        {
            Directory.CreateDirectory(UserLibrarySync.UserDataFolder);

            if (!File.Exists(FilePath))
                File.WriteAllText(FilePath, "[]");
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
                // best-effort
                Changed?.Invoke();
            }
        }

        public static void SaveToDisk(IEnumerable<ComputeItemDefinition> items)
        {
            EnsureStoreExists();

            var json = JsonConvert.SerializeObject(items, Formatting.Indented);
            File.WriteAllText(FilePath, json);

            ReloadFromDisk();
        }

        // -------------------- ✅ API refresh --------------------

        public static Task<bool> RefreshFromApiAsync(CancellationToken ct = default)
            => RefreshFromApiInternalAsync(sectionFilter: null, ct: ct);

        public static Task<bool> RefreshFromApiAsync(string sectionFilter, CancellationToken ct = default)
            => RefreshFromApiInternalAsync(sectionFilter, ct);

        private static async Task<bool> RefreshFromApiInternalAsync(string? sectionFilter, CancellationToken ct)
        {
            LastApiSyncUtc = null;
            LastApiStatusCode = 0;
            LastApiItemCount = 0;
            LastApiMessage = "";

            var cfg = ConfigManager.LoadConfig();
            var token = cfg?.AuthToken ?? "";

            if (string.IsNullOrWhiteSpace(token))
            {
                LastApiMessage = "Skipped compute sync: missing AuthToken (not logged in).";
                return false;
            }

            // ✅ Paged sync
            // GET /rategen-v2/library/compute-items/sync?limit=500&cursor=...
            string? cursor = null;
            var all = new List<ComputeItemDefinition>();

            try
            {
                for (int page = 0; page < 50; page++) // safety cap
                {
                    var url = $"{_apiBaseUrl}{_computeSyncPath}?limit=500";
                    if (!string.IsNullOrWhiteSpace(cursor))
                        url += "&cursor=" + Uri.EscapeDataString(cursor);

                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                    LastApiStatusCode = (int)resp.StatusCode;

                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        LastApiMessage = "Compute sync failed: Unauthorized (token expired). Please sign in again.";
                        return false;
                    }

                    if (resp.StatusCode == HttpStatusCode.Forbidden)
                    {
                        LastApiMessage = "Compute sync failed: Forbidden (no entitlement for rategen).";
                        return false;
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        LastApiMessage = $"Compute sync failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {Trim(body, 250)}";
                        return false;
                    }

                    // expected shape:
                    // { ok:true, meta:{...}, items:[...], nextCursor:"..." }
                    var parsed = TryParseSyncResponse(body);
                    if (parsed == null)
                    {
                        LastApiMessage = "Compute sync returned JSON but could not parse (unexpected response shape).";
                        return false;
                    }

                    if (parsed.Items != null && parsed.Items.Count > 0)
                        all.AddRange(parsed.Items);

                    cursor = string.IsNullOrWhiteSpace(parsed.NextCursor) ? null : parsed.NextCursor;

                    if (cursor == null) break; // done
                }

                // optional local filter by section
                if (!string.IsNullOrWhiteSpace(sectionFilter))
                {
                    var f = sectionFilter.Trim();
                    all = all.FindAll(x =>
                        (x.section ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                LastApiItemCount = all.Count;
                LastApiSyncUtc = DateTime.UtcNow;
                LastApiMessage = $"Compute sync OK. Items={all.Count}. Saved to disk.";

                SaveToDisk(all);
                return true;
            }
            catch (TaskCanceledException)
            {
                LastApiMessage = "Compute sync timed out or was cancelled.";
                return false;
            }
            catch (Exception ex)
            {
                LastApiMessage = $"Compute sync exception: {ex.Message}";
                return false;
            }
        }

        private static ComputeSyncResponse? TryParseSyncResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var root = JObject.Parse(json);

                // server returns "items"
                var itemsTok = root["items"];
                var nextCursorTok = root["nextCursor"];

                var items = itemsTok != null
                    ? itemsTok.ToObject<List<ComputeItemDefinition>>()
                    : new List<ComputeItemDefinition>();

                return new ComputeSyncResponse
                {
                    Items = items ?? new List<ComputeItemDefinition>(),
                    NextCursor = nextCursorTok?.ToString()
                };
            }
            catch
            {
                // some servers might return raw array
                try
                {
                    var list = JsonConvert.DeserializeObject<List<ComputeItemDefinition>>(json);
                    if (list != null)
                        return new ComputeSyncResponse { Items = list, NextCursor = null };
                }
                catch { }

                return null;
            }
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private sealed class ComputeSyncResponse
        {
            public List<ComputeItemDefinition> Items { get; set; } = new List<ComputeItemDefinition>();
            public string? NextCursor { get; set; }
        }
    }

    // ✅ matches server toComputeItemDefinition shape (keeps backward compatibility too)
    public sealed class ComputeItemDefinition
    {
        public string id { get; set; } = "";
        public string section { get; set; } = "";
        public string name { get; set; } = "";
        public string outputUnit { get; set; } = "m2";

        // server fields
        public decimal overheadPercentDefault { get; set; } = 10;
        public decimal profitPercentDefault { get; set; } = 25;

        // legacy convenience
        public decimal poPercent { get; set; } = 0;

        public bool enabled { get; set; } = true;
        public string notes { get; set; } = "";
        public DateTime? updatedAt { get; set; }

        public List<ComputeLine> lines { get; set; } = new List<ComputeLine>();
    }

    public sealed class ComputeLine
    {
        public string kind { get; set; } = "material"; // material | labour | constant

        public int? refSn { get; set; }
        public string? refKey { get; set; }
        public string? refName { get; set; }

        // used by your older compute engine
        public string description { get; set; } = "";

        public string unit { get; set; } = "";
        public decimal? unitPriceAtBuild { get; set; }

        public decimal qtyPerUnit { get; set; } = 0;
        public decimal factor { get; set; } = 1;
    }
}
