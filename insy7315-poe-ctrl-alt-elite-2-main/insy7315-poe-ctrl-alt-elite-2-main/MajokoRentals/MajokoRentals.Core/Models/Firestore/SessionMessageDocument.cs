using Google.Cloud.Firestore;

namespace MajokoRentals.Core.Models.Firestore
{
    /// <summary>
    /// Firestore document for messages within a session
    /// Collection: messaging-sessions/{sessionId}/messages
    /// Subcollection of MessagingSessionDocument
    /// </summary>
    [FirestoreData]
    public class SessionMessageDocument
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }

        [FirestoreProperty]
        public string SessionId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string SenderId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string SenderName { get; set; } = string.Empty;

        [FirestoreProperty]
        public string? SenderProfilePictureUrl { get; set; }

        [FirestoreProperty]
        public string Content { get; set; } = string.Empty;

        [FirestoreProperty]
        public string MessageType { get; set; } = "Text"; // Text, System, Attachment

        [FirestoreProperty]
        public Timestamp SentAt { get; set; }

        [FirestoreProperty]
        public bool IsRead { get; set; }

        [FirestoreProperty]
        public Timestamp? ReadAt { get; set; }

        [FirestoreProperty]
        public List<string>? AttachmentUrls { get; set; }

        [FirestoreProperty]
        public bool IsEdited { get; set; }

        [FirestoreProperty]
        public Timestamp? EditedAt { get; set; }

        [FirestoreProperty]
        public bool IsDeleted { get; set; }

        [FirestoreProperty]
        public Timestamp? DeletedAt { get; set; }

        [FirestoreProperty]
        public string? ReplyToMessageId { get; set; }

        [FirestoreProperty]
        public string? ReplyToMessageContent { get; set; }

        [FirestoreProperty]
        public string? ReplyToMessageSenderName { get; set; }
    }
}
