using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MajokoRentals.Core.Models
{
    public class Lease
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public long PropertyId { get; set; }

        [ForeignKey("PropertyId")]
        public virtual Property Property { get; set; } = null!;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [ForeignKey("TenantId")]
        public virtual ApplicationUser Tenant { get; set; } = null!;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public double MonthlyRent { get; set; }

        public double DepositAmount { get; set; }

        public DateTime? DepositPaidDate { get; set; }

        public LeaseStatus Status { get; set; } = LeaseStatus.Draft;

        public string? LeaseDocumentUrl { get; set; }

        public DateTime? SignedDate { get; set; }

        public string? SpecialTerms { get; set; }

        public bool AutoRenewal { get; set; } = false;

        public int RenewalNoticeDays { get; set; } = 30; // Days notice required

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public string? Notes { get; set; }

        // Navigation properties
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

        // Calculated properties
        public int DaysUntilExpiry => (EndDate - DateTime.UtcNow).Days;
        public bool IsExpiringSoon => DaysUntilExpiry <= 60 && DaysUntilExpiry > 0;
        public bool IsExpired => EndDate < DateTime.UtcNow;
    }
}

