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
