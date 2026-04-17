using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using ADLMRateGen.Helpers;
using Microsoft.IdentityModel.Tokens;

namespace ADLMRateGen.ADLM.Auth
{
    public static class JwtLicenseValidator
    {
        // Lazy JWKS fetcher — first RS256 validation triggers the network
        // fetch; subsequent validations hit the in-memory cache.
        private static readonly Lazy<JwksFetcher> _jwks = new Lazy<JwksFetcher>(() =>
            new JwksFetcher(AppEnvironment.ApiBaseUrl, productSlug: "rategen"));

        /// <summary>
        /// Dual-algo validation. Reads the JWT header to see whether to
        /// verify with RS256 (new) or HS256 (legacy). Prefer this over the
        /// sync TryValidateHS256 — once every plugin build shipping in the
        /// field uses this method we can deprecate the symmetric path.
        /// </summary>
        public static async System.Threading.Tasks.Task<(bool ok, JsonElement payload, string error)> TryValidateAsync(string jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt))
                return (false, default, "token is empty");

            string alg, kid;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var parsed = handler.ReadJwtToken(jwt);
                alg = parsed.Header?.Alg ?? "";
                kid = parsed.Header?.Kid ?? "";
            }
            catch (Exception ex)
            {
                return (false, default, "malformed token: " + ex.Message);
            }

            if (string.Equals(alg, "RS256", StringComparison.OrdinalIgnoreCase))
            {
                var key = await _jwks.Value.GetKeyAsync(kid).ConfigureAwait(false);
                if (key == null)
                {
                    await _jwks.Value.RefreshAsync().ConfigureAwait(false);
                    key = await _jwks.Value.GetKeyAsync(kid).ConfigureAwait(false);
                }
                if (key == null)
                    return (false, default, "no JWKS entry for kid " + kid);

                if (TryValidateWithKey(jwt, key, out var payload, out var err))
                    return (true, payload, string.Empty);

                // One more attempt after a forced refresh — handles the
                // narrow window right after a server-side key rotation.
                await _jwks.Value.RefreshAsync().ConfigureAwait(false);
                var fresh = await _jwks.Value.GetKeyAsync(kid).ConfigureAwait(false);
                if (fresh != null && TryValidateWithKey(jwt, fresh, out payload, out err))
                    return (true, payload, string.Empty);

                return (false, default, err);
            }

            if (string.Equals(alg, "HS256", StringComparison.OrdinalIgnoreCase))
            {
                var secret = LicenseSecrets.SharedSecret;
                if (string.IsNullOrEmpty(secret))
                    return (false, default, "HS256 token but no shared secret configured");

                if (TryValidateHS256(jwt, secret!, out var payload, out var err))
                    return (true, payload, string.Empty);
                return (false, default, err);
            }

            return (false, default, "unsupported alg: " + alg);
        }

        private static bool TryValidateWithKey(
            string jwt,
            SecurityKey key,
            out JsonElement payload,
            out string error)
        {
            payload = default;
            error = string.Empty;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var param = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "adlm",
                    ValidateAudience = true,
                    ValidAudience = "adlm-plugin",
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    IssuerSigningKey = key,
                };
                handler.ValidateToken(jwt, param, out var validated);
                var token = (JwtSecurityToken)validated;
                var json = token.Payload.SerializeToJson();
                payload = JsonDocument.Parse(json).RootElement.Clone();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryValidateHS256(string jwt, string sharedSecret, out JsonElement payload, out string error)
        {
            payload = default;
            error = string.Empty;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var param = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(sharedSecret))
                };
                handler.ValidateToken(jwt, param, out var validated);
                var token = (JwtSecurityToken)validated;
                var json = token.Payload.SerializeToJson();
                payload = JsonDocument.Parse(json).RootElement.Clone();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // Used by the online UI (not bound to device)
        public static bool IsEntitled(JsonElement licensePayload, string productKey)
        {
            if (!licensePayload.TryGetProperty("entitlements", out var ent)) return false;
            if (!ent.TryGetProperty(productKey, out var pk)) return false;

            var statusOk = pk.TryGetProperty("status", out var s) &&
                           string.Equals(s.GetString(), "active", StringComparison.OrdinalIgnoreCase);
            var expOk = pk.TryGetProperty("exp", out var ex) &&
                        DateTimeOffset.TryParse(ex.GetString(), out var exp) &&
                        exp > DateTimeOffset.UtcNow;

            return statusOk && expOk;
        }

        // Used by the plugin's offline path - enforces per-product device binding
        public static bool IsEntitledForDevice(JsonElement licensePayload, string productKey, string deviceFingerprint)
        {
            if (!licensePayload.TryGetProperty("entitlements", out var ent)) return false;
            if (!ent.TryGetProperty(productKey, out var pk)) return false;

            var dfpOk = pk.TryGetProperty("dfp", out var dfpEl) &&
                        string.Equals(dfpEl.GetString(), deviceFingerprint, StringComparison.Ordinal);
            if (!dfpOk) return false;

            var statusOk = pk.TryGetProperty("status", out var s) &&
                           string.Equals(s.GetString(), "active", StringComparison.OrdinalIgnoreCase);
            var expOk = pk.TryGetProperty("exp", out var ex) &&
                        DateTimeOffset.TryParse(ex.GetString(), out var exp) &&
                        exp > DateTimeOffset.UtcNow;

            return statusOk && expOk;
        }

        internal static class LicenseSecrets
        {
            public static string? SharedSecret => AppEnvironment.OfflineLicenseSharedSecret;
        }
    }
}
