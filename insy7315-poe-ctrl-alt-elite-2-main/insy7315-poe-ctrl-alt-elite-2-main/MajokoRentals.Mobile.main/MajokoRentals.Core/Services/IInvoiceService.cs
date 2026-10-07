using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.Services
{
    public interface IInvoiceService
    {
        Task<Invoice> GenerateMonthlyInvoiceAsync(int leaseId, DateTime month);
        Task<List<Invoice>> GenerateAllMonthlyInvoicesAsync(DateTime month);
        Task UpdateInvoiceStatusAsync(int invoiceId);
        Task<List<Invoice>> GetOverdueInvoicesAsync();
        Task SendInvoiceReminderAsync(int invoiceId);
    }
}

