using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Services;
using System.Security.Claims;

namespace MajokoRentals.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagingController : ControllerBase
    {
        private readonly IMessagingService _messagingService;
        private readonly ILogger<MessagingController> _logger;

        public MessagingController(
            IMessagingService messagingService,
            ILogger<MessagingController> logger)
        {
            _messagingService = messagingService;
            _logger = logger;
        }

        /// <summary>
        /// Request a new messaging session
        /// </summary>
        [HttpPost("request-session")]
        public async Task<ActionResult<MessagingSessionDto>> RequestSession([FromBody] CreateMessagingSessionRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var session = await _messagingService.CreateSessionRequestAsync(userId, request);
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating messaging session");
                return StatusCode(500, new { error = "Failed to create messaging session", details = ex.Message });
            }
        }

        /// <summary>
        /// Get pending session requests (for admins, property managers, caretakers)
        /// </summary>
        [HttpGet("pending-requests")]
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        public async Task<ActionResult<PendingSessionsSummary>> GetPendingRequests()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var summary = await _messagingService.GetPendingSessionsAsync(userId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending sessions");
                return StatusCode(500, new { error = "Failed to get pending sessions", details = ex.Message });
            }
        }

        /// <summary>
        /// Approve or reject a messaging session
        /// </summary>
        [HttpPost("approve/{sessionId}")]
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        public async Task<ActionResult<MessagingSessionDto>> ApproveSession(string sessionId, [FromBody] ApproveSessionRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var session = await _messagingService.ApproveSessionAsync(sessionId, userId, request);
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving session {sessionId}");
                return StatusCode(500, new { error = "Failed to approve session", details = ex.Message });
            }
        }

        /// <summary>
        /// Reject a messaging session
        /// </summary>
        [HttpPost("reject/{sessionId}")]
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        public async Task<ActionResult<MessagingSessionDto>> RejectSession(string sessionId, [FromBody] string rejectionReason)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var session = await _messagingService.RejectSessionAsync(sessionId, userId, rejectionReason);
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting session {sessionId}");
                return StatusCode(500, new { error = "Failed to reject session", details = ex.Message });
            }
        }

        /// <summary>
        /// Send a message in a session
        /// </summary>
        [HttpPost("send")]
        public async Task<ActionResult<SessionMessageDto>> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var message = await _messagingService.SendMessageAsync(userId, request);
                return Ok(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { error = "Failed to send message", details = ex.Message });
            }
        }

        /// <summary>
        /// Get user's messaging sessions
        /// </summary>
        [HttpGet("sessions")]
        public async Task<ActionResult<List<MessagingSessionDto>>> GetMySessions([FromQuery] bool activeOnly = true)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var sessions = await _messagingService.GetUserSessionsAsync(userId, activeOnly);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user sessions");
                return StatusCode(500, new { error = "Failed to get sessions", details = ex.Message });
            }
        }

        /// <summary>
        /// Get messages for a specific session
        /// </summary>
        [HttpGet("messages/{sessionId}")]
        public async Task<ActionResult<SessionMessagesResponse>> GetMessages(
            string sessionId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                // TODO: Verify user has access to this session

                var messages = await _messagingService.GetSessionMessagesAsync(sessionId, page, pageSize);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting messages for session {sessionId}");
                return StatusCode(500, new { error = "Failed to get messages", details = ex.Message });
            }
        }

        /// <summary>
        /// Mark messages as read
        /// </summary>
        [HttpPost("mark-read")]
        public async Task<IActionResult> MarkMessagesAsRead([FromBody] MarkMessagesReadRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                await _messagingService.MarkMessagesAsReadAsync(request.MessageIds, userId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
                return StatusCode(500, new { error = "Failed to mark messages as read", details = ex.Message });
            }
        }

        /// <summary>
        /// Get unread message count for a session
        /// </summary>
        [HttpGet("unread-count/{sessionId}")]
        public async Task<ActionResult<int>> GetUnreadCount(string sessionId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var count = await _messagingService.GetUnreadMessageCountAsync(sessionId, userId);
                return Ok(new { unreadCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting unread count for session {sessionId}");
                return StatusCode(500, new { error = "Failed to get unread count", details = ex.Message });
            }
        }

        /// <summary>
        /// End a messaging session
        /// </summary>
        [HttpPost("end/{sessionId}")]
        public async Task<IActionResult> EndSession(string sessionId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                // TODO: Verify user has permission to end this session

                await _messagingService.EndSessionAsync(sessionId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error ending session {sessionId}");
                return StatusCode(500, new { error = "Failed to end session", details = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific session by ID
        /// </summary>
        [HttpGet("session/{sessionId}")]
        public async Task<ActionResult<MessagingSessionDto>> GetSession(string sessionId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var session = await _messagingService.GetSessionByIdAsync(sessionId);
                if (session == null)
                {
                    return NotFound(new { error = "Session not found" });
                }

                // TODO: Verify user has access to this session

                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting session {sessionId}");
                return StatusCode(500, new { error = "Failed to get session", details = ex.Message });
            }
        }
    }
}
