using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MajokoRentals.Core.Models
{
    public class MaintenanceRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public long PropertyId { get; set; }

        [ForeignKey("PropertyId")]
        public virtual Property Property { get; set; } = null!;

        [Required]
        public string ReportedById { get; set; } = string.Empty;

        [ForeignKey("ReportedById")]
        public virtual ApplicationUser ReportedBy { get; set; } = null!;

        public string? AssignedToId { get; set; }

        [ForeignKey("AssignedToId")]
        public virtual ApplicationUser? AssignedTo { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public MaintenanceCategory Category { get; set; }

        [Required]
        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

        public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Pending;

        // Images before repair
        public string? ImageUrls { get; set; } // JSON array

        // Images after repair
        public string? CompletionImageUrls { get; set; } // JSON array

        [StringLength(500)]
        public string? Location { get; set; } // Specific location within property

        public DateTime ReportedDate { get; set; } = DateTime.UtcNow;

        public DateTime? AssignedDate { get; set; }

        public DateTime? StartedDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public double? EstimatedCost { get; set; }

        public double? ActualCost { get; set; }

        public string? ResolutionNotes { get; set; }

        public string? Comments { get; set; }

        // For offline sync
        public bool IsSynced { get; set; } = true;
        public DateTime? LastSyncedAt { get; set; }

        // Calculated properties
        [NotMapped]
        public int DaysOpen => Status != MaintenanceStatus.Resolved
            ? (DateTime.UtcNow - ReportedDate).Days
            : (CompletedDate!.Value - ReportedDate).Days;
    }
}

