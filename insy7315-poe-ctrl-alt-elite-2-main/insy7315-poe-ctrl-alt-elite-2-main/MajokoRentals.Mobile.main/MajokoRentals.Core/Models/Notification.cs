using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MajokoRentals.Core.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; } = NotificationType.InApp;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }

        public string? ActionUrl { get; set; }

        public int? RelatedEntityId { get; set; }

        public string? RelatedEntityType { get; set; } // "Invoice", "MaintenanceRequest", "Lease", etc.

        public bool IsSent { get; set; } = false;

        public DateTime? SentAt { get; set; }

        public string? ErrorMessage { get; set; }
    }
}

