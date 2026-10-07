namespace MajokoRentals.Core.Models
{
    /// <summary>
    /// Represents a message within a messaging session
    /// </summary>
    public class SessionMessage
    {
        public int Id { get; set; }

        /// <summary>
        /// The messaging session this message belongs to
        /// </summary>
        public int SessionId { get; set; }
        public virtual MessagingSession Session { get; set; } = null!;

        /// <summary>
        /// User who sent the message
        /// </summary>
        public string SenderId { get; set; } = string.Empty;
        public virtual ApplicationUser Sender { get; set; } = null!;

        /// <summary>
        /// Message content
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Message type (Text, System, Attachment)
        /// </summary>
        public MessageType MessageType { get; set; }

        /// <summary>
        /// When the message was sent
        /// </summary>
        public DateTime SentAt { get; set; }

        /// <summary>
        /// Whether the message has been read by the recipient
        /// </summary>
        public bool IsRead { get; set; }

        /// <summary>
        /// When the message was read
        /// </summary>
        public DateTime? ReadAt { get; set; }

        /// <summary>
        /// Attachment URLs (JSON array)
        /// </summary>
        public string? AttachmentUrls { get; set; }

        /// <summary>
        /// Whether the message has been edited
        /// </summary>
        public bool IsEdited { get; set; }

        /// <summary>
        /// When the message was last edited
        /// </summary>
        public DateTime? EditedAt { get; set; }

        /// <summary>
        /// Whether the message has been deleted
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// When the message was deleted
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// ID of the message this is replying to
        /// </summary>
        public int? ReplyToMessageId { get; set; }
        public virtual SessionMessage? ReplyToMessage { get; set; }

        /// <summary>
        /// Messages that replied to this message
        /// </summary>
        public virtual ICollection<SessionMessage> Replies { get; set; } = new List<SessionMessage>();
    }

    /// <summary>
    /// Type of message
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Regular text message
        /// </summary>
        Text = 1,

        /// <summary>
        /// System generated message (e.g., "Session approved")
        /// </summary>
        System = 2,

        /// <summary>
        /// Message with file attachments
        /// </summary>
        Attachment = 3
    }
}
