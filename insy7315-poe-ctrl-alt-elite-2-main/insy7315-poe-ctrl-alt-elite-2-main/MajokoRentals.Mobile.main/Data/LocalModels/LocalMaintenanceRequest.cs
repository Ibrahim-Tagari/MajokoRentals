using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MajokoRentals.Core.Models;

namespace MajokoRentals.Mobile.Data.LocalModels
{
    /// <summary>
    /// Simplified local version of MaintenanceRequest for offline storage
    /// </summary>
    public class LocalMaintenanceRequest
    {
        [Key]
        public int Id { get; set; }

        // Foreign Keys (simplified - no navigation properties)
        [Required]
        public long PropertyId { get; set; }

        [Required]
        public string ReportedById { get; set; } = string.Empty;

        public string? AssignedToId { get; set; }

        // Core fields
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

        // Local file paths (JSON array of local paths)
        public string? LocalImagePaths { get; set; }

        // Cloud URLs (after sync)
        public string? ImageUrls { get; set; }

        public string? CompletionImageUrls { get; set; }

        [StringLength(500)]
        public string? Location { get; set; }

        // Dates
        public DateTime ReportedDate { get; set; } = DateTime.UtcNow;
        public DateTime? AssignedDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        // Cost tracking
        public double? EstimatedCost { get; set; }
        public double? ActualCost { get; set; }

        public string? ResolutionNotes { get; set; }
        public string? Comments { get; set; }

        // Sync tracking
        public bool IsSynced { get; set; } = false;
        public DateTime? LastSyncedAt { get; set; }
        public DateTime CreatedLocally { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedLocally { get; set; } = DateTime.UtcNow;

        // Server version tracking for conflict detection
        public DateTime? ServerLastModified { get; set; }
    }
}
