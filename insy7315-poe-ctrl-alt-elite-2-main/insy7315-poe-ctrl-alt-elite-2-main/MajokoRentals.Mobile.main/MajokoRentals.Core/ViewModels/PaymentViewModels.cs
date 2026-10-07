using MajokoRentals.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.ViewModels
{
    public class InvoiceListViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
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
    }

    public class InvoiceDetailsViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public long LeaseId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string TenantEmail { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public double Amount { get; set; }
        public double AmountPaid { get; set; }
        public double AmountDue { get; set; }
        public PaymentStatus Status { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public DateTime? PaidDate { get; set; }
        public bool IsOverdue { get; set; }
        public int DaysOverdue { get; set; }
        public List<Payment> Payments { get; set; } = new();
    }

    public class RecordPaymentViewModel
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public double InvoiceAmount { get; set; }
        public double AmountAlreadyPaid { get; set; }
        public double AmountDue { get; set; }

        [Required]
        [Display(Name = "Payment Amount")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Payment amount must be greater than 0")]
        public double Amount { get; set; }

        [Required]
        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Bank Transfer";

        [Display(Name = "Transaction Reference")]
        public string? TransactionReference { get; set; }

        [Display(Name = "Payment Proof URL")]
        public string? PaymentProofUrl { get; set; }

        // Note: File uploads handled via API - IFormFile removed for mobile compatibility

        public string? Notes { get; set; }
    }

    public class PaymentHistoryViewModel
    {
        public string TenantName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public List<InvoiceListViewModel> Invoices { get; set; } = new();
        public double TotalPaid { get; set; }
        public double TotalOutstanding { get; set; }
        public int OverdueInvoices { get; set; }
    }

    public class GenerateInvoicesViewModel
    {
        [Required]
        [Display(Name = "Billing Month")]
        [DataType(DataType.Date)]
        public DateTime BillingMonth { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        [Display(Name = "Invoice Date")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(7);

        [Display(Name = "Active Leases Only")]
        public bool ActiveLeasesOnly { get; set; } = true;
    }
}

