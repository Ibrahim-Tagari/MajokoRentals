using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;

namespace MajokoRentals.Models.ViewModels
{
    public class RequestChatViewModel
    {
        public string? ParticipantId { get; set; }
        public string ParticipantName { get; set; } = string.Empty;
        public SessionType SessionType { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string InitialMessage { get; set; } = string.Empty;
        public long? RelatedPropertyId { get; set; }
        public int? RelatedMaintenanceRequestId { get; set; }
        public int? RelatedLeaseId { get; set; }
        public SessionPriority Priority { get; set; } = SessionPriority.Normal;

        // For dropdowns
        public List<UserOption> AvailableRecipients { get; set; } = new();
        public List<PropertyOption> Properties { get; set; } = new();
    }

    public class UserOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class PropertyOption
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class PendingRequestsViewModel
    {
        public List<MessagingSessionDto> PendingRequests { get; set; } = new();
        public int TotalCount { get; set; }
        public string UserRole { get; set; } = string.Empty;
    }

    public class ChatConversationViewModel
    {
        public MessagingSessionDto Session { get; set; } = null!;
        public List<SessionMessageDto> Messages { get; set; } = new();
        public int TotalMessages { get; set; }
        public int UnreadCount { get; set; }
        public bool CanSendMessages { get; set; }
        public bool CanEndSession { get; set; }
        public string CurrentUserId { get; set; } = string.Empty;
        public string CurrentUserName { get; set; } = string.Empty;
    }

    public class MyChatsViewModel
    {
        public List<MessagingSessionDto> ActiveSessions { get; set; } = new();
        public List<MessagingSessionDto> PendingSessions { get; set; } = new();
        public int TotalUnreadCount { get; set; }
        public string UserRole { get; set; } = string.Empty;
    }

    public class SendMessageViewModel
    {
        public string SessionId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "Text";
        public List<string>? AttachmentUrls { get; set; }
    }
}
