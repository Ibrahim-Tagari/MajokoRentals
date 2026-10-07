using Microsoft.EntityFrameworkCore;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Services;
using MajokoRentals.Mobile.Data;
using MajokoRentals.Mobile.Data.LocalModels;

namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Implementation of data caching service
    /// </summary>
    public class DataCacheService : IDataCacheService
    {
        private readonly LocalDbContext _localDb;
        private readonly IApiClientService _apiClient;
        private readonly IConnectivityService _connectivityService;

        public DataCacheService(
            LocalDbContext localDb,
            IApiClientService apiClient,
            IConnectivityService connectivityService)
        {
            _localDb = localDb;
            _apiClient = apiClient;
            _connectivityService = connectivityService;
        }

        public async Task CachePropertiesAsync()
        {
            try
            {
                if (!_connectivityService.IsConnected)
                    return;

                // Fetch properties from API
                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");

                if (properties == null || !properties.Any())
                    return;

                // Clear existing cache
                await _localDb.Properties.ExecuteDeleteAsync();

                // Cache new data
                foreach (var property in properties)
                {
                    var localProperty = new LocalProperty
                    {
                        Id = property.Id,
                        Name = property.Name,
                        Address = property.Address,
                        City = property.City,
                        Province = string.Empty, // Not in PropertyListDto
                        PostalCode = string.Empty, // Not in PropertyListDto
                        PropertyType = string.Empty, // Not in PropertyListDto
                        Bedrooms = property.Bedrooms,
                        Bathrooms = (int)property.Bathrooms,
                        RentalPrice = property.MonthlyRent,
                        IsActive = property.Status != MajokoRentals.Core.Models.PropertyStatus.Archived,
                        CachedAt = DateTime.UtcNow
                    };

                    _localDb.Properties.Add(localProperty);
                }

                await _localDb.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error caching properties: {ex.Message}");
                // Don't throw - caching is optional
            }
        }

        public async Task<List<CachedPropertyDto>> GetCachedPropertiesAsync()
        {
            try
            {
                var properties = await _localDb.Properties
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .Select(p => new CachedPropertyDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Address = p.Address
                    })
                    .ToListAsync();

                return properties;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting cached properties: {ex.Message}");
                return new List<CachedPropertyDto>();
            }
        }

        public async Task<bool> IsPropertyCacheFreshAsync()
        {
            try
            {
                var oldestCache = await _localDb.Properties
                    .OrderBy(p => p.CachedAt)
                    .Select(p => p.CachedAt)
                    .FirstOrDefaultAsync();

                if (oldestCache == default)
                    return false;

                // Cache is fresh if less than 24 hours old
                return (DateTime.UtcNow - oldestCache).TotalHours < 24;
            }
            catch
            {
                return false;
            }
        }

        public async Task ClearCacheAsync()
        {
            try
            {
                await _localDb.Properties.ExecuteDeleteAsync();
                await _localDb.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing cache: {ex.Message}");
            }
        }
    }
}
