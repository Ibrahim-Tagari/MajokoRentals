using MajokoRentals.Mobile.Data.LocalModels;

namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Service for synchronizing local data with the server
    /// </summary>
    public interface ISyncService
    {
        /// <summary>
        /// Synchronizes all pending changes with the server
        /// </summary>
        /// <returns>Sync result with statistics</returns>
        Task<SyncResult> SyncAllAsync();

        /// <summary>
        /// Syncs pending maintenance requests
        /// </summary>
        Task<SyncResult> SyncMaintenanceRequestsAsync();

        /// <summary>
        /// Syncs pending payments
        /// </summary>
        Task<SyncResult> SyncPaymentsAsync();

        /// <summary>
        /// Checks if there are pending changes to sync
        /// </summary>
        Task<bool> HasPendingChangesAsync();

        /// <summary>
        /// Gets count of pending items by type
        /// </summary>
        Task<Dictionary<string, int>> GetPendingCountsAsync();

        /// <summary>
        /// Gets the last sync time
        /// </summary>
        Task<DateTime?> GetLastSyncTimeAsync();

        /// <summary>
        /// Event fired when sync progress changes
        /// </summary>
        event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;

        /// <summary>
        /// Event fired when sync completes
        /// </summary>
        event EventHandler<SyncResult>? SyncCompleted;
    }

    /// <summary>
    /// Result of a sync operation
    /// </summary>
    public class SyncResult
    {
        public bool Success { get; set; }
        public int ItemsSynced { get; set; }
        public int ItemsFailed { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<SyncConflict> Conflicts { get; set; } = new();
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a sync conflict
    /// </summary>
    public class SyncConflict
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Description { get; set; } = string.Empty;
        public object? LocalVersion { get; set; }
        public object? ServerVersion { get; set; }
    }

    /// <summary>
    /// Progress information for sync operations
    /// </summary>
    public class SyncProgressEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int Percentage => TotalItems > 0 ? (ProcessedItems * 100 / TotalItems) : 0;
    }
}
