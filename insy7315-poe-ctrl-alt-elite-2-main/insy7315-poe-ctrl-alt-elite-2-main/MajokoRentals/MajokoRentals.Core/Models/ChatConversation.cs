using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MajokoRentals.Core.Models
{
    public class ChatConversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? EndedAt { get; set; }

        public bool IsEscalated { get; set; } = false;

        public DateTime? EscalatedAt { get; set; }

        public string? EscalatedTo { get; set; }

        public string? EscalationReason { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ConversationId { get; set; }

        [ForeignKey("ConversationId")]
        public virtual ChatConversation Conversation { get; set; } = null!;

        [Required]
        public string Message { get; set; } = string.Empty;

        public bool IsFromUser { get; set; } = true;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public string? Intent { get; set; } // Detected intent by AI

        public double? ConfidenceScore { get; set; }
    }
}

