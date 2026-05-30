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
        private readonly IEmailQueue _emailQueue;
        private readonly IRedisCacheService _redisCacheService;

        public AuthService(
            ITokenService tokenService,
            IRefreshTokenService refreshTokenService,
            IUnitOfWork unitOfWork,
            IEmailQueue emailQueue,
            IRedisCacheService redisCacheService)
        {
            _tokenService = tokenService;
            _refreshTokenService = refreshTokenService;
            _unitOfWork = unitOfWork;
            _emailQueue = emailQueue;
            _redisCacheService = redisCacheService;
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

        public async Task SendForgotPasswordOtpAsync(ForgotPasswordRequest request)
        {
            var user = await ((UserRepository)_unitOfWork.Users).GetByEmailAsync(request.Email);
            if (user == null)
            {
                throw new KeyNotFoundException("Email not found!");
            }

            // Generate a 6-digit OTP code
            var otpCode = GenerateOtpCode(6);

            // Store OTP in Redis (10-minute expiry)
            var cacheKey = $"ForgotPassword:OTP:{request.Email}";
            _redisCacheService.Set(cacheKey, otpCode);

            // Queue custom email containing OTP
            await _emailQueue.QueueEmailAsync(new EmailJob
            {
                Type = EmailType.Custom,
                ToEmail = user.Email,
                Subject = "LetsLearn Password Reset Verification Code",
                Body = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #0A0A0F; color: #FFFFFF; border-radius: 12px;'>
    <h2 style='color: #667EEA; text-align: center;'>Password Reset Code</h2>
    <p>Hi {user.Username},</p>
    <p>We received a request to reset your password. Use the verification code below to proceed with setting a new password:</p>
    <div style='background-color: #12121F; padding: 15px; border-radius: 8px; text-align: center; margin: 20px 0;'>
        <span style='font-size: 28px; font-weight: bold; letter-spacing: 5px; color: #FCD34D;'>{otpCode}</span>
    </div>
    <p>This code will expire in 10 minutes.</p>
    <p>If you did not request a password reset, please ignore this email.</p>
</div>"
            });
        }

        public async Task ResetPasswordWithOtpAsync(ResetPasswordRequest request)
        {
            var user = await ((UserRepository)_unitOfWork.Users).GetByEmailAsync(request.Email);
            if (user == null)
            {
                throw new KeyNotFoundException("Email not found!");
            }

            var cacheKey = $"ForgotPassword:OTP:{request.Email}";
            var storedCode = await _redisCacheService.GetAsync<string>(cacheKey, 5000);

            if (string.IsNullOrEmpty(storedCode) || storedCode != request.Code)
            {
                throw new UnauthorizedAccessException("Invalid or expired verification code!");
            }

            // Update user password
            user.PasswordHash = HashPassword(request.NewPassword);

            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to update password.", ex);
            }

            // Remove the OTP code from Redis
            _redisCacheService.Remove(cacheKey);
        }

        private static string GenerateOtpCode(int length = 6)
        {
            const string validChars = "1234567890";
            var sb = new StringBuilder();
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] uintBuffer = new byte[sizeof(uint)];
                while (sb.Length < length)
                {
                    rng.GetBytes(uintBuffer);
                    uint num = BitConverter.ToUInt32(uintBuffer, 0);
                    sb.Append(validChars[(int)(num % (uint)validChars.Length)]);
                }
            }
            return sb.ToString();
        }
    }
}
