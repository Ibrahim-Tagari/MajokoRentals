using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MajokoRentals.Core.Models
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LeaseId { get; set; }

        [ForeignKey("LeaseId")]
        public virtual Lease Lease { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public DateTime InvoiceDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        public double Amount { get; set; }

        public double AmountPaid { get; set; } = 0;

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PaidDate { get; set; }

        public string? Notes { get; set; }

        // Navigation properties
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // Calculated properties
        [NotMapped]
        public double AmountDue => Amount - AmountPaid;

        [NotMapped]
        public bool IsOverdue => DueDate < DateTime.UtcNow && Status != PaymentStatus.Paid;

        [NotMapped]
        public int DaysOverdue => IsOverdue ? (DateTime.UtcNow - DueDate).Days : 0;
    }
}

