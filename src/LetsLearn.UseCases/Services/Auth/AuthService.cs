using LetsLearn.UseCases.DTOs;
using LetsLearn.Core.Interfaces;
using LetsLearn.Core.Entities;
using LetsLearn.Infrastructure.Repository;
using LetsLearn.Core.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.UseCases.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly ITokenService _tokenService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IUnitOfWork _unitOfWork;

        public AuthService(ITokenService tokenService, IRefreshTokenService refreshTokenService, IUnitOfWork unitOfWork)
        {
            _tokenService = tokenService;
            _refreshTokenService = refreshTokenService;
            _unitOfWork = unitOfWork;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if existingUser != null: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task RegisterAsync(SignUpRequest request, HttpContext context)
        {
            var existingUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null)
                throw new InvalidOperationException("Email has been registered!");

            var user = new Core.Entities.User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                Username = request.Username,
                PasswordHash = HashPassword(request.Password),
                Role = request.Role
            };

            await _unitOfWork.Users.AddAsync(user);
            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to register user.", ex);
            }

            var accessToken = _tokenService.CreateAccessToken(user.Id, user.Role);
            var refreshToken = await _refreshTokenService.CreatRefreshTokenAsync(user.Id, user.Role);

            _tokenService.SetTokenCookies(context, accessToken, refreshToken);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if user == null: +1
        // - if !VerifyPassword: +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task<JwtTokenResponse> LoginAsync(AuthRequest request, HttpContext context)
        {
            var user = await ((UserRepository)_unitOfWork.Users).GetByEmailAsync(request.Email);
            if (user == null)
                throw new KeyNotFoundException("Email not found!");

            if (!VerifyPassword(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Incorrect email or password!");

            var accessToken = _tokenService.CreateAccessToken(user.Id, user.Role);
            var refreshToken = await _refreshTokenService.CreatRefreshTokenAsync(user.Id, user.Role);

            _tokenService.SetTokenCookies(context, accessToken, refreshToken);

            return new JwtTokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Delegates to refresh service, no branching here: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task RefreshAsync(HttpContext httpContext)
        {
            await _refreshTokenService.RefreshTokenAsync(httpContext);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if user == null: +1
        // - if !VerifyPassword(old): +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task UpdatePasswordAsync(UpdatePassword request, Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found!");

            if (!VerifyPassword(request.OldPassword, user.PasswordHash))
                throw new UnauthorizedAccessException("Old password is not correct!");

            user.PasswordHash = HashPassword(request.NewPassword);
            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to update password.", ex);
            }
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if userId != Guid.Empty: +1
        // - if storedToken != null: +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public void Logout(HttpContext context)
        {
            _tokenService.RemoveAllTokens(context);
        } 

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
}
