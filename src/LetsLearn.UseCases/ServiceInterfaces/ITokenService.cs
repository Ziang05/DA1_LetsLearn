using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface ITokenService
    {
        string CreateAccessToken(Guid userId, string role);
        string CreateRefreshToken(Guid userId, string role);
        ClaimsPrincipal ValidateToken(string token, bool isAccessToken);
        int GetRefreshTokenExpireSeconds();
        void SetTokenCookies(HttpContext context, string accessToken, string refreshToken);
        string? GetToken(HttpContext context, bool isAccessToken);
        void RemoveAllTokens(HttpContext context);
    }
}
