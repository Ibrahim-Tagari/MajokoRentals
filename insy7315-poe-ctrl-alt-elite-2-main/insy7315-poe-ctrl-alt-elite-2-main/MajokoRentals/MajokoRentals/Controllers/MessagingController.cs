using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using MajokoRentals.Models.ViewModels;
using System.Security.Claims;
using System.Linq;

namespace MajokoRentals.Controllers
{
    [Authorize]
    public class MessagingController : Controller
    {
        private readonly IMessagingService _messagingService;
        private readonly IUserProfileService _userProfileService;
        private readonly ILogger<MessagingController> _logger;
        private readonly IApiClientService _apiClientService;

        public MessagingController(
            IMessagingService messagingService,
            IUserProfileService userProfileService,
            ILogger<MessagingController> logger,
            IApiClientService apiClientService)
        {
            _messagingService = messagingService;
            _userProfileService = userProfileService;
            _logger = logger;
            _apiClientService = apiClientService;
        }

        // GET: /Messaging/MyChats
        [HttpGet]
        public async Task<IActionResult> MyChats()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var activeSessions = await _messagingService.GetUserSessionsAsync(userId, activeOnly: true);
                var allSessions = await _messagingService.GetUserSessionsAsync(userId, activeOnly: false);

                var pendingSessions = allSessions
                    .Where(s => s.ApprovalStatus == ApprovalStatus.Pending)
                    .ToList();

                var totalUnread = activeSessions.Sum(s => s.UnreadMessageCount);

                var viewModel = new MyChatsViewModel
                {
                    ActiveSessions = activeSessions,
                    PendingSessions = pendingSessions,
                    TotalUnreadCount = totalUnread,
                    UserRole = User.FindFirstValue(ClaimTypes.Role) ?? "Tenant"
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user chats");
                TempData["ErrorMessage"] = "Unable to load your chats. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: /Messaging/RequestChat
        [HttpGet]
        public async Task<IActionResult> RequestChat(long? propertyId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                // Get available recipients based on user role
                var recipients = new List<UserOption>();

                if (userRole == "Tenant")
                {
                    // Tenants can contact Property Managers, Caretakers, or Admins
                    var admins = await _userProfileService.GetUsersByRoleAsync("Admin");
                    var managers = await _userProfileService.GetUsersByRoleAsync("PropertyManager");
                    var caretakers = await _userProfileService.GetUsersByRoleAsync("Caretaker");

                    recipients.AddRange(admins.Select(u => new UserOption
                    {
                        Id = u.Uid,
                        Name = $"{u.FirstName} {u.LastName}".Trim(),
                        Role = "Admin",
                        Email = u.Email
                    }));

                    recipients.AddRange(managers.Select(u => new UserOption
                    {
                        Id = u.Uid,
                        Name = $"{u.FirstName} {u.LastName}".Trim(),
                        Role = "Property Manager",
                        Email = u.Email
                    }));

                    recipients.AddRange(caretakers.Select(u => new UserOption
                    {
                        Id = u.Uid,
                        Name = $"{u.FirstName} {u.LastName}".Trim(),
                        Role = "Caretaker",
                        Email = u.Email
                    }));
                }

                var viewModel = new RequestChatViewModel
                {
                    RelatedPropertyId = propertyId,
                    AvailableRecipients = recipients,
                    Priority = SessionPriority.Normal
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading request chat form");
                TempData["ErrorMessage"] = "Unable to load the request form. Please try again.";
                return RedirectToAction("MyChats");
            }
        }

        // POST: /Messaging/RequestChat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestChat(RequestChatViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var request = new CreateMessagingSessionRequest
                {
                    ParticipantId = model.ParticipantId!,
                    SessionType = SessionType.UserToUser,
                    Subject = model.Subject,
                    Description = model.InitialMessage,
                    RelatedPropertyId = model.RelatedPropertyId,
                    RelatedMaintenanceRequestId = model.RelatedMaintenanceRequestId,
                    RelatedLeaseId = model.RelatedLeaseId,
                    Priority = model.Priority
                };

                var session = await _messagingService.CreateSessionRequestAsync(userId, request);

                TempData["SuccessMessage"] = "Chat request sent successfully! You'll be notified when it's approved.";
                return RedirectToAction("Conversation", new { id = session.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat request");
                ModelState.AddModelError("", "Unable to send chat request. Please try again.");
                return View(model);
            }
        }

        // GET: /Messaging/PendingRequests
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        [HttpGet]
        public async Task<IActionResult> PendingRequests()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var summary = await _messagingService.GetPendingSessionsAsync(userId);

                var viewModel = new PendingRequestsViewModel
                {
                    PendingRequests = summary.RecentRequests,
                    TotalCount = summary.TotalPending,
                    UserRole = User.FindFirstValue(ClaimTypes.Role) ?? ""
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending requests");
                TempData["ErrorMessage"] = "Unable to load pending requests. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: /Messaging/ApproveRequest
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRequest(string sessionId, bool approved, string? rejectionReason)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var request = new ApproveSessionRequest
                {
                    Approved = approved,
                    RejectionReason = rejectionReason
                };

                var session = await _messagingService.ApproveSessionAsync(sessionId, userId, request);

                var message = approved ? "Chat request approved successfully!" : "Chat request rejected.";
                return Json(new { success = true, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving/rejecting session {sessionId}");
                return Json(new { success = false, message = "Unable to process request. Please try again." });
            }
        }

        // GET: /Messaging/Conversation/{id}
        [HttpGet]
        public async Task<IActionResult> Conversation(string id, int page = 1)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var session = await _messagingService.GetSessionByIdAsync(id);
                if (session == null)
                {
                    TempData["ErrorMessage"] = "Chat session not found.";
                    return RedirectToAction("MyChats");
                }

                // Verify user has access
                if (session.InitiatorId != userId && session.ParticipantId != userId)
                {
                    var userRole = User.FindFirstValue(ClaimTypes.Role);
                    if (userRole != "Admin")
                    {
                        TempData["ErrorMessage"] = "You don't have access to this conversation.";
                        return RedirectToAction("MyChats");
                    }
                }

                var messagesResponse = await _messagingService.GetSessionMessagesAsync(id, page, 50);
                var unreadCount = await _messagingService.GetUnreadMessageCountAsync(id, userId);

                var userProfile = await _userProfileService.GetUserProfileAsync(userId);

                var viewModel = new ChatConversationViewModel
                {
                    Session = session,
                    Messages = messagesResponse.Messages,
                    TotalMessages = messagesResponse.TotalCount,
                    UnreadCount = unreadCount,
                    CanSendMessages = session.IsActive && session.ApprovalStatus == ApprovalStatus.Approved,
                    CanEndSession = session.InitiatorId == userId || session.ParticipantId == userId || User.IsInRole("Admin"),
                    CurrentUserId = userId,
                    CurrentUserName = userProfile != null ? $"{userProfile.FirstName} {userProfile.LastName}".Trim() : "Unknown User"
                };

                // Mark messages as read
                var unreadMessageIds = messagesResponse.Messages
                    .Where(m => !m.IsRead && m.SenderId != userId)
                    .Select(m => m.Id)
                    .ToList();

                if (unreadMessageIds.Any())
                {
                    await _messagingService.MarkMessagesAsReadAsync(unreadMessageIds, userId);
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading conversation {id}");
                TempData["ErrorMessage"] = "Unable to load conversation. Please try again.";
                return RedirectToAction("MyChats");
            }
        }

        // POST: /Messaging/SendMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(SendMessageViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    _logger.LogWarning($"Invalid model state: {errors}");
                    return Json(new { success = false, message = $"Invalid data: {errors}" });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                _logger.LogInformation($"Sending message in session {model.SessionId} from user {userId}");

                var request = new SendMessageRequest
                {
                    SessionId = model.SessionId,
                    Content = model.Content,
                    MessageType = MessageType.Text,
                    AttachmentUrls = model.AttachmentUrls
                };

                var message = await _messagingService.SendMessageAsync(userId, request);

                _logger.LogInformation($"Message sent successfully: {message.Id}");

                return Json(new
                {
                    success = true,
                    message = new
                    {
                        id = message.Id,
                        content = message.Content,
                        senderName = message.SenderName,
                        sentAt = message.SentAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        isFromCurrentUser = true
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return Json(new { success = false, message = $"Unable to send message: {ex.Message}" });
            }
        }

        // GET: /Messaging/GetPendingChatCount
        [HttpGet]
        public async Task<IActionResult> GetPendingChatCount()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, count = 0 });
                }

                var userRole = User.FindFirstValue(ClaimTypes.Role);
                if (userRole != "Admin" && userRole != "PropertyManager" && userRole != "Caretaker")
                {
                    return Json(new { success = false, count = 0 });
                }

                var summary = await _messagingService.GetPendingSessionsAsync(userId);
                return Json(new { success = true, count = summary.TotalPending });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending chat count");
                return Json(new { success = false, count = 0 });
            }
        }

        // POST: /Messaging/EndSession
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndSession(string sessionId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                await _messagingService.EndSessionAsync(sessionId);

                return Json(new { success = true, message = "Chat session ended successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error ending session {sessionId}");
                return Json(new { success = false, message = "Unable to end session. Please try again." });
            }
        }

    }
}
