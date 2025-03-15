using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
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
	}
}
