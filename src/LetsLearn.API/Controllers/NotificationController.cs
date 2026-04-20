using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("notification")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _service;

        public NotificationController(INotificationService service)
        {
            _service = service;
        }

        private Guid GetUserId()
        {
            var idStr = User.Claims.First(c => c.Type == "userID").Value;
            return Guid.Parse(idStr);
        }

        // GET: /notification
        [HttpGet]
        public async Task<ActionResult<List<NotificationDto>>> GetNotifications(CancellationToken ct)
        {
            var userId = GetUserId();
            var list = await _service.GetNotificationsAsync(userId, ct);
            return Ok(list);
        }

        // PATCH: /notification/{id}/read
        [HttpPatch("{id:guid}/read")]
        public async Task<ActionResult<NotificationDto>> SetRead(
            Guid id,
            [FromBody] SetReadRequest request,
            CancellationToken ct)
        {
            var dto = await _service.MarkAsReadAsync(id, request.IsRead, ct);
            return Ok(dto);
        }

        // DELETE: /notification/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _service.DeleteNotificationAsync(id, ct);
            return NoContent();
        }

        // POST: /notification
        [HttpPost]
        // [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<NotificationDto>> Create(
            [FromBody] CreateNotificationRequest request,
            CancellationToken ct)
        {
            var dto = await _service.CreateNotificationAsync(request.UserId, request.Title, request.Message, ct);
            return CreatedAtAction(nameof(GetNotifications), new { }, dto);
        }
    }
}

