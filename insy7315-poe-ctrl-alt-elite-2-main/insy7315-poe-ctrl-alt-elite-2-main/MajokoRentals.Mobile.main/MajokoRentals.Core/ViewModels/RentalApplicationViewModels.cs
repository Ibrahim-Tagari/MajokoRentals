
using System.ComponentModel.DataAnnotations;
using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.ViewModels
{
    public class RentalApplicationListViewModel
    {
        public long Id { get; set; }
        public string ApplicantName { get; set; } = string.Empty;
        public string ApplicantEmail { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public DateTime ApplicationDate { get; set; }
        public ApplicationStatus Status { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public int DaysPending => (DateTime.UtcNow - ApplicationDate).Days;
    }

    public class RentalApplicationDetailsViewModel
    {
        public long Id { get; set; }
        public long PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public double PropertyRent { get; set; }

        // Applicant Information
        public string ApplicantId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string IdNumber { get; set; } = string.Empty;

        // Employment Information
        public string? EmploymentStatus { get; set; }
        public string? EmployerName { get; set; }
        public double? MonthlyIncome { get; set; }

        // Current Address
        public string? CurrentAddress { get; set; }

        // Application Details
        public DateTime PreferredMoveInDate { get; set; }
        public int NumberOfOccupants { get; set; }
        public bool HasPets { get; set; }
        public string? PetDetails { get; set; }
        public string? AdditionalInformation { get; set; }

        // Documents
        public string? IdDocumentUrl { get; set; }
        public string? ProofOfIncomeUrl { get; set; }
        public string? BankStatementUrl { get; set; }
        public string? ReferenceLetterUrl { get; set; }

        // Status
        public ApplicationStatus Status { get; set; }
        public DateTime ApplicationDate { get; set; }
        public string? ReviewedBy { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string? ReviewNotes { get; set; }
        public string? RejectionReason { get; set; }

        public int DaysPending => (DateTime.UtcNow - ApplicationDate).Days;
        public string FullName => $"{FirstName} {LastName}";
    }

    public class CreateRentalApplicationViewModel
    {
        [Required]
        [Display(Name = "Property")]
        public long PropertyId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(13)]
        [Display(Name = "ID/Passport Number")]
        public string IdNumber { get; set; } = string.Empty;

        [Display(Name = "Current Address")]
        public string? CurrentAddress { get; set; }

        // Employment Information
        [Display(Name = "Employment Status")]
        public string? EmploymentStatus { get; set; }

        [Display(Name = "Employer Name")]
        public string? EmployerName { get; set; }

        [Display(Name = "Monthly Income (R)")]
        [Range(0, double.MaxValue)]
        public double? MonthlyIncome { get; set; }

        // Application Details
        [Required]
        [Display(Name = "Preferred Move-in Date")]
        [DataType(DataType.Date)]
        public DateTime PreferredMoveInDate { get; set; } = DateTime.UtcNow.AddDays(30);

        [Required]
        [Range(1, 20)]
        [Display(Name = "Number of Occupants")]
        public int NumberOfOccupants { get; set; } = 1;

        [Display(Name = "Do you have pets?")]
        public bool HasPets { get; set; }

        [Display(Name = "Pet Details (type, breed, size)")]
        public string? PetDetails { get; set; }

        [Display(Name = "Additional Information or Comments")]
        [DataType(DataType.MultilineText)]
        public string? AdditionalInformation { get; set; }

        // Note: File uploads handled via API - IFormFile removed for mobile compatibility
    }

    public class ReviewApplicationViewModel
    {
        public long ApplicationId { get; set; }
        public string ApplicantName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Decision")]
        public ApplicationStatus Status { get; set; }

        [Display(Name = "Review Notes")]
        [DataType(DataType.MultilineText)]
        public string? ReviewNotes { get; set; }

        [Display(Name = "Reason for Rejection")]
        [DataType(DataType.MultilineText)]
        public string? RejectionReason { get; set; }
    }
}

