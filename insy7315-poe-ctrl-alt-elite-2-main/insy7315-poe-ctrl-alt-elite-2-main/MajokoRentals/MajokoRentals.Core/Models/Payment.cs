using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MajokoRentals.Core.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InvoiceId { get; set; }

        [ForeignKey("InvoiceId")]
        public virtual Invoice Invoice { get; set; } = null!;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [ForeignKey("TenantId")]
        public virtual ApplicationUser Tenant { get; set; } = null!;

        [Required]
        public double Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "EFT"; // EFT, Cash, Card, etc.

        public string? ProofOfPaymentUrl { get; set; } // URL to uploaded document

        public bool IsVerified { get; set; } = false;

        public DateTime? VerifiedDate { get; set; }

        public string? VerifiedBy { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // For offline sync
        public bool IsSynced { get; set; } = true;
        public DateTime? LastSyncedAt { get; set; }
    }
}

