using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Helpers
{
    internal static class JwtHelper
    {
        /// <summary>Returns the user-id stored in a JWT token.</summary>
        public static string? GetUserId(string jwt)
        {
            return TryReadUser(jwt)?.Id;
        }

        public static UserModel? TryReadUser(string jwt)
        {
            if (!TryReadToken(jwt, out var token))
            {
                return null;
            }

            var email = GetClaim(token,
                ClaimTypes.Email,
                "email");

            var username = GetClaim(token,
                ClaimTypes.Name,
                "unique_name",
                "username");

            var id = GetClaim(token,
                ClaimTypes.NameIdentifier,
                "sub");

            var avatarUrl = GetClaim(token,
                "avatarUrl",
                "avatar_url",
                "picture");

            var firstName = GetClaim(token,
                ClaimTypes.GivenName,
                "given_name",
                "firstName",
                "first_name");

            var lastName = GetClaim(token,
                ClaimTypes.Surname,
                "family_name",
                "lastName",
                "last_name");

            var zone = GetClaim(token,
                "zone");

            if (string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(email))
            {
                username = email.Split('@')[0];
            }

            if (string.IsNullOrWhiteSpace(id) &&
                string.IsNullOrWhiteSpace(email) &&
                string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            return new UserModel
            {
                Id = id ?? string.Empty,
                Email = email ?? string.Empty,
                Username = username ?? string.Empty,
                AvatarUrl = avatarUrl ?? string.Empty,
                FirstName = firstName ?? string.Empty,
                LastName = lastName ?? string.Empty,
                Zone = zone ?? string.Empty
            };
        }

        public static DateTime? TryGetExpiryLocal(string jwt)
        {
            if (!TryReadToken(jwt, out var token))
            {
                return null;
            }

            var exp = token.Payload.Expiration;
            if (exp.HasValue)
            {
                return DateTimeOffset.FromUnixTimeSeconds(exp.Value).ToLocalTime().DateTime;
            }

            if (token.ValidTo > DateTime.MinValue)
            {
                return DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc).ToLocalTime();
            }

            return null;
        }

        private static bool TryReadToken(string jwt, out JwtSecurityToken token)
        {
            token = null!;

            if (string.IsNullOrWhiteSpace(jwt))
            {
                return false;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                token = handler.ReadJwtToken(jwt);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string? GetClaim(JwtSecurityToken token, params string[] claimTypes)
        {
            return token.Claims
                .FirstOrDefault(c => claimTypes.Contains(c.Type, StringComparer.OrdinalIgnoreCase))
                ?.Value;
        }
    }
}
