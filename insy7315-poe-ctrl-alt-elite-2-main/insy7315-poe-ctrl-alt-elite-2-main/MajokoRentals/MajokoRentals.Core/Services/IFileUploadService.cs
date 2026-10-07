using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MajokoRentals.Core.Data;
using MajokoRentals.Core.Models;
namespace MajokoRentals.Core.Services
{
    public interface IFileUploadService
    {
        Task<string> UploadFileAsync(IFormFile file, string folder);
        Task<List<string>> UploadMultipleFilesAsync(List<IFormFile> files, string folder);
        Task<bool> DeleteFileAsync(string filePath);
        bool IsValidFile(IFormFile file);
    }
}


