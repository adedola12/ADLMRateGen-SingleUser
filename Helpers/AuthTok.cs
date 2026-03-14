using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ADLMRateGen.ViewModel.Model;
using Microsoft.IdentityModel.Tokens;

namespace ADLMRateGen.Helpers
{
    public class AuthTok
    {
        private readonly string _secretKey;

        public AuthTok()
        {
            _secretKey = AppEnvironment.LocalJwtSecret
                ?? throw new InvalidOperationException("ADLM_RATEGEN_LOCAL_JWT_SECRET is not configured.");

            if (_secretKey.Length < 32)
            {
                throw new InvalidOperationException("ADLM_RATEGEN_LOCAL_JWT_SECRET must be at least 32 characters.");
            }
        }

        public string GenerateAuthToken(UserModel user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var id = !string.IsNullOrWhiteSpace(user.Id) ? user.Id! : (user.Email ?? "unknown");
            var email = user.Email ?? string.Empty;
            var username = !string.IsNullOrWhiteSpace(user.Username)
                ? user.Username!
                : DeriveUsernameFromEmail(email);

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Email, email),
            };

            var token = new JwtSecurityToken(
                issuer: "ADLMRATEGen",
                audience: "ADLMRATEGen",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(15),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string DeriveUsernameFromEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "user";

            var local = email.Split('@')[0];
            if (string.IsNullOrEmpty(local)) return "user";

            int i = local.Length - 1;
            while (i >= 0 && char.IsDigit(local[i])) i--;

            var letters = local.Substring(0, i + 1);
            var digits = local.Substring(i + 1);

            if (digits.Length <= 2) return local;

            var masked = new string('*', digits.Length - 2) + digits[^2..];
            return letters + masked;
        }

        /* ───────── validate & decode ───────── */
        public UserModel? ValidateToken(string jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt))
                return null;

            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));

            var parms = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = handler.ValidateToken(jwt, parms, out _);

                return new UserModel
                {
                    Id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                    Username = principal.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
                    Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
