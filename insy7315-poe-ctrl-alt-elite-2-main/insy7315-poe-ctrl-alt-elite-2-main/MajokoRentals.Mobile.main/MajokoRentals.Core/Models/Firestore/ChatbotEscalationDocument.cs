using Google.Cloud.Firestore;

namespace MajokoRentals.Core.Models.Firestore
{
    /// <summary>
    /// Firestore document for chatbot escalations
    /// Collection: chatbot-escalations
    /// </summary>
    [FirestoreData]
    public class ChatbotEscalationDocument
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }

        [FirestoreProperty]
        public int ChatConversationId { get; set; }

        [FirestoreProperty]
        public string UserId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string UserName { get; set; } = string.Empty;

        [FirestoreProperty]
        public string? UserEmail { get; set; }

        [FirestoreProperty]
        public string? UserPhone { get; set; }

        [FirestoreProperty]
        public string? UserProfilePictureUrl { get; set; }

        [FirestoreProperty]
        public string UserRole { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Status { get; set; } = "Pending"; // Pending, Assigned, InProgress, AwaitingUserResponse, Resolved, Closed

        [FirestoreProperty]
        public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent

        [FirestoreProperty]
        public string? EscalationReason { get; set; }

        [FirestoreProperty]
        public string? Category { get; set; }

        [FirestoreProperty]
        public string? AssignedToAdminId { get; set; }

        [FirestoreProperty]
        public string? AssignedToAdminName { get; set; }

        [FirestoreProperty]
        public string? AssignedToAdminEmail { get; set; }

        [FirestoreProperty]
        public Timestamp CreatedAt { get; set; }

        [FirestoreProperty]
        public Timestamp? AssignedAt { get; set; }

        [FirestoreProperty]
        public Timestamp? FirstResponseAt { get; set; }

        [FirestoreProperty]
        public Timestamp? ResolvedAt { get; set; }

        [FirestoreProperty]
        public string? ResolutionNotes { get; set; }

        [FirestoreProperty]
        public bool EmailNotificationSent { get; set; }

        [FirestoreProperty]
        public Timestamp? EmailNotificationSentAt { get; set; }

        [FirestoreProperty]
        public bool InAppNotificationCreated { get; set; }

        [FirestoreProperty]
        public string? IssueSummary { get; set; }

        [FirestoreProperty]
        public List<string>? Tags { get; set; }

        [FirestoreProperty]
        public string? MessagingSessionId { get; set; }

        [FirestoreProperty]
        public List<ChatMessageSnapshot>? RecentMessages { get; set; }

        [FirestoreProperty]
        public int? ResponseTimeMinutes { get; set; }

        [FirestoreProperty]
        public double? ResolutionTimeHours { get; set; }

        [FirestoreProperty]
        public Timestamp UpdatedAt { get; set; }
    }

    /// <summary>
    /// Snapshot of chat messages for escalation context
    /// </summary>
    [FirestoreData]
    public class ChatMessageSnapshot
    {
        [FirestoreProperty]
        public int Id { get; set; }

        [FirestoreProperty]
        public string Message { get; set; } = string.Empty;

        [FirestoreProperty]
        public bool IsFromUser { get; set; }

        [FirestoreProperty]
        public Timestamp SentAt { get; set; }

        [FirestoreProperty]
        public string? Intent { get; set; }

        [FirestoreProperty]
        public double? ConfidenceScore { get; set; }
    }
}
