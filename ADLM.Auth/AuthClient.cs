using System;
using System.IO;
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
        public string BaseUrl { get; set; } = "https://adlmweb.onrender.com";
        public string ProductKey { get; set; } = "rategen";
        public TimeSpan AccessSkew { get; set; } = TimeSpan.FromSeconds(30);
        public Func<string>? DeviceFingerprintProvider { get; set; }   // optional
        public int TimeoutMs { get; set; } = 90000;
    }

    /// <summary>Simple DPAPI store for small secrets.</summary>
    internal sealed class SecureStore
    {
        private readonly string _name;
        public SecureStore(string name) => _name = name;

        public void Save(string clear)
        {
            var bytes = Encoding.UTF8.GetBytes(clear ?? "");
            var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Path, enc);
        }

        public string? Load()
        {
            if (!File.Exists(Path)) return null;
            var enc = File.ReadAllBytes(Path);
            var dec = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }

        public void Clear()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }

        private string Dir => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ADLM.Auth");

        private string Path => System.IO.Path.Combine(Dir, _name + ".bin");
    }

    /// <summary>Persist/restore cookies for the refresh flow across restarts.</summary>
    internal sealed class CookieVault
    {
        private readonly SecureStore _store;
        private readonly Uri _baseUri;

        public CookieVault(string name, string baseUrl)
        {
            _store = new SecureStore(name);
            _baseUri = new Uri(FixBaseUrl(baseUrl));
        }

        public void Save(CookieContainer jar)
        {
            var list = new System.Collections.Generic.List<object>();
            foreach (Cookie c in jar.GetCookies(_baseUri))
            {
                list.Add(new
                {
                    c.Name,
                    c.Value,
                    c.Domain,
                    c.Path,
                    c.Secure,
                    c.HttpOnly,
                    Expires = c.Expires == DateTime.MinValue ? (DateTime?)null : c.Expires
                });
            }
            _store.Save(JsonSerializer.Serialize(list));
        }

        public void Restore(CookieContainer jar)
        {
            var raw = _store.Load();
            if (string.IsNullOrWhiteSpace(raw)) return;

            try
            {
                var arr = JsonSerializer.Deserialize<JsonElement>(raw);
                foreach (var el in arr.EnumerateArray())
                {
                    var ck = new Cookie(
                        el.GetProperty("Name").GetString() ?? "",
                        el.GetProperty("Value").GetString() ?? "",
                        el.GetProperty("Path").GetString() ?? "/",
                        NormalizeDomain(el.GetProperty("Domain").GetString(), _baseUri.Host)
                    )
                    {
                        Secure = el.TryGetProperty("Secure", out var s) && s.GetBoolean(),
                        HttpOnly = el.TryGetProperty("HttpOnly", out var h) && h.GetBoolean()
                    };

                    if (el.TryGetProperty("Expires", out var ex) && ex.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(ex.GetString(), out var dt))
                        ck.Expires = dt;

                    jar.Add(_baseUri, ck);
                }
            }
            catch { /* ignore corrupt cookie store */ }
        }

        public void Clear() => _store.Clear();

        private static string NormalizeDomain(string? d, string fallbackHost)
            => string.IsNullOrWhiteSpace(d) ? fallbackHost : d.TrimStart('.');

        private static string FixBaseUrl(string u) => u.EndsWith("/") ? u : (u + "/");
    }

    public sealed class AuthClient : IDisposable
    {
        private readonly AuthOptions _opt;

        // License token is for offline checks
        private readonly SecureStore _licenseStore;

        // Access token + expiry are persisted so we can resume without asking user
        private readonly SecureStore _sessionStore = new SecureStore("session.jwt");
        private readonly CookieContainer _cookies = new CookieContainer();
        private readonly CookieVault _cookieVault;

        private string? _accessToken;
        private DateTimeOffset _accessExpiryUtc;

        public bool HasSession => !string.IsNullOrEmpty(_accessToken);

        private readonly HttpClient _http;

        public AuthClient(AuthOptions options)
        {
            _opt = options;
            _licenseStore = new SecureStore(options.ProductKey + ".license");
            _cookieVault = new CookieVault("refresh.cookies", _opt.BaseUrl);

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            // HttpClient with persistent cookies
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = true,
                CookieContainer = _cookies,
                Proxy = null,
                UseProxy = false
            };

            // Restore cookies from last run
            _cookieVault.Restore(_cookies);

            _http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(_opt.TimeoutMs) };
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            // Restore prior access token if present
            TryLoadSession();
        }

        /* ================ PUBLIC API ================ */

        public string? GetCachedLicenseToken() => _licenseStore.Load();

        public async Task<bool> LoginAsync(string identifier, string password, CancellationToken ct = default)
        {
            var body = new
            {
                identifier,
                password,
                productKey = _opt.ProductKey,
                device_fingerprint = _opt.DeviceFingerprintProvider?.Invoke()
            };

            var resText = await PostJsonAsync("/auth/login", JsonSerializer.Serialize(body), null, ct);
            if (resText == null) return false;

            var root = JsonDocument.Parse(resText).RootElement;

            if (!root.TryGetProperty("accessToken", out var atProp))
                throw new InvalidOperationException("Login failed: accessToken missing in server response.");

            _accessToken = atProp.GetString();
            _accessExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(14);
            SaveSession();

            // persist cookies received during login (refresh cookie)
            _cookieVault.Save(_cookies);

            if (root.TryGetProperty("licenseToken", out var licProp))
            {
                var lic = licProp.GetString();
                if (!string.IsNullOrEmpty(lic)) _licenseStore.Save(lic);
            }

            return true;
        }

        public async Task PingAsync(CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                using var req = new HttpRequestMessage(HttpMethod.Head, CombineUrl(_opt.BaseUrl, "/"));
                await _http.SendAsync(req, cts.Token);
            }
            catch { /* ignore */ }
        }

        public async Task EnsureEntitledAsync(string productKey, CancellationToken ct = default)
        {
            var at = await GetAccessTokenAsync(ct);
            if (string.IsNullOrEmpty(at))
                throw new InvalidOperationException("Not signed in.");

            var raw = await GetJsonRawAsync("/me/entitlements", at, ct) ?? "[]";
            var arr = JsonDocument.Parse(raw).RootElement;

            var ok = false;
            foreach (var e in arr.EnumerateArray())
            {
                var key = e.TryGetProperty("productKey", out var pk) ? (pk.GetString() ?? "") : "";
                if (!key.Equals(productKey, StringComparison.OrdinalIgnoreCase)) continue;

                var statusOk = e.TryGetProperty("status", out var s) &&
                               string.Equals(s.GetString(), "active", StringComparison.OrdinalIgnoreCase);

                var expOk = e.TryGetProperty("expiresAt", out var ex) &&
                            DateTimeOffset.TryParse(ex.GetString(), out var exp) &&
                            exp > DateTimeOffset.UtcNow;

                if (statusOk && expOk) { ok = true; break; }
            }
            if (!ok) throw new UnauthorizedAccessException($"No active subscription for '{productKey}'.");
        }

        public async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct = default)
        {
            var at = await GetAccessTokenAsync(ct);
            if (string.IsNullOrEmpty(at)) throw new InvalidOperationException("Not signed in.");
            var raw = await GetJsonRawAsync(path, at, ct) ?? "{}";
            return JsonDocument.Parse(raw);
        }

        // ADLM.Auth/AuthClient.cs
        public async Task<JsonDocument> PutJsonAsync(string path, object body, CancellationToken ct = default)
        {
            var at = await GetAccessTokenAsync(ct);
            if (string.IsNullOrEmpty(at)) throw new InvalidOperationException("Not signed in.");

            // omit nulls (so baseVersion = null is not sent)
            var json = JsonSerializer.Serialize(body ?? new { }, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var url = CombineUrl(_opt.BaseUrl, path);

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Put, url);
                    req.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", at);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(_opt.TimeoutMs));

                    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    var text = await resp.Content.ReadAsStringAsync(ct);

                    if (resp.StatusCode == HttpStatusCode.Unauthorized)
                        throw new UnauthorizedAccessException("Unauthorized");

                    if (!resp.IsSuccessStatusCode)
                        throw MakeHttpError(resp.StatusCode, text);

                    return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                }
                catch (TaskCanceledException) when (attempt == 1)
                {
                    await Task.Delay(1200, ct);
                }
            }

            throw new TimeoutException("PUT request timed out.");
        }




        public void SignOut()
        {
            _licenseStore.Clear();
            _sessionStore.Clear();
            _cookieVault.Clear();
            _accessToken = null;
            _accessExpiryUtc = default;
        }

        public void Dispose()
        {
            try { _cookieVault.Save(_cookies); } catch { }
            _http.Dispose();
        }

        /* ================ PRIVATE HELPERS ================ */

        private void SaveSession()
        {
            var payload = JsonSerializer.Serialize(new
            {
                access = _accessToken ?? "",
                expiry = _accessExpiryUtc.UtcDateTime
            });
            _sessionStore.Save(payload);
        }

        private void TryLoadSession()
        {
            var raw = _sessionStore.Load();
            if (string.IsNullOrWhiteSpace(raw)) return;

            try
            {
                var root = JsonDocument.Parse(raw).RootElement;
                var tok = root.TryGetProperty("access", out var a) ? a.GetString() : null;
                var expStr = root.TryGetProperty("expiry", out var e) ? e.GetString() : null;

                if (!string.IsNullOrWhiteSpace(tok) &&
                    DateTimeOffset.TryParse(expStr, out var exp) &&
                    exp > DateTimeOffset.UtcNow)
                {
                    _accessToken = tok;
                    _accessExpiryUtc = exp;
                }
            }
            catch { /* ignore corrupt session */ }
        }

        private async Task<string?> GetAccessTokenAsync(CancellationToken ct)
        {
            // 1) still valid?
            if (!string.IsNullOrEmpty(_accessToken) &&
                DateTimeOffset.UtcNow.Add(_opt.AccessSkew) < _accessExpiryUtc)
                return _accessToken;

            // 2) try refresh (uses persisted cookie)
            var resText = await PostJsonAsync("/auth/refresh", "{}", null, ct);
            if (resText == null) return null;

            var root = JsonDocument.Parse(resText).RootElement;
            if (!root.TryGetProperty("accessToken", out var atProp))
                throw new InvalidOperationException("Refresh failed: accessToken missing in server response.");

            _accessToken = atProp.GetString();
            _accessExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(14);
            SaveSession();

            // cookies might rotate; persist jar again
            try { _cookieVault.Save(_cookies); } catch { }

            if (root.TryGetProperty("licenseToken", out var licProp))
            {
                var lic = licProp.GetString();
                if (!string.IsNullOrEmpty(lic)) _licenseStore.Save(lic);
            }

            return _accessToken;
        }

        private async Task<string?> GetJsonRawAsync(string path, string bearer, CancellationToken ct)
        {
            var url = CombineUrl(_opt.BaseUrl, path);
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
                    await Task.Delay(1200, ct);
                }
            }
            throw new TimeoutException("GET request timed out.");
        }

        private async Task<string?> PostJsonAsync(string path, string jsonBody, string? bearer, CancellationToken ct)
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
                        return null; // login/refresh invalid

                    if (resp.StatusCode == HttpStatusCode.Forbidden)
                    {
                        var (msg, code) = ExtractError(text);
                        var what = !string.IsNullOrWhiteSpace(code) ? $"{msg} ({code})" : msg;
                        throw new UnauthorizedAccessException(what);
                    }

                    if (!resp.IsSuccessStatusCode)
                        throw MakeHttpError(resp.StatusCode, text);

                    return text;
                }
                catch (TaskCanceledException) when (attempt == 1)
                {
                    await Task.Delay(1200, ct);
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
            catch { }
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
