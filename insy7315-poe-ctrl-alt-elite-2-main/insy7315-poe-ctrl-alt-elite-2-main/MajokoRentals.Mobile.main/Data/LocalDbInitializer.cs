using Microsoft.EntityFrameworkCore;

namespace MajokoRentals.Mobile.Data
{
    /// <summary>
    /// Initializes and manages the local SQLite database
    /// </summary>
    public static class LocalDbInitializer
    {
        private static bool _initialized = false;

        /// <summary>
        /// Ensures the database is created and ready to use
        /// </summary>
        public static async Task InitializeAsync(LocalDbContext context)
        {
            if (_initialized)
                return;

            try
            {
                // Create database if it doesn't exist
                await context.Database.EnsureCreatedAsync();

                _initialized = true;
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error initializing local database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Clears all local data (useful for testing or logout)
        /// </summary>
        public static async Task ClearAllDataAsync(LocalDbContext context)
        {
            await context.MaintenanceRequests.ExecuteDeleteAsync();
            await context.Payments.ExecuteDeleteAsync();
            await context.SyncQueue.ExecuteDeleteAsync();
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Gets database file path
        /// </summary>
        public static string GetDatabasePath()
        {
            var appDataPath = FileSystem.AppDataDirectory;
            return Path.Combine(appDataPath, "majoko_local.db");
        }
    }
}
