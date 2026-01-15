using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public sealed class ComputeCatalogSyncService
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        /// <param name="baseUrl">Example: https://your-domain.com (no trailing slash)</param>
        public ComputeCatalogSyncService(HttpClient http, string baseUrl)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _baseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
        }

        /// <summary>
        /// Downloads compute item definitions from API and stores to compute-items.json
        /// </summary>
        public async Task SyncAsync(CancellationToken ct = default)
        {
            // You can change this to your real endpoint.
            // Recommended: GET /api/rates/compute-items  -> returns JSON array of ComputeItemDefinition
            var url = $"{_baseUrl}/api/rates/compute-items";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);

            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            var items = JsonConvert.DeserializeObject<List<ComputeItemDefinition>>(json)
                        ?? new List<ComputeItemDefinition>();

            ComputeCatalogStore.SaveToDisk(items);
        }
    }
}
