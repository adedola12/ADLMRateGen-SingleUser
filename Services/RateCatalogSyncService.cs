using ADLMRateGen.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Syncs the "materials" + "labour" libraries from the new v2 endpoints.
    /// Avoids crashing on 404 and provides clear messages.
    /// </summary>
    public class RateCatalogSyncService
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        // v2 routes based on your server rategen.library.js
        private const string META_PATH = "/rategen-v2/library/meta";
        private const string ALL_PATH = "/rategen-v2/library/all";

        public event Action<string>? CatalogUpdated;

        public RateCatalogSyncService(HttpClient http, string baseUrl)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
        }

        public async Task CheckAndPromptUpdateAsync(
            string token,
            string zone,
            Func<string, Task<bool>> confirmUpdateAsync)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;

            var cfg = ConfigManager.LoadConfig() ?? new AppConfig();
            var currentVersion = cfg.LastCatalogVersion; // keep using your existing field

            // 1) read meta
            var metaUrl = $"{_baseUrl}{META_PATH}";
            var metaJson = await GetJsonOrThrowAsync(metaUrl, token);

            var meta = ParseMeta(metaJson);
            if (meta == null)
                return;

            cfg.LastCatalogCheckUtc = DateTime.UtcNow;
            ConfigManager.SaveConfig(cfg);

            // pick a single version to compare (max of materials+labour)
            var latest = Math.Max(meta.MaterialsVersion, meta.LabourVersion);

            if (latest <= currentVersion)
                return;

            var msg = $"New library update available.\n" +
                      $"Materials v{meta.MaterialsVersion}, Labour v{meta.LabourVersion}\n" +
                      $"Update now?";

            var ok = await confirmUpdateAsync(msg);
            if (!ok) return;

            // 2) download all
            // (zone not required by your server snippet, but harmless if present as query)
            var allUrl = $"{_baseUrl}{ALL_PATH}?zone={Uri.EscapeDataString(zone ?? "")}";
            var allJson = await GetJsonOrThrowAsync(allUrl, token);

            var extracted = ExtractAllPayload(allJson);
            if (extracted == null)
                throw new InvalidOperationException("Library download succeeded but could not parse materials/labours.");

            // 3) save to disk (arrays only)
            Directory.CreateDirectory(UserLibrarySync.UserDataFolder);

            File.WriteAllText(Path.Combine(UserLibrarySync.UserDataFolder, "materials.json"),
                extracted.MaterialsJson);

            File.WriteAllText(Path.Combine(UserLibrarySync.UserDataFolder, "labour.json"),
                extracted.LaboursJson);

            // update stored version
            cfg.LastCatalogVersion = latest;
            ConfigManager.SaveConfig(cfg);

            CatalogUpdated?.Invoke($"✅ Library updated (Materials v{meta.MaterialsVersion}, Labour v{meta.LabourVersion})");
        }

        private async Task<string> GetJsonOrThrowAsync(string url, string token)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (res.StatusCode == HttpStatusCode.NotFound)
                throw new HttpRequestException($"404 Not Found: {url}");

            if (res.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException("Unauthorized (token expired). Please sign in again.");

            if (res.StatusCode == HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException("Forbidden (no entitlement).");

            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"{(int)res.StatusCode} {res.ReasonPhrase}: {Trim(body, 200)}");

            return body;
        }

        private static LibraryMeta? ParseMeta(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                var meta = root["meta"];
                if (meta == null) return null;

                var m = meta["materials"];
                var l = meta["labour"];

                return new LibraryMeta
                {
                    MaterialsVersion = (int?)m?["version"] ?? 0,
                    LabourVersion = (int?)l?["version"] ?? 0
                };
            }
            catch
            {
                return null;
            }
        }

        private static AllPayload? ExtractAllPayload(string json)
        {
            try
            {
                var root = JObject.Parse(json);

                // server returns: { ok:true, materials:[...], labours:[...], meta:{...} }
                var mats = root["materials"];
                var labs = root["labours"];

                if (mats == null || labs == null) return null;

                return new AllPayload
                {
                    MaterialsJson = mats.ToString(Formatting.Indented),
                    LaboursJson = labs.ToString(Formatting.Indented)
                };
            }
            catch
            {
                return null;
            }
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private sealed class LibraryMeta
        {
            public int MaterialsVersion { get; set; }
            public int LabourVersion { get; set; }
        }

        private sealed class AllPayload
        {
            public string MaterialsJson { get; set; } = "[]";
            public string LaboursJson { get; set; } = "[]";
        }
    }
}
