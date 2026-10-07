using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MajokoRentals.Core.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;

        [ForeignKey("SenderId")]
        public virtual ApplicationUser Sender { get; set; } = null!;

        [Required]
        public string RecipientId { get; set; } = string.Empty;

        [ForeignKey("RecipientId")]
        public virtual ApplicationUser Recipient { get; set; } = null!;

        [Required]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public DateTime SentDate { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        public DateTime? ReadDate { get; set; }

        public string? AttachmentUrls { get; set; } // JSON array

        public int? RelatedPropertyId { get; set; }

        public int? RelatedMaintenanceRequestId { get; set; }

        public int? RelatedLeaseId { get; set; }

        public bool IsArchived { get; set; } = false;
    }
}

