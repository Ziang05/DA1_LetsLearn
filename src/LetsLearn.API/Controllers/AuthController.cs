using System;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LetsLearn.UseCases.Services.Auth;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("auth")]

    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Register([FromBody] SignUpRequest request)
        {
            try
            {
                await _authService.RegisterAsync(request, HttpContext);
                return Ok(new { message = "Successfully registered" });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Email has been registered"))
            {
                return Conflict(new { message = "Email already in use" });
            }
            catch (InvalidOperationException)
            {
                return StatusCode(500, new { message = "Something went wrong. Please try again later." });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request, HttpContext);
                return Ok(new { message = "Login successful", data = response });
            }
            catch (KeyNotFoundException)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }
            catch
            {
                return StatusCode(500, new { message = "Something went wrong. Please try again later." });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            try
            {
                await _authService.RefreshAsync(HttpContext);
                return Ok(new { message = "Access token refreshed" });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            try
            {
                _authService.Logout(HttpContext);
                return Ok(new { message = "Logged out successfully" });
            }
            catch
            {
                return StatusCode(500, new { message = "Something went wrong. Please try again later." });
            }
        }

        [HttpPatch("me/password")]
        [Authorize]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePassword request)
        {
            try
            {
                var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
                await _authService.UpdatePasswordAsync(request, userId);
                return Ok(new { message = "Password updated successfully" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Current password is incorrect" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch
            {
                return StatusCode(500, new { message = "Something went wrong. Please try again later." });
            }
        }
    }
}
