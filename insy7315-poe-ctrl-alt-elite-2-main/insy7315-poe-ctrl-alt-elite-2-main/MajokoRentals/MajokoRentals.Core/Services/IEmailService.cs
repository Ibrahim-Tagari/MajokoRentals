using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MajokoRentals.Core.Data;
using MajokoRentals.Core.Models;
namespace MajokoRentals.Core.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task SendEmailWithAttachmentAsync(string to, string subject, string body, string attachmentPath);
        Task SendBulkEmailAsync(List<string> recipients, string subject, string body);
    }
}


