using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace ADLMRateGen.ADLM.Auth
{
    /// <summary>
    /// Fetches and caches the ADLM JWKS so the plugin can verify
    /// RS256-signed license tokens.
    ///
    /// Mirror of the MEP and Planswift implementations — keep in sync
    /// whenever the JWKS shape or transport changes server-side.
    /// </summary>
    internal sealed class JwksFetcher
    {
        private const string DefaultJwksPath = "/.well-known/jwks.json";
        private static readonly HttpClient SharedHttp = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        private readonly string _jwksUrl;
        private readonly string _diskCachePath;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private ConcurrentDictionary<string, RsaSecurityKey> _byKid =
            new ConcurrentDictionary<string, RsaSecurityKey>(StringComparer.Ordinal);

        private bool _loadedFromDisk;

        public JwksFetcher(string? apiBaseUrl, string productSlug)
        {
            var baseUrl = string.IsNullOrWhiteSpace(apiBaseUrl)
                ? "https://adlmweb.onrender.com"
                : apiBaseUrl!.TrimEnd('/');
            _jwksUrl = baseUrl + DefaultJwksPath;

            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ADLM.Auth");
            Directory.CreateDirectory(folder);

            var safeSlug = string.IsNullOrWhiteSpace(productSlug) ? "default" : productSlug;
            _diskCachePath = Path.Combine(folder, safeSlug + ".jwks.json");
        }

        public async Task<RsaSecurityKey?> GetKeyAsync(string kid, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(kid)) return null;

            if (_byKid.TryGetValue(kid, out var key)) return key;

            if (!_loadedFromDisk)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                if (_byKid.TryGetValue(kid, out key)) return key;
            }

            await RefreshAsync(ct).ConfigureAwait(false);
            _byKid.TryGetValue(kid, out key);
            return key;
        }

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                using (var resp = await SharedHttp.GetAsync(_jwksUrl, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode) return;

                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var parsed = ParseJwks(body);
                    if (parsed.Count == 0) return;

                    _byKid = new ConcurrentDictionary<string, RsaSecurityKey>(
                        parsed, StringComparer.Ordinal);

                    try { File.WriteAllText(_diskCachePath, body); }
                    catch { }
                    _loadedFromDisk = true;
                }
            }
            catch
            {
                // Network / disk failure — keep cached keys if any.
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task LoadFromDiskAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_loadedFromDisk) return;
                _loadedFromDisk = true;

                if (!File.Exists(_diskCachePath)) return;
                var body = File.ReadAllText(_diskCachePath);
                var parsed = ParseJwks(body);
                if (parsed.Count == 0) return;
                _byKid = new ConcurrentDictionary<string, RsaSecurityKey>(
                    parsed, StringComparer.Ordinal);
            }
            catch { }
            finally { _gate.Release(); }
        }

        private static Dictionary<string, RsaSecurityKey> ParseJwks(string json)
        {
            var result = new Dictionary<string, RsaSecurityKey>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    if (!doc.RootElement.TryGetProperty("keys", out var keysEl)) return result;
                    if (keysEl.ValueKind != JsonValueKind.Array) return result;

                    foreach (var k in keysEl.EnumerateArray())
                    {
                        if (k.TryGetProperty("kty", out var ktyEl) &&
                            !string.Equals(ktyEl.GetString(), "RSA", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!k.TryGetProperty("n", out var nEl) ||
                            !k.TryGetProperty("e", out var eEl) ||
                            !k.TryGetProperty("kid", out var kidEl))
                            continue;

                        var kid = kidEl.GetString();
                        if (string.IsNullOrWhiteSpace(kid)) continue;

                        var rsa = RSA.Create();
                        var parameters = new RSAParameters
                        {
                            Modulus = Base64UrlDecode(nEl.GetString() ?? string.Empty),
                            Exponent = Base64UrlDecode(eEl.GetString() ?? string.Empty),
                        };
                        rsa.ImportParameters(parameters);

                        var securityKey = new RsaSecurityKey(rsa) { KeyId = kid };
                        result[kid!] = securityKey;
                    }
                }
            }
            catch { }
            return result;
        }

        private static byte[] Base64UrlDecode(string input)
        {
            if (string.IsNullOrEmpty(input)) return new byte[0];
            var s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }
    }
}
