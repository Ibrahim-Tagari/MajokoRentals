using System.Text.Json;

namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Implementation of offline file storage service
    /// </summary>
    public class OfflineFileService : IOfflineFileService
    {
        private const string OfflineFilesFolder = "OfflineFiles";
        private const string FileMetadataFileName = "file_metadata.json";

        public OfflineFileService()
        {
            InitializeStorageAsync().Wait();
        }

        private async Task InitializeStorageAsync()
        {
            var basePath = GetOfflineFilesPath();
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            // Create category subfolders
            var categories = new[] { "maintenance", "payment", "profile", "temp" };
            foreach (var category in categories)
            {
                var categoryPath = Path.Combine(basePath, category);
                if (!Directory.Exists(categoryPath))
                {
                    Directory.CreateDirectory(categoryPath);
                }
            }

            await Task.CompletedTask;
        }

        private string GetOfflineFilesPath()
        {
            return Path.Combine(FileSystem.AppDataDirectory, OfflineFilesFolder);
        }

        public async Task<string> SaveFileLocallyAsync(Stream fileStream, string fileName, string category)
        {
            try
            {
                // Sanitize filename
                var safeFileName = SanitizeFileName(fileName);

                // Add timestamp to make unique
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var uniqueFileName = $"{timestamp}_{safeFileName}";

                // Get category path
                var categoryPath = Path.Combine(GetOfflineFilesPath(), category.ToLower());
                if (!Directory.Exists(categoryPath))
                {
                    Directory.CreateDirectory(categoryPath);
                }

                // Full file path
                var filePath = Path.Combine(categoryPath, uniqueFileName);

                // Save file
                using (var fileStreamOutput = File.Create(filePath))
                {
                    await fileStream.CopyToAsync(fileStreamOutput);
                }

                // Save metadata
                await SaveFileMetadataAsync(filePath, fileName, category);

                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving file locally: {ex.Message}");
                throw;
            }
        }

        public async Task<Stream?> GetFileStreamAsync(string localPath)
        {
            try
            {
                if (!File.Exists(localPath))
                    return null;

                var fileStream = File.OpenRead(localPath);
                return await Task.FromResult(fileStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> FileExistsAsync(string localPath)
        {
            return await Task.FromResult(File.Exists(localPath));
        }

        public async Task DeleteFileAsync(string localPath)
        {
            try
            {
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }

                await RemoveFileMetadataAsync(localPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting file: {ex.Message}");
                throw;
            }
        }

        public async Task<long> GetFileSizeAsync(string localPath)
        {
            try
            {
                if (!File.Exists(localPath))
                    return 0;

                var fileInfo = new FileInfo(localPath);
                return await Task.FromResult(fileInfo.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting file size: {ex.Message}");
                return 0;
            }
        }

        public async Task<string> CopyFileToAppStorageAsync(string sourceFilePath, string category)
        {
            try
            {
                if (!File.Exists(sourceFilePath))
                    throw new FileNotFoundException("Source file not found", sourceFilePath);

                using (var sourceStream = File.OpenRead(sourceFilePath))
                {
                    var fileName = Path.GetFileName(sourceFilePath);
                    return await SaveFileLocallyAsync(sourceStream, fileName, category);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying file to app storage: {ex.Message}");
                throw;
            }
        }

        public async Task<List<string>> GetPendingFilesAsync(string? category = null)
        {
            try
            {
                var metadata = await LoadFileMetadataAsync();

                var pendingFiles = metadata
                    .Where(m => !m.IsUploaded)
                    .Where(m => string.IsNullOrEmpty(category) || m.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .Select(m => m.FilePath)
                    .ToList();

                return pendingFiles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting pending files: {ex.Message}");
                return new List<string>();
            }
        }

        // Helper methods for file metadata tracking
        private async Task SaveFileMetadataAsync(string filePath, string originalFileName, string category)
        {
            try
            {
                var metadata = await LoadFileMetadataAsync();

                metadata.Add(new FileMetadata
                {
                    FilePath = filePath,
                    OriginalFileName = originalFileName,
                    Category = category,
                    CreatedAt = DateTime.UtcNow,
                    IsUploaded = false
                });

                await SaveMetadataListAsync(metadata);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving file metadata: {ex.Message}");
            }
        }

        private async Task RemoveFileMetadataAsync(string filePath)
        {
            try
            {
                var metadata = await LoadFileMetadataAsync();
                metadata.RemoveAll(m => m.FilePath == filePath);
                await SaveMetadataListAsync(metadata);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing file metadata: {ex.Message}");
            }
        }

        public async Task MarkFileAsUploadedAsync(string filePath, string cloudUrl)
        {
            try
            {
                var metadata = await LoadFileMetadataAsync();
                var fileMetadata = metadata.FirstOrDefault(m => m.FilePath == filePath);

                if (fileMetadata != null)
                {
                    fileMetadata.IsUploaded = true;
                    fileMetadata.CloudUrl = cloudUrl;
                    fileMetadata.UploadedAt = DateTime.UtcNow;
                    await SaveMetadataListAsync(metadata);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking file as uploaded: {ex.Message}");
            }
        }

        private async Task<List<FileMetadata>> LoadFileMetadataAsync()
        {
            try
            {
                var metadataPath = Path.Combine(GetOfflineFilesPath(), FileMetadataFileName);

                if (!File.Exists(metadataPath))
                    return new List<FileMetadata>();

                var json = await File.ReadAllTextAsync(metadataPath);
                return JsonSerializer.Deserialize<List<FileMetadata>>(json) ?? new List<FileMetadata>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading file metadata: {ex.Message}");
                return new List<FileMetadata>();
            }
        }

        private async Task SaveMetadataListAsync(List<FileMetadata> metadata)
        {
            try
            {
                var metadataPath = Path.Combine(GetOfflineFilesPath(), FileMetadataFileName);
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metadataPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving metadata list: {ex.Message}");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized;
        }

        // File metadata model
        private class FileMetadata
        {
            public string FilePath { get; set; } = string.Empty;
            public string OriginalFileName { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public bool IsUploaded { get; set; }
            public string? CloudUrl { get; set; }
            public DateTime? UploadedAt { get; set; }
        }
    }
}
