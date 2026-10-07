namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Service for caching data for offline use
    /// </summary>
    public interface IDataCacheService
    {
        /// <summary>
        /// Caches properties from API for offline use
        /// </summary>
        Task CachePropertiesAsync();

        /// <summary>
        /// Gets cached properties (works offline)
        /// </summary>
        Task<List<CachedPropertyDto>> GetCachedPropertiesAsync();

        /// <summary>
        /// Checks if properties cache is fresh (less than 24 hours old)
        /// </summary>
        Task<bool> IsPropertyCacheFreshAsync();

        /// <summary>
        /// Clears all cached data
        /// </summary>
        Task ClearCacheAsync();
    }

    public class CachedPropertyDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string DisplayText => $"{Name} - {Address}";
    }
}
