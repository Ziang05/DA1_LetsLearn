using LetsLearn.API.Middleware;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LetsLearn.API.Hubs
{
    // ─── Contract (shared interface between FE & BE) ──────────────────────────
    // FE sẽ nhận sự kiện "ReceiveMessage" với payload ChatMessageDto
    // FE sẽ gọi hub method "JoinConversation" và "SendMessage"

    /// <summary>
    /// DTO gửi qua SignalR — cả BE và FE đều dùng cùng cấu trúc này.
    /// </summary>
    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderAvatar { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    // ─── Hub ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// ChatHub xử lý real-time messaging qua SignalR.
    /// Dùng lại IMessageService và IConversationService sẵn có.
    /// </summary>
    public class ChatHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly IUserService _userService;

        public ChatHub(IMessageService messageService, IUserService userService)
        {
            _messageService = messageService;
            _userService = userService;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Lấy userId từ JWT claim được inject bởi JwtAuthMiddleware.</summary>
        private Guid GetUserId()
        {
            var raw = Context.GetHttpContext()?.User?.FindFirst("userID")?.Value;
            if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out var id))
                throw new HubException("Unauthorized: invalid token or session expired.");
            return id;
        }

        // ── Hub Methods (FE → BE) ─────────────────────────────────────────────

        /// <summary>
        /// Bước 1: FE gọi để tham gia vào group SignalR tương ứng với conversationId.
        /// Chỉ cho phép nếu user thực sự là thành viên của conversation (kiểm tra DB).
        /// </summary>
        public async Task JoinConversation(string conversationId)
        {
            try
            {
                var userId = GetUserId();

                if (!Guid.TryParse(conversationId, out var convGuid))
                    throw new HubException("Invalid conversationId.");

                // Tái dụng logic kiểm tra quyền truy cập từ MessageService
                var hasAccess = await _messageService.IsUserInConversationAsync(userId, convGuid);
                if (!hasAccess)
                    throw new HubException("Access denied: you are not part of this conversation.");

                // Thêm connection hiện tại vào SignalR group
                await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);

                // Thông báo cho chính client biết join thành công
                await Clients.Caller.SendAsync("Joined", conversationId);
            }
            catch (HubException) { throw; }
            catch (Exception ex)
            {
                throw new HubException($"Failed to join conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// Bước 2: FE gọi để gửi tin nhắn.
        /// BE lưu vào DB (tái dùng MessageService) rồi broadcast tới tất cả thành viên trong group.
        /// </summary>
        public async Task SendMessage(string conversationId, string content)
        {
            try
            {
                var userId = GetUserId();

                if (!Guid.TryParse(conversationId, out var convGuid))
                    throw new HubException("Invalid conversationId.");

                if (string.IsNullOrWhiteSpace(content))
                    throw new HubException("Message content cannot be empty.");

                // Kiểm tra quyền
                var hasAccess = await _messageService.IsUserInConversationAsync(userId, convGuid);
                if (!hasAccess)
                    throw new HubException("Access denied.");

                // Lưu vào database — tái dùng MessageService sẵn có
                var request = new CreateMessageRequest
                {
                    ConversationId = convGuid,
                    Content = content.Trim()
                };
                await _messageService.CreateMessageAsync(request, userId);

                // Lấy thông tin sender để gửi kèm cho FE
                var sender = await _userService.GetByIdAsync(userId);

                var payload = new ChatMessageDto
                {
                    Id = Guid.NewGuid(),
                    ConversationId = convGuid,
                    SenderId = userId,
                    SenderName = sender.Username ?? "Unknown",
                    SenderAvatar = sender.Avatar ?? "",
                    Content = content.Trim(),
                    Timestamp = DateTime.UtcNow
                };

                // Broadcast tới TẤT CẢ thành viên trong group (kể cả người gửi)
                await Clients.Group(conversationId).SendAsync("ReceiveMessage", payload);
            }
            catch (HubException) { throw; }
            catch (Exception ex)
            {
                throw new HubException($"Failed to send message: {ex.Message}");
            }
        }

        /// <summary>Rời khỏi group khi ngắt kết nối.</summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // SignalR tự dọn dẹp group membership khi disconnect — không cần làm gì thêm
            await base.OnDisconnectedAsync(exception);
        }
    }
}
