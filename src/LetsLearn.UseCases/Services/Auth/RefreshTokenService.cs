using LetsLearn.Core.Interfaces;
using LetsLearn.Core.Entities;
using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using LetsLearn.Infrastructure.Repository;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.UseCases.Services.Auth
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;

        public RefreshTokenService(ITokenService tokenService, IUnitOfWork unitOfWork)
        {
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Pure repo operations without branching: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<string> CreatRefreshTokenAsync(Guid userId, string role)
        {
            var refreshToken = _tokenService.CreateRefreshToken(userId, role);
            var expirySeconds = int.Parse(_tokenService.GetRefreshTokenExpireSeconds().ToString());
            var tokenEntity = new RefreshToken
            {
                UserId = userId,
                Token = refreshToken,
                ExpiryDate = DateTime.UtcNow.AddSeconds(expirySeconds)
            };

            await ((RefreshTokenRepository)_unitOfWork.RefreshTokens).AddOrUpdateAsync(tokenEntity);
            await _unitOfWork.CommitAsync();

            return refreshToken;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if header missing or invalid (with ||): if +1, logical operator || +1
        // - catch (inner) around ValidateToken: +1
        // - if stored token invalid/expired (with two ||): if +1, two logical operators || +2
        // D = 6 => Minimum Test Cases = D + 1 = 7
        public async Task RefreshTokenAsync(HttpContext context)
        {
            var refreshToken = _tokenService.GetToken(context, isAccessToken: false);
            if (string.IsNullOrEmpty(refreshToken))
                throw new Exception("Refresh token is missing!");

            var userPrincipal = _tokenService.ValidateToken(refreshToken, isAccessToken: false);

            var userId = Guid.Parse(userPrincipal.Claims.First(c => c.Type == "userID").Value);
            var role = userPrincipal.Claims.First(c => c.Type == ClaimTypes.Role).Value;

            var storedToken = await ((RefreshTokenRepository)_unitOfWork.RefreshTokens).GetByUserIdAsync(userId);
            if (storedToken == null || storedToken.Token != refreshToken || storedToken.ExpiryDate < DateTime.UtcNow)
            {
                throw new SecurityTokenException("Invalid or expired refresh token!");
            }

            var newAccessToken = _tokenService.CreateAccessToken(userId, role);
            var newRefreshToken = await CreatRefreshTokenAsync(userId, role);
            _tokenService.SetTokenCookies(context, newAccessToken, newRefreshToken);
        }
    }
}
