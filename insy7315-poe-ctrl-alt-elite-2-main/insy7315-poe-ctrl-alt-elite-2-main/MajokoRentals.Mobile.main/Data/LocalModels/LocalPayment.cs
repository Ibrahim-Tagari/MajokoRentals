using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Mobile.Data.LocalModels
{
    /// <summary>
    /// Simplified local version of Payment for offline storage
    /// </summary>
    public class LocalPayment
    {
        [Key]
        public int Id { get; set; }

        // Foreign Keys (simplified)
        [Required]
        public int InvoiceId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        // Payment details
        [Required]
        public double Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "EFT";

        // Local file path for proof of payment (before upload)
        public string? LocalProofOfPaymentPath { get; set; }

        // Cloud URL (after upload)
        public string? ProofOfPaymentUrl { get; set; }

        // Verification status
        public bool IsVerified { get; set; } = false;
        public DateTime? VerifiedDate { get; set; }
        public string? VerifiedBy { get; set; }

        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Sync tracking
        public bool IsSynced { get; set; } = false;
        public DateTime? LastSyncedAt { get; set; }
        public DateTime CreatedLocally { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedLocally { get; set; } = DateTime.UtcNow;

        // Server version tracking for conflict detection
        public DateTime? ServerLastModified { get; set; }
    }
}
