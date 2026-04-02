using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace WebApiTokenBasedAuthendication.Controllers
{
    [ApiController]
    [Route("api/Authendication")]
    public class AuthendicationController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, RefreshTokenInfo> RefreshTokens = new();
        private readonly IConfiguration _configuration;

        public AuthendicationController(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public class AuthendicateionRequestBody
        {
            public string? UserName { get; set; }
            public string? Password { get; set; }
        }

        public class RefreshTokenRequestBody
        {
            public string? RefreshToken { get; set; }
        }

        public class TokenResponse
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public DateTime AccessTokenExpiryUtc { get; set; }
            public DateTime RefreshTokenExpiryUtc { get; set; }
        }

        private sealed class RefreshTokenInfo
        {
            public int UserId { get; init; }
            public DateTime ExpiresAtUtc { get; init; }
        }

        private class RequestedUserinfo
        {
            public int UserId { get; set; }
            public string? UserName { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string City { get; set; }

            public RequestedUserinfo(int userId, string? userName, string? firstName, string? lastName, string city)
            {
                UserId = userId;
                UserName = userName;
                FirstName = firstName;
                LastName = lastName;
                City = city;
            }
        }

        [HttpPost("authendicate")]
        public ActionResult<TokenResponse> Authendicate(AuthendicateionRequestBody authendicateionRequestBody)
        {
            var user = ValidateUserCredentials(authendicateionRequestBody.UserName, authendicateionRequestBody.Password);
            if (user == null)
            {
                return Unauthorized();
            }

            return Ok(CreateTokenResponse(user));
        }

        [HttpPost("refresh")]
        public ActionResult<TokenResponse> Refresh(RefreshTokenRequestBody refreshTokenRequestBody)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenRequestBody.RefreshToken))
            {
                return BadRequest("Refresh token is required.");
            }

            if (!RefreshTokens.TryRemove(refreshTokenRequestBody.RefreshToken, out var refreshTokenInfo))
            {
                return Unauthorized("Invalid refresh token.");
            }

            if (refreshTokenInfo.ExpiresAtUtc < DateTime.UtcNow)
            {
                return Unauthorized("Refresh token has expired.");
            }

            var user = GetUserById(refreshTokenInfo.UserId);
            if (user is null)
            {
                return Unauthorized();
            }

            return Ok(CreateTokenResponse(user));
        }

        private TokenResponse CreateTokenResponse(RequestedUserinfo user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_configuration["Authendication:SecretForKey"]));
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var accessTokenExpiryUtc = DateTime.UtcNow.AddMinutes(15);

            var claimsForToken = new List<Claim>
            {
                new("sub", user.UserId.ToString()),
                new("given_name", user.FirstName ?? string.Empty),
                new("family_name", user.LastName ?? string.Empty),
                new("city", user.City)
            };

            var jwtSecurityToken = new JwtSecurityToken(
                _configuration["Authendication:Issuer"],
                _configuration["Authendication:Audience"],
                claimsForToken,
                DateTime.UtcNow,
                accessTokenExpiryUtc,
                signingCredentials);

            var tokenToReturn = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
            var refreshToken = GenerateRefreshToken();
            var refreshTokenExpiryUtc = DateTime.UtcNow.AddDays(7);

            RefreshTokens[refreshToken] = new RefreshTokenInfo
            {
                UserId = user.UserId,
                ExpiresAtUtc = refreshTokenExpiryUtc
            };

            return new TokenResponse
            {
                AccessToken = tokenToReturn,
                RefreshToken = refreshToken,
                AccessTokenExpiryUtc = accessTokenExpiryUtc,
                RefreshTokenExpiryUtc = refreshTokenExpiryUtc
            };
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(randomNumber);
        }

        private RequestedUserinfo? ValidateUserCredentials(string? userName, string? password)
        {
            return new RequestedUserinfo(1, userName ?? "Mr.X", "FirestName", "LastName", "Acity");
        }

        private RequestedUserinfo? GetUserById(int userId)
        {
            if (userId != 1)
            {
                return null;
            }

            return new RequestedUserinfo(1, "Mr.X", "FirestName", "LastName", "Acity");
        }
    }
}
