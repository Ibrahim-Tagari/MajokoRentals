
using MajokoRentals.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.ViewModels
{
    public class LeaseListViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string TenantEmail { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double MonthlyRent { get; set; }
        public LeaseStatus Status { get; set; }
        public int DaysUntilExpiry { get; set; }
        public bool IsExpiringSoon { get; set; }
        public bool IsExpired { get; set; }
    }

    public class LeaseDetailsViewModel
    {
        public string Id { get; set; } = string.Empty;
        public long PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string TenantEmail { get; set; } = string.Empty;
        public string TenantPhone { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double MonthlyRent { get; set; }
        public double SecurityDeposit { get; set; }
        public LeaseStatus Status { get; set; }
        public string? LeaseDocumentUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DaysUntilExpiry { get; set; }
        public bool IsExpiringSoon { get; set; }
        public bool IsExpired { get; set; }
        public List<Invoice> Invoices { get; set; } = new();
        public List<MaintenanceRequest> MaintenanceRequests { get; set; } = new();
    }

    public class CreateLeaseViewModel
    {
        [Required]
        [Display(Name = "Property")]
        public long PropertyId { get; set; }

        [Required]
        [Display(Name = "Tenant")]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1);

        [Required]
        [Display(Name = "Monthly Rent")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Monthly rent must be greater than 0")]
        public double MonthlyRent { get; set; }

        [Required]
        [Display(Name = "Security Deposit")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Security deposit must be greater than 0")]
        public double SecurityDeposit { get; set; }

        [Display(Name = "Lease Document")]

        public LeaseStatus Status { get; set; } = LeaseStatus.Active;
    }

    public class EditLeaseViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Property")]
        public long PropertyId { get; set; }

        [Required]
        [Display(Name = "Tenant")]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Required]
        [Display(Name = "Monthly Rent")]
        [Range(0.01, double.MaxValue)]
        public double MonthlyRent { get; set; }

        [Required]
        [Display(Name = "Security Deposit")]
        [Range(0.01, double.MaxValue)]
        public double SecurityDeposit { get; set; }

        public string? ExistingLeaseDocumentUrl { get; set; }


        public LeaseStatus Status { get; set; }
    }

    public class RenewLeaseViewModel
    {
        public string LeaseId { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public DateTime CurrentEndDate { get; set; }
        public double CurrentMonthlyRent { get; set; }

        [Required]
        [Display(Name = "New Start Date")]
        [DataType(DataType.Date)]
        public DateTime NewStartDate { get; set; }

        [Required]
        [Display(Name = "New End Date")]
        [DataType(DataType.Date)]
        public DateTime NewEndDate { get; set; }

        [Required]
        [Display(Name = "Monthly Rent")]
        [Range(0.01, double.MaxValue)]
        public double MonthlyRent { get; set; }

    }

    public class TerminateLeaseViewModel
    {
        public string LeaseId { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double MonthlyRent { get; set; }

        [Required]
        [Display(Name = "Termination Date")]
        [DataType(DataType.Date)]
        public DateTime TerminationDate { get; set; }

        [Display(Name = "Reason for Termination")]
        [DataType(DataType.MultilineText)]
        public string? Reason { get; set; }
    }
}

