using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.DTOs
{
    // Request DTOs

    /// <summary>
    /// Request to create a new messaging session
    /// </summary>
    public class CreateMessagingSessionRequest
    {
        public string? ParticipantId { get; set; }
        public SessionType SessionType { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? Description { get; set; }
        public long? RelatedPropertyId { get; set; }
        public int? RelatedMaintenanceRequestId { get; set; }
        public int? RelatedLeaseId { get; set; }
        public SessionPriority Priority { get; set; } = SessionPriority.Normal;
    }

    /// <summary>
    /// Request to approve or reject a messaging session
    /// </summary>
    public class ApproveSessionRequest
    {
        public bool Approved { get; set; }
        public string? RejectionReason { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request to send a message in a session
    /// </summary>
    public class SendMessageRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public MessageType MessageType { get; set; } = MessageType.Text;
        public List<string>? AttachmentUrls { get; set; }
        public int? ReplyToMessageId { get; set; }
    }

    /// <summary>
    /// Request to mark messages as read
    /// </summary>
    public class MarkMessagesReadRequest
    {
        public List<string> MessageIds { get; set; } = new();
    }

    // Response DTOs

    /// <summary>
    /// Messaging session response
    /// </summary>
    public class MessagingSessionDto
    {
        public string Id { get; set; } = string.Empty;
        public string InitiatorId { get; set; } = string.Empty;
        public string InitiatorName { get; set; } = string.Empty;
        public string? InitiatorProfilePictureUrl { get; set; }

        public string? ParticipantId { get; set; }
        public string? ParticipantName { get; set; }
        public string? ParticipantProfilePictureUrl { get; set; }

        public SessionType SessionType { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ApprovalStatus ApprovalStatus { get; set; }

        public string? ApprovedById { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }

        public long? RelatedPropertyId { get; set; }
        public string? RelatedPropertyName { get; set; }
        public int? RelatedMaintenanceRequestId { get; set; }
        public int? RelatedLeaseId { get; set; }
        public int? RelatedChatbotEscalationId { get; set; }

        public bool IsActive { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public SessionPriority Priority { get; set; }

        public int UnreadMessageCount { get; set; }
        public SessionMessageDto? LastMessage { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Session message response
    /// </summary>
    public class SessionMessageDto
    {
        public string Id { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string? SenderProfilePictureUrl { get; set; }
        public string Content { get; set; } = string.Empty;
        public MessageType MessageType { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public List<string>? AttachmentUrls { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; }
        public int? ReplyToMessageId { get; set; }
        public SessionMessageDto? ReplyToMessage { get; set; }
    }

    /// <summary>
    /// Paginated session messages response
    /// </summary>
    public class SessionMessagesResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public List<SessionMessageDto> Messages { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// Summary of pending session requests
    /// </summary>
    public class PendingSessionsSummary
    {
        public int TotalPending { get; set; }
        public int HighPriority { get; set; }
        public int UrgentPriority { get; set; }
        public List<MessagingSessionDto> RecentRequests { get; set; } = new();
    }
}
