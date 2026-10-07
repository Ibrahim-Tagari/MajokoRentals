using MajokoRentals.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.DTOs
{
    public class InvoiceDto
    {
        public string Id { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public long LeaseId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public double Amount { get; set; }
        public double AmountPaid { get; set; }
        public double AmountDue { get; set; }
        public PaymentStatus Status { get; set; }
        public bool IsOverdue { get; set; }
        public int DaysOverdue { get; set; }
        public string? Description { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PaymentDto> Payments { get; set; } = new();
    }

    public class PaymentDto
    {
        public long Id { get; set; }
        public long InvoiceId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public double Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? ProofOfPaymentUrl { get; set; }
    }

    public class RecordPaymentDto
    {
        [Required]
        public string InvoiceId { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        public double Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }
}

