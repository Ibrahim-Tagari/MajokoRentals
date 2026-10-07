using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MajokoRentals.Core.Models
{
    public class RentalApplication
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public long PropertyId { get; set; }

        [ForeignKey("PropertyId")]
        public virtual Property Property { get; set; } = null!;

        [Required]
        public string ApplicantId { get; set; } = string.Empty;

        [ForeignKey("ApplicantId")]
        public virtual ApplicationUser Applicant { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string IdNumber { get; set; } = string.Empty;

        public string? CurrentAddress { get; set; }

        public string? EmploymentStatus { get; set; }

        public string? EmployerName { get; set; }

        public double? MonthlyIncome { get; set; }

        public int NumberOfOccupants { get; set; } = 1;

        public bool HasPets { get; set; }

        public string? PetDetails { get; set; }

        // Document URLs
        public string? IdDocumentUrl { get; set; }
        public string? ProofOfIncomeUrl { get; set; }
        public string? BankStatementUrl { get; set; }
        public string? ReferenceLetterUrl { get; set; }

        public DateTime PreferredMoveInDate { get; set; }

        public string? AdditionalInformation { get; set; }

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

        public DateTime ApplicationDate { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedDate { get; set; }

        public string? ReviewedBy { get; set; }

        public string? ReviewNotes { get; set; }

        public string? RejectionReason { get; set; }
    }
}

