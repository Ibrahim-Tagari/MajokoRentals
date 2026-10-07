namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Service for managing files in offline mode
    /// </summary>
    public interface IOfflineFileService
    {
        /// <summary>
        /// Saves a file locally for offline storage
        /// </summary>
        /// <param name="fileStream">File stream to save</param>
        /// <param name="fileName">Name of the file</param>
        /// <param name="category">Category (maintenance, payment, etc.)</param>
        /// <returns>Local file path</returns>
        Task<string> SaveFileLocallyAsync(Stream fileStream, string fileName, string category);

        /// <summary>
        /// Gets a file stream from local storage
        /// </summary>
        /// <param name="localPath">Local file path</param>
        /// <returns>File stream</returns>
        Task<Stream?> GetFileStreamAsync(string localPath);

        /// <summary>
        /// Checks if a file exists locally
        /// </summary>
        /// <param name="localPath">Local file path</param>
        /// <returns>True if file exists</returns>
        Task<bool> FileExistsAsync(string localPath);

        /// <summary>
        /// Deletes a local file
        /// </summary>
        /// <param name="localPath">Local file path</param>
        Task DeleteFileAsync(string localPath);

        /// <summary>
        /// Gets file size in bytes
        /// </summary>
        /// <param name="localPath">Local file path</param>
        /// <returns>File size in bytes</returns>
        Task<long> GetFileSizeAsync(string localPath);

        /// <summary>
        /// Copies a file from device to app storage
        /// </summary>
        /// <param name="sourceFilePath">Source file path from device</param>
        /// <param name="category">Category (maintenance, payment, etc.)</param>
        /// <returns>Local app file path</returns>
        Task<string> CopyFileToAppStorageAsync(string sourceFilePath, string category);

        /// <summary>
        /// Gets all pending files that need to be uploaded
        /// </summary>
        /// <param name="category">Optional category filter</param>
        /// <returns>List of file paths</returns>
        Task<List<string>> GetPendingFilesAsync(string? category = null);
    }
}
