using MajokoRentals.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.DTOs
{
    public class MaintenanceRequestDto
    {
        public string Id { get; set; } = string.Empty;
        public long PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MaintenanceCategory Category { get; set; }
        public MaintenancePriority Priority { get; set; }
        public MaintenanceStatus Status { get; set; }
        public string? Location { get; set; }
        public DateTime ReportedDate { get; set; }
        public string ReportedByName { get; set; } = string.Empty;
        public string? AssignedToName { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public double? EstimatedCost { get; set; }
        public double? ActualCost { get; set; }
        public string? ResolutionNotes { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public List<string> CompletionImageUrls { get; set; } = new();
    }

    public class CreateMaintenanceRequestDto
    {
        [Required]
        public long PropertyId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public MaintenanceCategory Category { get; set; }

        [Required]
        public MaintenancePriority Priority { get; set; }

        [StringLength(200)]
        public string? Location { get; set; }

        public string? Comments { get; set; }
    }

    public class UpdateMaintenanceStatusDto
    {
        [Required]
        public string RequestId { get; set; } = string.Empty;

        [Required]
        public MaintenanceStatus Status { get; set; }

        public string? Notes { get; set; }
        public double? ActualCost { get; set; }
    }
}

