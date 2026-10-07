using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Mobile.Data.LocalModels
{
    /// <summary>
    /// Tracks pending operations that need to be synced with the server
    /// </summary>
    public class SyncQueueItem
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Type of entity (MaintenanceRequest, Payment, etc.)
        /// </summary>
        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// Local ID of the entity
        /// </summary>
        [Required]
        public int EntityId { get; set; }

        /// <summary>
        /// Operation type (Create, Update, Delete)
        /// </summary>
        [Required]
        public SyncOperation Operation { get; set; }

        /// <summary>
        /// JSON serialized entity data
        /// </summary>
        public string? EntityData { get; set; }

        /// <summary>
        /// When the operation was queued
        /// </summary>
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of sync attempts
        /// </summary>
        public int AttemptCount { get; set; } = 0;

        /// <summary>
        /// Last error message if sync failed
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Last time sync was attempted
        /// </summary>
        public DateTime? LastAttemptAt { get; set; }

        /// <summary>
        /// Priority (higher = more urgent)
        /// </summary>
        public int Priority { get; set; } = 0;
    }

    public enum SyncOperation
    {
        Create,
        Update,
        Delete
    }
}
