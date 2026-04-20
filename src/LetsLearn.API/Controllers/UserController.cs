using LetsLearn.Core.Shared;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetSelfInformation()
        {
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            var user = await _userService.GetByIdAsync(userId);
            return Ok(user);
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateUserInformation([FromBody] UpdateUserDTO dto)
        {
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            var updated = await _userService.UpdateAsync(userId, dto);
            return Ok(updated);
        }

        [HttpGet("all")]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Teacher}")]
        public async Task<IActionResult> GetAllUsers()
        {
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            var users = await _userService.GetAllAsync(userId);
            return Ok(users);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetUserInformation(Guid id, CancellationToken ct)
        {
            var user = await _userService.GetByIdAsync(id, ct);
            return Ok(user);
        }

        [HttpGet("work")]
        public async Task<ActionResult<List<TopicDTO>>> GetAllWorksOfUser(
            [FromQuery] string? type,
            [FromQuery] String? courseId,
            [FromQuery] DateTime? start,
            [FromQuery] DateTime? end,
            CancellationToken ct = default)
        {
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);

            var result = await _userService.GetAllWorksOfUserAsync(userId, type, start, end, ct);

            return Ok(result);
        }

        [HttpGet("me/report")]
        public async Task<ActionResult<StudentReportDTO>> GetLearnerReport(
            [FromQuery] String courseId,
            [FromQuery] DateTime? start,
            [FromQuery] DateTime? end,
            CancellationToken ct)
        {
            // Lấy userId
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);

            var report = await _userService.GetStudentReportAsync(
                userId,
                courseId,
                start,
                end,
                ct
            );

            return Ok(report);
        }

        [HttpDelete("leave")]
        public async Task<IActionResult> LeaveCourse([FromQuery] string courseId, CancellationToken ct)
        {
            // Lấy userId từ JWT token
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);

            await _userService.LeaveCourseAsync(userId, courseId, ct);

            return NoContent();
        }
    }
}
