using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;          // <-- needed for CancellationToken
using System.Threading.Tasks;

namespace ADLMRateGen.ADLM.Auth
{
    public sealed class AuthOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:4000/";
        public string ProductKey { get; set; } = "";   // "rategen" | "planswift" | "revit"
        public TimeSpan AccessSkew { get; set; } = TimeSpan.FromSeconds(30);
        public Func<string> DeviceFingerprintProvider { get; set; } // optional
        public int TimeoutMs { get; set; } = 30000;
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

        public AuthClient(AuthOptions options)
        {
            _opt = options;
            _licenseStore = new SecureStore(options.ProductKey + ".license");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        public string GetCachedLicenseToken() => _licenseStore.Load();

        // AuthClient.cs
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


        // BACK-COMPAT: keep the old signature and forward to the new one
        //public Task<bool> LoginAsync(string email, string password, CancellationToken ct = default)
        //    => LoginAsync(identifier: email, password: password, ct);

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

        public void Dispose() { /* nothing */ }

        // ---- HTTP helpers using HttpWebRequest ----
        // AuthClient.cs

        private Task<string> GetJsonAsync(string path, string bearer, CancellationToken ct)
        {
            return Task.Factory.StartNew(() =>
            {
                var url = CombineUrl(_opt.BaseUrl, path);
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = _opt.TimeoutMs;
                req.ReadWriteTimeout = _opt.TimeoutMs;
                req.Accept = "application/json";
                if (!string.IsNullOrEmpty(bearer))
                    req.Headers["Authorization"] = "Bearer " + bearer;

                try
                {
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var s = resp.GetResponseStream())
                    using (var r = new System.IO.StreamReader(s, Encoding.UTF8))
                    {
                        return r.ReadToEnd(); // may be ""
                    }
                }
                catch (WebException wex)
                {
                    var http = wex.Response as HttpWebResponse;
                    if (http != null && (http.StatusCode == HttpStatusCode.NotFound ||
                                         http.StatusCode == HttpStatusCode.NoContent))
                    {
                        return "[]"; // treat as empty list
                    }
                    // bubble other errors
                    throw;
                }
            }, ct);
        }

        public async Task EnsureEntitledAsync(string productKey, CancellationToken ct = default)
        {
            var at = await GetAccessTokenAsync(ct);
            if (string.IsNullOrEmpty(at))
                throw new InvalidOperationException("Not signed in.");

            var resText = await GetJsonAsync("/me/entitlements", at, ct);
            if (string.IsNullOrWhiteSpace(resText))
                resText = "[]"; // be defensive

            JsonElement root;
            try
            {
                root = JsonDocument.Parse(resText).RootElement;
            }
            catch
            {
                // If the server sent something unexpected, do not crash the app
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

        private Task<string> PostJsonAsync(string path, string jsonBody, string bearer, CancellationToken ct)
        {
            return Task.Factory.StartNew(() =>
            {
                var url = CombineUrl(_opt.BaseUrl, path);
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.Timeout = _opt.TimeoutMs;
                req.ReadWriteTimeout = _opt.TimeoutMs;
                req.Accept = "application/json";
                req.ContentType = "application/json; charset=utf-8";
                if (!string.IsNullOrEmpty(bearer))
                    req.Headers["Authorization"] = "Bearer " + bearer;

                var data = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
                req.ContentLength = data.Length;
                using (var rs = req.GetRequestStream())
                    rs.Write(data, 0, data.Length);

                try
                {
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var s = resp.GetResponseStream())
                    using (var r = new System.IO.StreamReader(s, Encoding.UTF8))
                    {
                        return r.ReadToEnd();
                    }
                }
                catch (WebException wex)
                {
                    var httpResp = wex.Response as HttpWebResponse;
                    if (httpResp != null && httpResp.StatusCode == HttpStatusCode.Unauthorized)
                        return null;
                    throw;
                }
            }, ct);
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
