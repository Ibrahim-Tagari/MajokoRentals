using MajokoRentals.Core.DTOs;

namespace MajokoRentals.Core.Services
{
    /// <summary>
    /// Service for managing broadcast announcements
    /// </summary>
    public interface IAnnouncementService
    {
        // Announcement Management
        Task<AnnouncementDto> CreateAnnouncementAsync(string createdById, CreateAnnouncementRequest request);
        Task<AnnouncementDto> UpdateAnnouncementAsync(string announcementId, UpdateAnnouncementRequest request);
        Task DeleteAnnouncementAsync(string announcementId);
        Task<AnnouncementDto?> GetAnnouncementByIdAsync(string announcementId);
        Task<List<AnnouncementDto>> GetAllAnnouncementsAsync(bool activeOnly = true);
        Task<List<AnnouncementDto>> GetAnnouncementsForUserAsync(string userId, string userRole, List<long>? propertyIds = null);
        Task<AnnouncementsSummary> GetAnnouncementsSummaryAsync(string userId, string userRole, List<long>? propertyIds = null);
        Task MarkAsViewedAsync(string announcementId, string userId);
    }
}
