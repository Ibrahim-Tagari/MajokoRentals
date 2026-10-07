namespace MajokoRentals.Core.Models
{
    /// <summary>
    /// Represents an escalation from the chatbot to human support
    /// </summary>
    public class ChatbotEscalation
    {
        public int Id { get; set; }

        /// <summary>
        /// Reference to the chatbot conversation that was escalated
        /// </summary>
        public int ChatConversationId { get; set; }
        public virtual ChatConversation ChatConversation { get; set; } = null!;

        /// <summary>
        /// User who triggered the escalation
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Current status of the escalation
        /// </summary>
        public EscalationStatus Status { get; set; }

        /// <summary>
        /// Priority level based on user role or issue type
        /// </summary>
        public EscalationPriority Priority { get; set; }

        /// <summary>
        /// Reason for escalation
        /// </summary>
        public string? EscalationReason { get; set; }

        /// <summary>
        /// Category of the issue
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Admin assigned to handle this escalation
        /// </summary>
        public string? AssignedToAdminId { get; set; }
        public virtual ApplicationUser? AssignedToAdmin { get; set; }

        /// <summary>
        /// When the escalation was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the escalation was assigned
        /// </summary>
        public DateTime? AssignedAt { get; set; }

        /// <summary>
        /// When the escalation was first responded to
        /// </summary>
        public DateTime? FirstResponseAt { get; set; }

        /// <summary>
        /// When the escalation was resolved
        /// </summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Admin's resolution notes
        /// </summary>
        public string? ResolutionNotes { get; set; }

        /// <summary>
        /// Whether email notification was sent to admins
        /// </summary>
        public bool EmailNotificationSent { get; set; }

        /// <summary>
        /// When email notification was sent
        /// </summary>
        public DateTime? EmailNotificationSentAt { get; set; }

        /// <summary>
        /// Whether in-app notification was created
        /// </summary>
        public bool InAppNotificationCreated { get; set; }

        /// <summary>
        /// Summary of the user's issue (auto-generated or manual)
        /// </summary>
        public string? IssueSummary { get; set; }

        /// <summary>
        /// Tags for categorization and searching
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        /// Related messaging session if one was created
        /// </summary>
        public int? MessagingSessionId { get; set; }
        public virtual MessagingSession? MessagingSession { get; set; }

        /// <summary>
        /// Average response time in minutes
        /// </summary>
        public int? ResponseTimeMinutes
        {
            get
            {
                if (FirstResponseAt.HasValue)
                {
                    return (int)(FirstResponseAt.Value - CreatedAt).TotalMinutes;
                }
                return null;
            }
        }

        /// <summary>
        /// Total resolution time in hours
        /// </summary>
        public double? ResolutionTimeHours
        {
            get
            {
                if (ResolvedAt.HasValue)
                {
                    return (ResolvedAt.Value - CreatedAt).TotalHours;
                }
                return null;
            }
        }
    }

    /// <summary>
    /// Status of a chatbot escalation
    /// </summary>
    public enum EscalationStatus
    {
        /// <summary>
        /// Escalation created but not yet assigned
        /// </summary>
        Pending = 1,

        /// <summary>
        /// Escalation assigned to an admin
        /// </summary>
        Assigned = 2,

        /// <summary>
        /// Admin is actively working on the escalation
        /// </summary>
        InProgress = 3,

        /// <summary>
        /// Waiting for user response
        /// </summary>
        AwaitingUserResponse = 4,

        /// <summary>
        /// Escalation has been resolved
        /// </summary>
        Resolved = 5,

        /// <summary>
        /// Escalation was closed without resolution
        /// </summary>
        Closed = 6
    }

    /// <summary>
    /// Priority level for escalations
    /// </summary>
    public enum EscalationPriority
    {
        /// <summary>
        /// Low priority - can wait
        /// </summary>
        Low = 1,

        /// <summary>
        /// Normal priority - standard response time
        /// </summary>
        Normal = 2,

        /// <summary>
        /// High priority - expedited response needed
        /// </summary>
        High = 3,

        /// <summary>
        /// Urgent - immediate attention required
        /// </summary>
        Urgent = 4
    }
}
