using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ADLMRateGen.ADLM.Auth
{
    public sealed class AuthOptions
    {
        //public string BaseUrl { get; set; } = "http://localhost:4000/";
        public string BaseUrl { get; set; } = "https://adlmweb.onrender.com/";
        public string ProductKey { get; set; } = "";   // "rategen" | "planswift" | "revit"
        public TimeSpan AccessSkew { get; set; } = TimeSpan.FromSeconds(30);
        public Func<string> DeviceFingerprintProvider { get; set; } // optional
        public int TimeoutMs { get; set; } = 90000; // 90s to tolerate cold starts
    }

    internal sealed class SecureStore
    {
        private readonly string _name;
        public SecureStore(string name) { _name = name; }

        public void Save(string token)
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            System.IO.Directory.CreateDirectory(GetDir());
            System.IO.File.WriteAllBytes(GetPath(), enc);
        }

        public string Load()
        {
            var p = GetPath();
            if (!System.IO.File.Exists(p)) return null;
            var enc = System.IO.File.ReadAllBytes(p);
            var dec = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }

        public void Clear()
        {
            var p = GetPath();
            if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
        }

        private string GetDir()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ADLM.Auth");
        }
        private string GetPath() => System.IO.Path.Combine(GetDir(), _name + ".bin");
    }

    public sealed class AuthClient : IDisposable
    {
        private readonly AuthOptions _opt;
        private readonly SecureStore _licenseStore;
        private string _accessToken;
        private DateTimeOffset _accessExpiryUtc;

        // pooled HttpClient
        private readonly HttpClient _http;

        public AuthClient(AuthOptions options)
        {
            _opt = options;
            _licenseStore = new SecureStore(options.ProductKey + ".license");

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = false,
                Proxy = null,
                UseProxy = false,
            };

            _http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(_opt.TimeoutMs)
            };
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public string GetCachedLicenseToken() => _licenseStore.Load();

        public async Task<bool> LoginAsync(string identifier, string password, CancellationToken ct = default)
        {
            var body = new
            {
                identifier,                   // username or email
                password,
                productKey = _opt.ProductKey,
                device_fingerprint = _opt.DeviceFingerprintProvider != null
                    ? _opt.DeviceFingerprintProvider()
                    : null
            };

            var jsonBody = JsonSerializer.Serialize(body);
            var resText = await PostJsonAsync("/auth/login", jsonBody, null, ct);
            if (resText == null) return false;

            var root = JsonDocument.Parse(resText).RootElement;
            _accessToken = root.GetProperty("accessToken").GetString();
            var licenseToken = root.GetProperty("licenseToken").GetString();
            if (!string.IsNullOrEmpty(licenseToken)) _licenseStore.Save(licenseToken);

            _accessExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(14);
            return true;
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(_accessToken) &&
                DateTimeOffset.UtcNow.Add(_opt.AccessSkew) < _accessExpiryUtc)
                return _accessToken;

            var resText = await PostJsonAsync("/auth/refresh", "{}", null, ct);
            if (resText == null) return null;

            var root = JsonDocument.Parse(resText).RootElement;
            _accessToken = root.GetProperty("accessToken").GetString();
            var licenseToken = root.GetProperty("licenseToken").GetString();
            if (!string.IsNullOrEmpty(licenseToken)) _licenseStore.Save(licenseToken);
            _accessExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(14);
            return _accessToken;
        }

        public void SignOut()
        {
            _licenseStore.Clear();
            _accessToken = null;
        }

        public void Dispose()
        {
            _http?.Dispose();
        }

        /* ---------------- PUBLIC JSON HELPERS ---------------- */

        public async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct = default)
        {
            var at = await GetAccessTokenAsync(ct);
            if (string.IsNullOrEmpty(at)) throw new InvalidOperationException("Not signed in.");
            var raw = await GetJsonAsyncInternal(path, at, ct);
            if (string.IsNullOrWhiteSpace(raw)) raw = "{}";
            return JsonDocument.Parse(raw);
        }

        public async Task<JsonDocument> PutJsonAsync(string path, object body, CancellationToken ct = default)
        {
            var at = await GetAccessTokenAsync(ct);
            if (string.IsNullOrEmpty(at)) throw new InvalidOperationException("Not signed in.");
            var jsonBody = JsonSerializer.Serialize(body ?? new { });
            var raw = await PostJsonAsync(path, jsonBody, at, ct);
            if (string.IsNullOrWhiteSpace(raw)) raw = "{}";
            return JsonDocument.Parse(raw);
        }

        // (Optional) warm-up ping to reduce cold-start timeouts
        public async Task PingAsync(CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                var url = CombineUrl(_opt.BaseUrl, "/");
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                await _http.SendAsync(req, cts.Token);
            }
            catch { /* ignore */ }
        }

        /* ---------------- ENTITLEMENT ---------------- */

        public async Task EnsureEntitledAsync(string productKey, CancellationToken ct = default)
        {
            var at = await GetAccessTokenAsync(ct);
            if (string.IsNullOrEmpty(at))
                throw new InvalidOperationException("Not signed in.");

            var resText = await GetJsonAsyncInternal("/me/entitlements", at, ct);
            if (string.IsNullOrWhiteSpace(resText))
                resText = "[]";

            JsonElement root;
            try
            {
                root = JsonDocument.Parse(resText).RootElement;
            }
            catch
            {
                throw new InvalidOperationException("Entitlements unavailable. Please try again.");
            }

            var ok = false;
            foreach (var e in root.EnumerateArray())
            {
                var key = e.GetProperty("productKey").GetString() ?? "";
                if (!key.Equals(productKey, StringComparison.OrdinalIgnoreCase)) continue;

                var statusOk = e.TryGetProperty("status", out var s) &&
                               string.Equals(s.GetString(), "active", StringComparison.OrdinalIgnoreCase);
                var expOk = e.TryGetProperty("expiresAt", out var ex) &&
                            DateTimeOffset.TryParse(ex.GetString(), out var exp) &&
                            exp > DateTimeOffset.UtcNow;

                if (statusOk && expOk) { ok = true; break; }
            }

            if (!ok)
                throw new UnauthorizedAccessException($"No active subscription for '{productKey}'.");
        }

        /* ---------------- LOW-LEVEL HTTP (HttpClient) ---------------- */

        private async Task<string> GetJsonAsyncInternal(string path, string bearer, CancellationToken ct)
        {
            var url = CombineUrl(_opt.BaseUrl, path);

            // one retry on timeout/cold start
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    if (!string.IsNullOrEmpty(bearer))
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(_opt.TimeoutMs));

                    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    var text = await resp.Content.ReadAsStringAsync(ct);

                    if (resp.StatusCode == HttpStatusCode.NoContent || resp.StatusCode == HttpStatusCode.NotFound)
                        return "[]";

                    if (resp.StatusCode == HttpStatusCode.Unauthorized)
                        throw new UnauthorizedAccessException("Unauthorized");

                    if (!resp.IsSuccessStatusCode)
                        throw MakeHttpError(resp.StatusCode, text);

                    return text;
                }
                catch (TaskCanceledException) when (attempt == 1)
                {
                    // retry once on timeout
                    await Task.Delay(1500, ct);
                    continue;
                }
            }

            throw new TimeoutException("GET request timed out.");
        }

        private async Task<string> PostJsonAsync(string path, string jsonBody, string bearer, CancellationToken ct)
        {
            var url = CombineUrl(_opt.BaseUrl, path);

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, url);
                    if (!string.IsNullOrEmpty(bearer))
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);

                    req.Content = new StringContent(jsonBody ?? "{}", Encoding.UTF8, "application/json");

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(_opt.TimeoutMs));

                    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    var text = await resp.Content.ReadAsStringAsync(ct);

                    if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // keep old behavior: return null for invalid creds
                        return null;
                    }

                    if (resp.StatusCode == HttpStatusCode.Forbidden ||
                        resp.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        var (msg, code) = ExtractError(text);
                        var what = code != null ? $"{msg} ({code})" : msg;
                        throw new UnauthorizedAccessException(what);
                    }

                    if (!resp.IsSuccessStatusCode)
                        throw MakeHttpError(resp.StatusCode, text);

                    return text;
                }
                catch (TaskCanceledException) when (attempt == 1)
                {
                    // retry once on timeout
                    await Task.Delay(1500, ct);
                    continue;
                }
            }

            throw new TimeoutException("POST request timed out.");
        }

        private static Exception MakeHttpError(HttpStatusCode status, string body)
        {
            var (msg, _) = ExtractError(body);
            msg = string.IsNullOrWhiteSpace(msg) ? "Request failed." : msg;
            return new InvalidOperationException($"{(int)status} {status}: {msg}");
        }

        private static (string message, string code) ExtractError(string body)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    string msg = root.TryGetProperty("error", out var e) ? (e.GetString() ?? "") : "";
                    string code = root.TryGetProperty("code", out var c) ? (c.GetString() ?? "") : "";
                    return (msg, code);
                }
            }
            catch { /* ignore non-JSON */ }
            return ("", "");
        }

        private static string CombineUrl(string baseUrl, string path)
        {
            if (string.IsNullOrEmpty(baseUrl)) return path ?? "";
            if (string.IsNullOrEmpty(path)) return baseUrl;
            if (baseUrl.EndsWith("/")) baseUrl = baseUrl.TrimEnd('/');
            if (!path.StartsWith("/")) path = "/" + path;
            return baseUrl + path;
        }
    }
}
