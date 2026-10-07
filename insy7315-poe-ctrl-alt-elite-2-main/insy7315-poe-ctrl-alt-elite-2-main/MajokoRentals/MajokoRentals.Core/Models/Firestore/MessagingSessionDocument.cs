using Google.Cloud.Firestore;

namespace MajokoRentals.Core.Models.Firestore
{
    /// <summary>
    /// Firestore document for messaging sessions
    /// Collection: messaging-sessions
    /// </summary>
    [FirestoreData]
    public class MessagingSessionDocument
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }

        [FirestoreProperty]
        public string InitiatorId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string InitiatorName { get; set; } = string.Empty;

        [FirestoreProperty]
        public string? InitiatorEmail { get; set; }

        [FirestoreProperty]
        public string? InitiatorProfilePictureUrl { get; set; }

        [FirestoreProperty]
        public string? ParticipantId { get; set; }

        [FirestoreProperty]
        public string? ParticipantName { get; set; }

        [FirestoreProperty]
        public string? ParticipantEmail { get; set; }

        [FirestoreProperty]
        public string? ParticipantProfilePictureUrl { get; set; }

        [FirestoreProperty]
        public string SessionType { get; set; } = "UserToUser"; // UserToUser, ChatbotEscalation

        [FirestoreProperty]
        public string Subject { get; set; } = string.Empty;

        [FirestoreProperty]
        public string? Description { get; set; }

        [FirestoreProperty]
        public string ApprovalStatus { get; set; } = "Pending"; // Pending, Approved, Rejected

        [FirestoreProperty]
        public string? ApprovedById { get; set; }

        [FirestoreProperty]
        public string? ApprovedByName { get; set; }

        [FirestoreProperty]
        public Timestamp? ApprovedAt { get; set; }

        [FirestoreProperty]
        public string? RejectionReason { get; set; }

        [FirestoreProperty]
        public string? RelatedPropertyId { get; set; }

        [FirestoreProperty]
        public string? RelatedPropertyName { get; set; }

        [FirestoreProperty]
        public int? RelatedMaintenanceRequestId { get; set; }

        [FirestoreProperty]
        public int? RelatedLeaseId { get; set; }

        [FirestoreProperty]
        public string? RelatedChatbotEscalationId { get; set; }

        [FirestoreProperty]
        public bool IsActive { get; set; }

        [FirestoreProperty]
        public Timestamp RequestedAt { get; set; }

        [FirestoreProperty]
        public Timestamp? StartedAt { get; set; }

        [FirestoreProperty]
        public Timestamp? EndedAt { get; set; }

        [FirestoreProperty]
        public string Priority { get; set; } = "Normal"; // Normal, High, Urgent

        [FirestoreProperty]
        public int UnreadCountInitiator { get; set; }

        [FirestoreProperty]
        public int UnreadCountParticipant { get; set; }

        [FirestoreProperty]
        public string? LastMessageContent { get; set; }

        [FirestoreProperty]
        public Timestamp? LastMessageAt { get; set; }

        [FirestoreProperty]
        public string? LastMessageSenderId { get; set; }

        [FirestoreProperty]
        public string? Notes { get; set; }

        [FirestoreProperty]
        public Timestamp CreatedAt { get; set; }

        [FirestoreProperty]
        public Timestamp UpdatedAt { get; set; }
    }
}
