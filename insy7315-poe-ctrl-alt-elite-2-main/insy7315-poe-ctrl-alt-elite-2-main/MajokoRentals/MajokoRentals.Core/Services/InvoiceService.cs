using MajokoRentals.Core.Data;
using MajokoRentals.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MajokoRentals.Core.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ILogger<InvoiceService> _logger;

        public InvoiceService(ApplicationDbContext context, IEmailService emailService, IEmailTemplateService emailTemplateService, ILogger<InvoiceService> logger)
        {
            _context = context;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _logger = logger;
        }

        public async Task<Invoice> GenerateMonthlyInvoiceAsync(int leaseId, DateTime month)
        {
            var lease = await _context.Leases
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.Id == leaseId);

            if (lease == null || lease.Status != LeaseStatus.Active)
                throw new ArgumentException("Invalid or inactive lease");

            // Check if invoice already exists for this month
            var existingInvoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.LeaseId == leaseId &&
                    i.InvoiceDate.Month == month.Month &&
                    i.InvoiceDate.Year == month.Year);

            if (existingInvoice != null)
                return existingInvoice;

            var invoice = new Invoice
            {
                LeaseId = leaseId,
                InvoiceNumber = GenerateInvoiceNumber(),
                InvoiceDate = month,
                DueDate = new DateTime(month.Year, month.Month, 5), // Due on 5th of month
                Amount = lease.MonthlyRent,
                Description = $"Monthly Rent - {lease.Property.Name}",
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Invoice {invoice.InvoiceNumber} generated for lease {leaseId}");

            // Send email notification using professional template
            try
            {
                var emailBody = _emailTemplateService.GetInvoiceGeneratedEmailTemplate(
                    lease.Tenant.FullName,
                    lease.Property.Name,
                    invoice.InvoiceNumber,
                    (decimal)invoice.Amount,
                    invoice.DueDate
                );

                await _emailService.SendEmailAsync(lease.Tenant.Email!, "New Invoice Generated", emailBody, isHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send invoice email: {ex.Message}");
            }

            return invoice;
        }

        public async Task<List<Invoice>> GenerateAllMonthlyInvoicesAsync(DateTime month)
        {
            var activeLeases = await _context.Leases
                .Where(l => l.Status == LeaseStatus.Active)
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .ToListAsync();

            var invoices = new List<Invoice>();

            foreach (var lease in activeLeases)
            {
                try
                {
                    var invoice = await GenerateMonthlyInvoiceAsync(lease.Id, month);
                    invoices.Add(invoice);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to generate invoice for lease {lease.Id}: {ex.Message}");
                }
            }

            return invoices;
        }

        public async Task UpdateInvoiceStatusAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null) return;

            invoice.AmountPaid = invoice.Payments.Where(p => p.IsVerified).Sum(p => p.Amount);

            if (invoice.AmountPaid >= invoice.Amount)
            {
                invoice.Status = PaymentStatus.Paid;
                invoice.PaidDate = DateTime.UtcNow;
            }
            else if (invoice.AmountPaid > 0)
            {
                invoice.Status = PaymentStatus.PartiallyPaid;
            }
            else if (invoice.DueDate < DateTime.UtcNow)
            {
                invoice.Status = PaymentStatus.Overdue;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<Invoice>> GetOverdueInvoicesAsync()
        {
            return await _context.Invoices
                .Include(i => i.Lease)
                .ThenInclude(l => l.Tenant)
                .Where(i => i.DueDate < DateTime.UtcNow && i.Status != PaymentStatus.Paid)
                .ToListAsync();
        }

        public async Task SendInvoiceReminderAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Lease)
                .ThenInclude(l => l.Tenant)
                .Include(i => i.Lease.Property)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null) return;

            var emailBody = _emailTemplateService.GetPaymentReminderEmailTemplate(
                invoice.Lease.Tenant.FullName,
                invoice.InvoiceNumber,
                (decimal)invoice.AmountDue,
                invoice.DueDate,
                invoice.IsOverdue,
                invoice.DaysOverdue
            );

            await _emailService.SendEmailAsync(invoice.Lease.Tenant.Email!, "Rent Payment Reminder", emailBody, isHtml: true);
        }

        private string GenerateInvoiceNumber()
        {
            return $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        }
    }
}

