
using MajokoRentals.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.ViewModels
{
    public class MaintenanceRequestListViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public string ReportedByName { get; set; } = string.Empty;
        public MaintenanceCategory Category { get; set; }
        public MaintenancePriority Priority { get; set; }
        public MaintenanceStatus Status { get; set; }
        public DateTime ReportedDate { get; set; }
        public string? AssignedToName { get; set; }
        public int DaysOpen { get; set; }
    }

    public class MaintenanceRequestDetailsViewModel
    {
        public string Id { get; set; } = string.Empty;
        public long PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public string ReportedById { get; set; } = string.Empty;
        public string ReportedByName { get; set; } = string.Empty;
        public string ReportedByEmail { get; set; } = string.Empty;
        public string? AssignedToId { get; set; }
        public string? AssignedToName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MaintenanceCategory Category { get; set; }
        public MaintenancePriority Priority { get; set; }
        public MaintenanceStatus Status { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public List<string> CompletionImageUrls { get; set; } = new();
        public string? Location { get; set; }
        public DateTime ReportedDate { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public double? EstimatedCost { get; set; }
        public double? ActualCost { get; set; }
        public string? ResolutionNotes { get; set; }
        public string? Comments { get; set; }
        public int DaysOpen { get; set; }
    }

    public class CreateMaintenanceRequestViewModel
    {
        [Required]
        [Display(Name = "Property")]
        public long PropertyId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public MaintenanceCategory Category { get; set; }

        [Required]
        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

        [StringLength(500)]
        [Display(Name = "Specific Location")]
        public string? Location { get; set; }

        // Note: File uploads handled via API - IFormFile removed for mobile compatibility
    }

    public class EditMaintenanceRequestViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Property")]
        public long PropertyId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public MaintenanceCategory Category { get; set; }

        [Required]
        public MaintenancePriority Priority { get; set; }

        public MaintenanceStatus Status { get; set; }

        [StringLength(500)]
        [Display(Name = "Specific Location")]
        public string? Location { get; set; }

        public List<string> ExistingImages { get; set; } = new();

        [Display(Name = "Add New Images")]

        public List<string> ImagesToDelete { get; set; } = new();

        [Display(Name = "Estimated Cost")]
        public double? EstimatedCost { get; set; }
    }

    public class AssignMaintenanceRequestViewModel
    {
        public string RequestId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Assign To")]
        public string AssignedToId { get; set; } = string.Empty;

        [Display(Name = "Estimated Cost")]
        public double? EstimatedCost { get; set; }

        public string? Notes { get; set; }
    }

    public class CompleteMaintenanceRequestViewModel
    {
        public string RequestId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Actual Cost")]
        public double? ActualCost { get; set; }

        [Required]
        [StringLength(2000)]
        [Display(Name = "Resolution Notes")]
        public string ResolutionNotes { get; set; } = string.Empty;

    }
}

