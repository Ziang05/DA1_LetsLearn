using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LetsLearn.UseCases.Services.Auth
{
    public class TokenService : ITokenService
    {
        private readonly string AccessTokenCookie;
        private readonly string RefreshTokenCookie;
        private readonly string AuthPrefix;
        private readonly int AccessTokenExpireSeconds;
        private readonly int RefreshTokenExpireSeconds;

        private readonly string _issuer;
        private readonly byte[] _secretBytes;

        public TokenService(IConfiguration config)
        {
            _issuer = config["Jwt:Issuer"] ?? throw new ArgumentException("Jwt:Issuer is required");
            var secretKey = config["Jwt:Secret"] ?? throw new ArgumentException("Jwt:Secret is required");
            _secretBytes = Encoding.UTF8.GetBytes(secretKey);
            AccessTokenExpireSeconds = int.Parse(config["Jwt:AccessTokenExpireSeconds"] ?? "3600");
            RefreshTokenExpireSeconds = int.Parse(config["Jwt:RefreshTokenExpireSeconds"] ?? "604800");
            AccessTokenCookie = config["Jwt:AccessTokenCookie"] ?? "ACCESS_TOKEN";
            RefreshTokenCookie = config["Jwt:RefreshTokenCookie"] ?? "REFRESH_TOKEN";
            AuthPrefix = config["Jwt:AuthPrefix"] ?? "Bearer_";
        }

        public string CreateToken(Guid userId, string role, bool isAccessToken)
        {
            var claims = new List<Claim>
            {
                new Claim("userID", userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim("typ", isAccessToken ? "access" : "refresh")
            };

            var credentials = new SigningCredentials(new SymmetricSecurityKey(_secretBytes), SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.AddSeconds(isAccessToken ? AccessTokenExpireSeconds : RefreshTokenExpireSeconds);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                claims: claims,
                expires: expires,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string CreateAccessToken(Guid userId, string role)
            => CreateToken(userId, role, true);

        public string CreateRefreshToken(Guid userId, string role)
            => CreateToken(userId, role, false);

        public ClaimsPrincipal ValidateToken(string token, bool isAccessToken)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = false,
                    IssuerSigningKey = new SymmetricSecurityKey(_secretBytes),
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                return tokenHandler.ValidateToken(token, validationParameters, out _);
            }
            catch (SecurityTokenException)
            {
                throw;
            }
            catch (ArgumentException ex)
            {
                throw new SecurityTokenException("Invalid token.", ex);
            }
            catch (CryptographicException ex)
            {
                throw new SecurityTokenException("Invalid token signature.", ex);
            }
        }

        public int GetRefreshTokenExpireSeconds()
        {
            return RefreshTokenExpireSeconds;
        }

        public void SetTokenCookies(HttpContext context, string accessToken, string refreshToken)
        {
            // Configure cookies for cross-site HTTP requests
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,        // Allow HTTP cookies
                SameSite = SameSiteMode.None,  // More permissive for cross-site HTTP
                Path = "/",
                Domain = null          // Don't restrict domain for cross-site access
            };

            // Set Access Token Cookie
            var accessCookieOptions = new CookieOptions
            {
                HttpOnly = cookieOptions.HttpOnly,
                Secure = cookieOptions.Secure,
                SameSite = cookieOptions.SameSite,
                Path = cookieOptions.Path,
                Domain = cookieOptions.Domain,
                Expires = DateTime.UtcNow.AddSeconds(AccessTokenExpireSeconds)
            };
            context.Response.Cookies.Append(AccessTokenCookie, AuthPrefix + accessToken, accessCookieOptions);

            // Set Refresh Token Cookie
            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = cookieOptions.HttpOnly,
                Secure = cookieOptions.Secure,
                SameSite = cookieOptions.SameSite,
                Path = cookieOptions.Path,
                Domain = cookieOptions.Domain,
                Expires = DateTime.UtcNow.AddSeconds(RefreshTokenExpireSeconds)
            };
            context.Response.Cookies.Append(RefreshTokenCookie, AuthPrefix + refreshToken, refreshCookieOptions);
        }

        public void RemoveAllTokens(HttpContext context)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Domain = null,
                Expires = DateTime.UtcNow.AddDays(-1) // Expire in the past
            };

            context.Response.Cookies.Append(AccessTokenCookie, "", cookieOptions);
            context.Response.Cookies.Append(RefreshTokenCookie, "", cookieOptions);
        }

        public string? GetToken(HttpContext context, bool isAccessToken)
        {
            var cookieName = isAccessToken ? AccessTokenCookie : RefreshTokenCookie;
            if (!context.Request.Cookies.TryGetValue(cookieName, out var cookieValue))
                throw new Exception("No authorization cookie!");

            if (!cookieValue.StartsWith(AuthPrefix))
                throw new Exception("Authorization cookie must start with '" + AuthPrefix + "'.");

            return cookieValue.Substring(AuthPrefix.Length);
        }
    }
}