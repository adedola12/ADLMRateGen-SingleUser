using ADLMRateGen.Helpers;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ADLMRateGen.Services
{
    public class RateCatalogSyncService
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        public event Action<string> CatalogUpdated;

        public RateCatalogSyncService(HttpClient http, string baseUrl)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
        }

        public async Task CheckAndPromptUpdateAsync(string token, string zone, Func<string, Task<bool>> confirmUpdateAsync)
        {
            var cfg = ConfigManager.LoadConfig() ?? new AppConfig();
            var currentVersion = cfg.LastCatalogVersion;

            var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/rates/manifest?zone={Uri.EscapeDataString(zone)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();
            var manifest = JsonConvert.DeserializeObject<RateCatalogManifest>(json);
            if (manifest == null) return;

            cfg.LastCatalogCheckUtc = DateTime.UtcNow;
            ConfigManager.SaveConfig(cfg);

            if (manifest.CatalogVersion <= currentVersion)
                return;

            var ok = await confirmUpdateAsync($"New rates available: {manifest.Message}\nUpdate now?");
            if (!ok) return;

            await ApplyCatalogAsync(token, zone, manifest.CatalogVersion);

            cfg.LastCatalogVersion = manifest.CatalogVersion;
            ConfigManager.SaveConfig(cfg);

            CatalogUpdated?.Invoke($"Rates updated: {manifest.Message}");
        }

        private async Task ApplyCatalogAsync(string token, string zone, int version)
        {
            await DownloadAndSaveAsync(token, $"{_baseUrl}/api/rates/materials?zone={zone}&version={version}", "materials.json");
            await DownloadAndSaveAsync(token, $"{_baseUrl}/api/rates/labour?zone={zone}&version={version}", "labour.json");

            // ✅ NEW: compute items catalog
            await DownloadAndSaveAsync(token, $"{_baseUrl}/api/rates/compute-items?zone={zone}&version={version}", "compute-items.json");
        }

        private async Task DownloadAndSaveAsync(string token, string url, string fileName)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();

            Directory.CreateDirectory(UserLibrarySync.UserDataFolder);
            var path = Path.Combine(UserLibrarySync.UserDataFolder, fileName);
            File.WriteAllText(path, json);
        }
    }
}
