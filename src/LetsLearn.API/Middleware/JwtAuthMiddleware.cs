using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace LetsLearn.API.Middleware
{
    public class JwtAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtAuthMiddleware> _logger;
        private readonly string _secret;
        private readonly string _issuer;

        private static readonly string[] _skipPaths =
        {
            "/auth/login",
            "/auth/signup",
            "/auth/refresh",
            "/swagger",
            "/swagger-ui",
            "/v3/api-docs",
            "/ws",
            "/debug"
        };

        public JwtAuthMiddleware(RequestDelegate next, ILogger<JwtAuthMiddleware> logger, IConfiguration config)
        {
            _next = next;
            _logger = logger;
            _secret = config["Jwt:Secret"] ?? "your-super-secret-key";
            _issuer = config["Jwt:Issuer"] ?? "auth0";
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();

            if (_skipPaths.Any(skip => path.StartsWith(skip)))
            {
                await _next(context);
                return;
            }

            var accessTokenCookie = context.Request.Cookies["ACCESS_TOKEN"];
            if (!string.IsNullOrEmpty(accessTokenCookie) && accessTokenCookie.StartsWith("Bearer_"))
            {
                var token = accessTokenCookie.Substring("Bearer_".Length);

                try
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var key = Encoding.ASCII.GetBytes(_secret);
                    tokenHandler.ValidateToken(token, new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidIssuer = _issuer,
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.Zero
                    }, out var validatedToken);

                    var jwtToken = (JwtSecurityToken)validatedToken;
                    //var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
                    var claims = jwtToken.Claims.Select(c =>
                    {
                        if (c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                        {
                            return new Claim(ClaimTypes.Role, c.Value);
                        }
                        return c;
                    });

                    var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
                    context.User = new ClaimsPrincipal(identity);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Invalid token: {ex.Message}");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Invalid Token");
                    return;
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing Token");
                return;
            }

            await _next(context);
        }
    }

    public static class JwtAuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtAuthMiddleware>();
        }
    }
}