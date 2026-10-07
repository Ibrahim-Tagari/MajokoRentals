using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.DTOs
{
    // Request DTOs

    /// <summary>
    /// Request to create a chatbot escalation
    /// </summary>
    public class CreateEscalationRequest
    {
        public int ChatConversationId { get; set; }
        public string? EscalationReason { get; set; }
        public string? Category { get; set; }
        public EscalationPriority Priority { get; set; } = EscalationPriority.Normal;
        public string? IssueSummary { get; set; }
        public List<string>? Tags { get; set; }
    }

    /// <summary>
    /// Request to assign an escalation to an admin
    /// </summary>
    public class AssignEscalationRequest
    {
        public string AdminId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to respond to an escalation
    /// </summary>
    public class RespondToEscalationRequest
    {
        public string Response { get; set; } = string.Empty;
        public EscalationStatus? NewStatus { get; set; }
    }

    /// <summary>
    /// Request to resolve an escalation
    /// </summary>
    public class ResolveEscalationRequest
    {
        public string ResolutionNotes { get; set; } = string.Empty;
        public bool CreateFollowUp { get; set; }
        public string? FollowUpNotes { get; set; }
    }

    /// <summary>
    /// Request to update escalation status
    /// </summary>
    public class UpdateEscalationStatusRequest
    {
        public EscalationStatus Status { get; set; }
        public string? Notes { get; set; }
    }

    // Response DTOs

    /// <summary>
    /// Chatbot escalation response
    /// </summary>
    public class ChatbotEscalationDto
    {
        public int Id { get; set; }
        public int ChatConversationId { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public string? UserPhone { get; set; }
        public string? UserProfilePictureUrl { get; set; }
        public UserRole UserRole { get; set; }

        public EscalationStatus Status { get; set; }
        public EscalationPriority Priority { get; set; }
        public string? EscalationReason { get; set; }
        public string? Category { get; set; }

        public string? AssignedToAdminId { get; set; }
        public string? AssignedToAdminName { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? FirstResponseAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public string? ResolutionNotes { get; set; }
        public string? IssueSummary { get; set; }
        public List<string>? Tags { get; set; }

        public int? MessagingSessionId { get; set; }
        public bool EmailNotificationSent { get; set; }
        public bool InAppNotificationCreated { get; set; }

        public int? ResponseTimeMinutes { get; set; }
        public double? ResolutionTimeHours { get; set; }

        public List<ChatMessageSummaryDto> RecentMessages { get; set; } = new();
    }

    /// <summary>
    /// Chat message summary for escalation context
    /// </summary>
    public class ChatMessageSummaryDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsFromUser { get; set; }
        public DateTime SentAt { get; set; }
        public string? Intent { get; set; }
    }

    /// <summary>
    /// Paginated escalations response
    /// </summary>
    public class EscalationsResponse
    {
        public List<ChatbotEscalationDto> Escalations { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// Summary of pending escalations
    /// </summary>
    public class PendingEscalationsSummary
    {
        public int TotalPending { get; set; }
        public int TotalAssigned { get; set; }
        public int TotalInProgress { get; set; }
        public int UrgentCount { get; set; }
        public int HighPriorityCount { get; set; }
        public double AverageResponseTimeMinutes { get; set; }
        public List<ChatbotEscalationDto> RecentEscalations { get; set; } = new();
    }

    /// <summary>
    /// Admin workload summary
    /// </summary>
    public class AdminWorkloadDto
    {
        public string AdminId { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public int AssignedEscalations { get; set; }
        public int PendingEscalations { get; set; }
        public int InProgressEscalations { get; set; }
        public int ResolvedToday { get; set; }
        public double AverageResolutionTimeHours { get; set; }
    }

    /// <summary>
    /// Escalation statistics
    /// </summary>
    public class EscalationStatistics
    {
        public int TotalEscalations { get; set; }
        public int ResolvedEscalations { get; set; }
        public int PendingEscalations { get; set; }
        public double AverageResolutionTimeHours { get; set; }
        public double AverageResponseTimeMinutes { get; set; }
        public Dictionary<string, int> EscalationsByCategory { get; set; } = new();
        public Dictionary<EscalationPriority, int> EscalationsByPriority { get; set; } = new();
    }
}
