using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ADLMRateGen.Helpers
{
	internal static class JwtHelper
	{
		/// <summary>Returns the user‐id (NameIdentifier claim) stored in a JWT token.</summary>
		public static string? GetUserId(string jwt)
		{
			if (string.IsNullOrWhiteSpace(jwt)) return null;

			var handler = new JwtSecurityTokenHandler();
			var token = handler.ReadJwtToken(jwt);

			return token.Claims
						.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
						?.Value;
		}
	}
}
