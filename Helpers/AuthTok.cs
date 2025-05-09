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
			//_secretKey = secrets.JWTTokenSecretKey;

			//if (string.IsNullOrEmpty(_secretKey) || _secretKey.Length < 32)
			//{
			//    throw new Exception("JWTTokenSecretKey not found or invalid in secrets.json. Key must be at least 32 characters.");
			//}
		}

		public string GenerateAuthToken(UserModel user)
		{
			var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("[REDACTED-JWT-KEY]"));
			var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

			if (user == null)
			{
				throw new ArgumentNullException(nameof(user), "The user cannot be null.");
			}

			var claims = new[]
			{
				new Claim(ClaimTypes.NameIdentifier, user.Id),
				new Claim(ClaimTypes.Name, user.Username),
				new Claim(ClaimTypes.Email, user.Email),
			};

			var token = new JwtSecurityToken(
				issuer: "ADLMRATEGen",
				audience: "ADLMRATEGen",
				claims: claims,
				expires: DateTime.Now.AddDays(15),
				signingCredentials: credentials);

			return new JwtSecurityTokenHandler().WriteToken(token);
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
