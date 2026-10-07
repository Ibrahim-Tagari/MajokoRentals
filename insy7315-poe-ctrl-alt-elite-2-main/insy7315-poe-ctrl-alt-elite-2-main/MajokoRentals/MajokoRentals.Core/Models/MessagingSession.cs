namespace MajokoRentals.Core.Models
{
    /// <summary>
    /// Represents an approved messaging session between tenant and property manager/caretaker
    /// or between user and admin for chatbot escalations
    /// </summary>
    public class MessagingSession
    {
        public int Id { get; set; }

        /// <summary>
        /// User who initiated the session (typically Tenant or user with escalation)
        /// </summary>
        public string InitiatorId { get; set; } = string.Empty;
        public virtual ApplicationUser Initiator { get; set; } = null!;

        /// <summary>
        /// User who will participate (PropertyManager, Caretaker, or Admin)
        /// </summary>
        public string? ParticipantId { get; set; }
        public virtual ApplicationUser? Participant { get; set; }

        /// <summary>
        /// Type of session: UserToUser or ChatbotEscalation
        /// </summary>
        public SessionType SessionType { get; set; }

        /// <summary>
        /// Subject or reason for the session
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Additional details about the request
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Current approval status
        /// </summary>
        public ApprovalStatus ApprovalStatus { get; set; }

        /// <summary>
        /// Admin or Caretaker who approved/rejected the session
        /// </summary>
        public string? ApprovedById { get; set; }
        public virtual ApplicationUser? ApprovedBy { get; set; }

        /// <summary>
        /// When the session was approved or rejected
        /// </summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// Reason for rejection if applicable
        /// </summary>
        public string? RejectionReason { get; set; }

        /// <summary>
        /// Related property if applicable
        /// </summary>
        public long? RelatedPropertyId { get; set; }
        public virtual Property? RelatedProperty { get; set; }

        /// <summary>
        /// Related maintenance request if applicable
        /// </summary>
        public int? RelatedMaintenanceRequestId { get; set; }
        public virtual MaintenanceRequest? RelatedMaintenanceRequest { get; set; }

        /// <summary>
        /// Related lease if applicable
        /// </summary>
        public int? RelatedLeaseId { get; set; }
        public virtual Lease? RelatedLease { get; set; }

        /// <summary>
        /// Related chatbot escalation if this is an escalation session
        /// </summary>
        public int? RelatedChatbotEscalationId { get; set; }
        public virtual ChatbotEscalation? RelatedChatbotEscalation { get; set; }

        /// <summary>
        /// Whether the session is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// When the session was requested
        /// </summary>
        public DateTime RequestedAt { get; set; }

        /// <summary>
        /// When the session started (after approval)
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the session ended
        /// </summary>
        public DateTime? EndedAt { get; set; }

        /// <summary>
        /// Session priority level
        /// </summary>
        public SessionPriority Priority { get; set; }

        /// <summary>
        /// Messages in this session
        /// </summary>
        public virtual ICollection<SessionMessage> Messages { get; set; } = new List<SessionMessage>();

        /// <summary>
        /// Additional notes about the session
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Type of messaging session
    /// </summary>
    public enum SessionType
    {
        /// <summary>
        /// Direct messaging between users (Tenant to Manager/Caretaker)
        /// </summary>
        UserToUser = 1,

        /// <summary>
        /// Session created from chatbot escalation
        /// </summary>
        ChatbotEscalation = 2
    }

    /// <summary>
    /// Approval status of a messaging session
    /// </summary>
    public enum ApprovalStatus
    {
        /// <summary>
        /// Session is awaiting approval
        /// </summary>
        Pending = 1,

        /// <summary>
        /// Session has been approved and can proceed
        /// </summary>
        Approved = 2,

        /// <summary>
        /// Session has been rejected
        /// </summary>
        Rejected = 3
    }

    /// <summary>
    /// Priority level for messaging sessions
    /// </summary>
    public enum SessionPriority
    {
        /// <summary>
        /// Normal priority
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority - requires faster response
        /// </summary>
        High = 2,

        /// <summary>
        /// Urgent - requires immediate attention
        /// </summary>
        Urgent = 3
    }
}
