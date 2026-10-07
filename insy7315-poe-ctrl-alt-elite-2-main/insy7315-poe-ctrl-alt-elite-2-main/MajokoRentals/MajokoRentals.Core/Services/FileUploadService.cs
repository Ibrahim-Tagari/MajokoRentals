using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MajokoRentals.Core.Data;
using MajokoRentals.Core.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace MajokoRentals.Core.Services
{
    public class FileUploadService : IFileUploadService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUploadService> _logger;
        private readonly Cloudinary? _cloudinary;

        public FileUploadService(IConfiguration configuration, IWebHostEnvironment environment, ILogger<FileUploadService> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;

            // Initialize Cloudinary
            var cloudName = _configuration["Cloudinary:CloudName"];
            var apiKey = _configuration["Cloudinary:ApiKey"];
            var apiSecret = _configuration["Cloudinary:ApiSecret"];

            if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            {
                _logger.LogWarning("Cloudinary credentials not configured");
            }
            else
            {
                var account = new Account(cloudName, apiKey, apiSecret);
                _cloudinary = new Cloudinary(account);
                _cloudinary.Api.Secure = true;
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or null");

            if (!IsValidFile(file))
                throw new ArgumentException("Invalid file type or size");

            if (_cloudinary == null)
                throw new InvalidOperationException("Cloudinary is not configured. Please add Cloudinary credentials to appsettings.json");

            try
            {
                using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"majoko-rentals/{folder}",
                    Transformation = new Transformation()
                        .Width(1200)
                        .Height(800)
                        .Crop("limit")
                        .Quality("auto")
                        .FetchFormat("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.LogInformation($"File uploaded successfully to Cloudinary: {uploadResult.SecureUrl}");
                    return uploadResult.SecureUrl.ToString();
                }
                else
                {
                    _logger.LogError($"Cloudinary upload failed: {uploadResult.Error?.Message}");
                    throw new Exception($"Failed to upload to Cloudinary: {uploadResult.Error?.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to Cloudinary");
                throw;
            }
        }

        public async Task<List<string>> UploadMultipleFilesAsync(List<IFormFile> files, string folder)
        {
            var uploadedFiles = new List<string>();

            foreach (var file in files)
            {
                var filePath = await UploadFileAsync(file, folder);
                uploadedFiles.Add(filePath);
            }

            return uploadedFiles;
        }

        public async Task<bool> DeleteFileAsync(string fileUrl)
        {
            if (_cloudinary == null)
                return false;

            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return false;

                // Extract public ID from Cloudinary URL
                // URL format: https://res.cloudinary.com/{cloud_name}/image/upload/v{version}/{public_id}.{format}
                var uri = new Uri(fileUrl);
                var segments = uri.AbsolutePath.Split('/');

                // Find the public_id (everything after /upload/)
                var uploadIndex = Array.IndexOf(segments, "upload");
                if (uploadIndex >= 0 && uploadIndex < segments.Length - 1)
                {
                    var publicIdParts = segments.Skip(uploadIndex + 2).ToArray(); // Skip upload and version
                    var publicId = string.Join("/", publicIdParts);

                    // Remove file extension
                    publicId = Path.ChangeExtension(publicId, null);

                    var deletionParams = new DeletionParams(publicId);
                    var result = await _cloudinary.DestroyAsync(deletionParams);

                    if (result.Result == "ok")
                    {
                        _logger.LogInformation($"File deleted successfully from Cloudinary: {publicId}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete file from Cloudinary: {fileUrl}");
                return false;
            }
        }

        public bool IsValidFile(IFormFile file)
        {
            var maxSizeMB = _configuration.GetValue<int>("FileStorage:MaxFileSizeMB");
            var allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>();

            var extension = Path.GetExtension(file.FileName).ToLower();

            if (file.Length > maxSizeMB * 1024 * 1024)
            {
                _logger.LogWarning($"File {file.FileName} exceeds maximum size of {maxSizeMB}MB");
                return false;
            }

            if (allowedExtensions != null && !allowedExtensions.Contains(extension))
            {
                _logger.LogWarning($"File extension {extension} is not allowed");
                return false;
            }

            return true;
        }
    }
}


