using LetsLearn.Core.Shared;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(Roles = AppRoles.Admin)]
    public class AdminController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ICourseService _courseService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IUserService userService,
            ICourseService courseService,
            ILogger<AdminController> logger)
        {
            _userService = userService;
            _courseService = courseService;
            _logger = logger;
        }

        // ========== DASHBOARD ==========
        [HttpGet("dashboard")]
        public async Task<ActionResult<AdminDashboardDTO>> GetDashboard(CancellationToken ct = default)
        {
            var adminId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            var allUsers = (await _userService.GetAllAsync(adminId)).ToList();
            var allCourses = (await _courseService.GetAllCoursesForAdminAsync(ct)).ToList();

            var dashboard = new AdminDashboardDTO
            {
                TotalUsers = allUsers.Count(),
                TotalStudents = allUsers.Count(u => u.Role == AppRoles.Student),
                TotalTeachers = allUsers.Count(u => u.Role == AppRoles.Teacher),
                TotalAdmins = allUsers.Count(u => u.Role == AppRoles.Admin),
                TotalCourses = allCourses.Count(),
                ActiveCourses = allCourses.Count(c => c.IsPublished),
                RecentUsers = allUsers.Take(5).ToList(),
                RecentCourses = allCourses.Take(5).ToList()
            };

            return Ok(dashboard);
        }

        // ========== USER MANAGEMENT ==========
        [HttpGet("users")]
        public async Task<ActionResult<List<GetUserResponse>>> GetAllUsers(
            [FromQuery] string? role = null,
            [FromQuery] string? search = null,
            CancellationToken ct = default)
        {
            var adminId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            var users = (await _userService.GetAllAsync(adminId)).ToList();

            if (!string.IsNullOrEmpty(role))
            {
                users = users.Where(u => u.Role.Equals(role, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(search))
            {
                users = users.Where(u =>
                    u.Username.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            return Ok(users);
        }

        [HttpGet("users/{id:guid}")]
        public async Task<ActionResult<GetUserResponse>> GetUserById(Guid id, CancellationToken ct = default)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id, ct);
                return Ok(user);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
        }

        [HttpPut("users/{id:guid}/role")]
        public async Task<ActionResult<UpdateUserResponse>> UpdateUserRole(
            Guid id,
            [FromBody] UpdateUserRoleRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Note: Role update requires direct user modification
                // This endpoint is simplified - implement user role update in service layer
                return BadRequest(new { message = "Role update not implemented. Please add UpdateUserRoleAsync to IUserService." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user role");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("users/{id:guid}")]
        public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct = default)
        {
            try
            {
                // Note: Delete user functionality not implemented in service layer
                return BadRequest(new { message = "User deletion not implemented. Please add DeleteAsync to IUserService." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return BadRequest(new { message = ex.Message });
            }
        }

        // ========== COURSE MANAGEMENT ==========
        [HttpGet("courses")]
        public async Task<ActionResult<List<GetCourseResponse>>> GetAllCourses(
            [FromQuery] bool? isPublished = null,
            [FromQuery] string? search = null,
            CancellationToken ct = default)
        {
            var courses = await _courseService.GetAllCoursesForAdminAsync(ct);

            if (isPublished.HasValue)
            {
                courses = courses.Where(c => c.IsPublished == isPublished.Value).ToList();
            }

            if (!string.IsNullOrEmpty(search))
            {
                courses = courses.Where(c =>
                    c.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (c.Description != null && c.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            return Ok(courses);
        }

        [HttpDelete("courses/{id}")]
        public async Task<IActionResult> DeleteCourse(string id, CancellationToken ct = default)
        {
            try
            {
                // Note: Delete course functionality not implemented in service layer
                return BadRequest(new { message = "Course deletion not implemented. Please add DeleteCourseAsync to ICourseService." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Course not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting course");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPatch("courses/{id}/publish")]
        public async Task<ActionResult<GetCourseResponse>> ToggleCoursePublish(
            string id,
            [FromBody] TogglePublishRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Note: Toggle publish functionality not implemented in service layer
                return BadRequest(new { message = "Toggle publish not implemented. Please add TogglePublishAsync to ICourseService." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Course not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling course publish status");
                return BadRequest(new { message = ex.Message });
            }
        }

        // ========== STATISTICS ==========
        [HttpGet("statistics/users")]
        public async Task<ActionResult<UserStatisticsDTO>> GetUserStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            CancellationToken ct = default)
        {
            var adminId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            var users = (await _userService.GetAllAsync(adminId)).ToList();

            var stats = new UserStatisticsDTO
            {
                TotalUsers = users.Count(),
                StudentCount = users.Count(u => u.Role == AppRoles.Student),
                TeacherCount = users.Count(u => u.Role == AppRoles.Teacher),
                AdminCount = users.Count(u => u.Role == AppRoles.Admin),
                NewUsersThisMonth = 0, // CreatedAt not available
                UserGrowthRate = 0.0 // CreatedAt not available
            };

            return Ok(stats);
        }

        [HttpGet("statistics/courses")]
        public async Task<ActionResult<CourseStatisticsDTO>> GetCourseStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            CancellationToken ct = default)
        {
            var courses = (await _courseService.GetAllCoursesForAdminAsync(ct)).ToList();

            var stats = new CourseStatisticsDTO
            {
                TotalCourses = courses.Count(),
                PublishedCourses = courses.Count(c => c.IsPublished),
                UnpublishedCourses = courses.Count(c => !c.IsPublished),
                NewCoursesThisMonth = 0, // CreatedAt not available
                CourseGrowthRate = 0.0 // CreatedAt not available
            };

            return Ok(stats);
        }

        // ========== HELPERS ==========
        // Note: Growth rate calculation requires CreatedAt property on DTOs
    }

    // DTOs
    public class AdminDashboardDTO
    {
        public int TotalUsers { get; set; }
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalCourses { get; set; }
        public int ActiveCourses { get; set; }
        public List<GetUserResponse> RecentUsers { get; set; } = new();
        public List<GetCourseResponse> RecentCourses { get; set; } = new();
    }

    public class UpdateUserRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public class TogglePublishRequest
    {
        public bool IsPublished { get; set; }
    }

    public class UserStatisticsDTO
    {
        public int TotalUsers { get; set; }
        public int StudentCount { get; set; }
        public int TeacherCount { get; set; }
        public int AdminCount { get; set; }
        public int NewUsersThisMonth { get; set; }
        public double UserGrowthRate { get; set; }
    }

    public class CourseStatisticsDTO
    {
        public int TotalCourses { get; set; }
        public int PublishedCourses { get; set; }
        public int UnpublishedCourses { get; set; }
        public int NewCoursesThisMonth { get; set; }
        public double CourseGrowthRate { get; set; }
    }
}
