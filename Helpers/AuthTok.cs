using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using ADLMRateGen.ViewModel.Model;
using Microsoft.IdentityModel.Tokens;
using System.Net.Sockets;

namespace ADLMRateGen.Helpers
{
	public class AuthTok
	{
		private readonly string _secretKey;

		public AuthTok()
		{
			//var secrets = SecretManager.GetSecrets<AppSecrets>();
			_secretKey = "[REDACTED-JWT-KEY]";

			if (string.IsNullOrEmpty(_secretKey) || _secretKey.Length < 32)
			{
				throw new Exception("JWTTokenSecretKey not found or invalid in secrets.json. Key must be at least 32 characters.");
			}
		}

        public string GenerateAuthToken(UserModel user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            // fallbacks so nothing is null
            var id = !string.IsNullOrWhiteSpace(user.Id) ? user.Id! : (user.Email ?? "unknown");

            var email = user.Email ?? string.Empty;
            var username = !string.IsNullOrWhiteSpace(user.Username)
                           ? user.Username!
                           : DeriveUsernameFromEmail(email);   // auto-derive when missing

            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("[REDACTED-JWT-KEY]"));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),                // never null
                new Claim(ClaimTypes.Name,          username ?? string.Empty),
                new Claim(ClaimTypes.Email,         email    ?? string.Empty),
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

            // split letters vs digits at the end
            int i = local.Length - 1;
            while (i >= 0 && char.IsDigit(local[i])) i--;

            var letters = local.Substring(0, i + 1);          // "dolapo"
            var digits = local.Substring(i + 1);             // "836"

            if (digits.Length <= 2) return local;             // nothing or 1–2 digits: keep as-is
            // mask all but last 2 digits
            var masked = new string('*', digits.Length - 2) + digits[^2..]; // "*36" for "836"
            return letters + masked;                           // "dolapo*36"
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
					Id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "",
					Username = principal.FindFirst(ClaimTypes.Name)?.Value ?? "",
					Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? ""
				};
			}
			catch
			{
				// invalid signature, expired, etc.
				return null;
			}
		}
	}
}
