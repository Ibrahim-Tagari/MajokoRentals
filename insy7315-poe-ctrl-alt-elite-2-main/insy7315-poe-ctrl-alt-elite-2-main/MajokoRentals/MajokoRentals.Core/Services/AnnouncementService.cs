using Google.Cloud.Firestore;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using Microsoft.Extensions.Logging;

namespace MajokoRentals.Core.Services
{
    public class AnnouncementService : IAnnouncementService
    {
        private readonly IFirestoreService _firestoreService;
        private readonly ILogger<AnnouncementService> _logger;
        private const string AnnouncementsCollection = "announcements";

        public AnnouncementService(
            IFirestoreService firestoreService,
            ILogger<AnnouncementService> logger)
        {
            _firestoreService = firestoreService;
            _logger = logger;
        }

        public async Task<AnnouncementDto> CreateAnnouncementAsync(string createdById, CreateAnnouncementRequest request)
        {
            try
            {
                // Get creator details
                var creator = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", createdById);
                string creatorName = "Unknown User";
                if (creator != null)
                {
                    var firstName = creator.ContainsKey("firstName") ? creator["firstName"]?.ToString() : "";
                    var lastName = creator.ContainsKey("lastName") ? creator["lastName"]?.ToString() : "";
                    creatorName = $"{firstName} {lastName}".Trim();
                    if (string.IsNullOrWhiteSpace(creatorName))
                    {
                        creatorName = creator.ContainsKey("email") ? creator["email"]?.ToString() ?? "Unknown User" : "Unknown User";
                    }
                }

                var announcement = new AnnouncementDocument
                {
                    Title = request.Title,
                    Content = request.Content,
                    Type = request.Type.ToString(),
                    Priority = (int)request.Priority,
                    CreatedBy = createdById,
                    CreatedByName = creatorName,
                    CreatedDate = Timestamp.GetCurrentTimestamp(),
                    ExpiryDate = request.ExpiryDate.HasValue ? Timestamp.FromDateTime(request.ExpiryDate.Value.ToUniversalTime()) : null,
                    IsActive = true,
                    ForAllTenants = request.ForAllTenants,
                    ForAllCaretakers = request.ForAllCaretakers,
                    ForAllManagers = request.ForAllManagers,
                    TargetPropertyIds = request.TargetPropertyIds,
                    ImageUrl = request.ImageUrl,
                    ViewedBy = new List<string>()
                };

                var docId = await _firestoreService.AddDocumentAsync(AnnouncementsCollection, announcement);
                announcement.Id = docId;

                _logger.LogInformation($"Announcement created: {docId} by {createdById}");

                return MapToDto(announcement, createdById);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating announcement");
                throw;
            }
        }

        public async Task<AnnouncementDto> UpdateAnnouncementAsync(string announcementId, UpdateAnnouncementRequest request)
        {
            try
            {
                var updates = new Dictionary<string, object>
                {
                    { "Title", request.Title },
                    { "Content", request.Content },
                    { "Type", request.Type.ToString() },
                    { "Priority", (int)request.Priority },
                    { "ExpiryDate", request.ExpiryDate.HasValue ? Timestamp.FromDateTime(request.ExpiryDate.Value.ToUniversalTime()) : null! },
                    { "IsActive", request.IsActive },
                    { "ForAllTenants", request.ForAllTenants },
                    { "ForAllCaretakers", request.ForAllCaretakers },
                    { "ForAllManagers", request.ForAllManagers },
                    { "TargetPropertyIds", request.TargetPropertyIds },
                    { "ImageUrl", request.ImageUrl ?? "" }
                };

                await _firestoreService.UpdateDocumentAsync(AnnouncementsCollection, announcementId, updates);

                var announcement = await _firestoreService.GetDocumentAsync<AnnouncementDocument>(AnnouncementsCollection, announcementId);
                return MapToDto(announcement!, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating announcement {announcementId}");
                throw;
            }
        }

        public async Task DeleteAnnouncementAsync(string announcementId)
        {
            try
            {
                await _firestoreService.DeleteDocumentAsync(AnnouncementsCollection, announcementId);
                _logger.LogInformation($"Announcement deleted: {announcementId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting announcement {announcementId}");
                throw;
            }
        }

        public async Task<AnnouncementDto?> GetAnnouncementByIdAsync(string announcementId)
        {
            try
            {
                var announcement = await _firestoreService.GetDocumentAsync<AnnouncementDocument>(AnnouncementsCollection, announcementId);
                return announcement != null ? MapToDto(announcement, "") : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting announcement {announcementId}");
                throw;
            }
        }

        public async Task<List<AnnouncementDto>> GetAllAnnouncementsAsync(bool activeOnly = true)
        {
            try
            {
                var announcements = await _firestoreService.GetCollectionAsync<AnnouncementDocument>(AnnouncementsCollection);

                if (activeOnly)
                {
                    announcements = announcements.Where(a => a.IsActive).ToList();
                }

                return announcements
                    .OrderByDescending(a => a.CreatedDate)
                    .Select(a => MapToDto(a, ""))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all announcements");
                throw;
            }
        }

        public async Task<List<AnnouncementDto>> GetAnnouncementsForUserAsync(string userId, string userRole, List<long>? propertyIds = null)
        {
            try
            {
                var allAnnouncements = await GetAllAnnouncementsAsync(true);
                var now = DateTime.UtcNow;

                var filtered = allAnnouncements.Where(a =>
                {
                    // Check if expired
                    if (a.ExpiryDate.HasValue && a.ExpiryDate.Value < now)
                        return false;

                    // Check target audience
                    bool isTargeted = false;

                    if (userRole == "Tenant" && a.ForAllTenants)
                        isTargeted = true;
                    if (userRole == "Caretaker" && a.ForAllCaretakers)
                        isTargeted = true;
                    if ((userRole == "Admin" || userRole == "PropertyManager") && a.ForAllManagers)
                        isTargeted = true;

                    // Check property targeting
                    if (isTargeted && a.TargetPropertyIds.Any())
                    {
                        if (propertyIds == null || !propertyIds.Any())
                            return false;

                        isTargeted = a.TargetPropertyIds.Intersect(propertyIds).Any();
                    }

                    return isTargeted;
                })
                .OrderByDescending(a => a.Priority)
                .ThenByDescending(a => a.CreatedDate)
                .ToList();

                // Mark as new if user hasn't viewed
                foreach (var announcement in filtered)
                {
                    if (!string.IsNullOrEmpty(announcement.Id))
                    {
                        var doc = await _firestoreService.GetDocumentAsync<AnnouncementDocument>(AnnouncementsCollection, announcement.Id);
                        announcement.IsNew = doc != null && !doc.ViewedBy.Contains(userId);
                    }
                    else
                    {
                        announcement.IsNew = false;
                    }
                }

                return filtered;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting announcements for user {userId}");
                throw;
            }
        }

        public async Task<AnnouncementsSummary> GetAnnouncementsSummaryAsync(string userId, string userRole, List<long>? propertyIds = null)
        {
            try
            {
                var announcements = await GetAnnouncementsForUserAsync(userId, userRole, propertyIds);

                return new AnnouncementsSummary
                {
                    TotalActive = announcements.Count,
                    UnreadCount = announcements.Count(a => a.IsNew),
                    RecentAnnouncements = announcements.Take(5).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting announcements summary for user {userId}");
                throw;
            }
        }

        public async Task MarkAsViewedAsync(string announcementId, string userId)
        {
            try
            {
                var announcement = await _firestoreService.GetDocumentAsync<AnnouncementDocument>(AnnouncementsCollection, announcementId);
                if (announcement != null && !announcement.ViewedBy.Contains(userId))
                {
                    announcement.ViewedBy.Add(userId);
                    await _firestoreService.UpdateDocumentAsync(AnnouncementsCollection, announcementId, new Dictionary<string, object>
                    {
                        { "ViewedBy", announcement.ViewedBy }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking announcement {announcementId} as viewed");
                throw;
            }
        }

        private AnnouncementDto MapToDto(AnnouncementDocument announcement, string currentUserId)
        {
            Enum.TryParse<AnnouncementType>(announcement.Type, out var type);

            return new AnnouncementDto
            {
                Id = announcement.Id ?? string.Empty,
                Title = announcement.Title,
                Content = announcement.Content,
                Type = type,
                Priority = (AnnouncementPriority)announcement.Priority,
                CreatedBy = announcement.CreatedBy,
                CreatedByName = announcement.CreatedByName,
                CreatedDate = announcement.CreatedDate.ToDateTime(),
                ExpiryDate = announcement.ExpiryDate?.ToDateTime(),
                IsActive = announcement.IsActive,
                ForAllTenants = announcement.ForAllTenants,
                ForAllCaretakers = announcement.ForAllCaretakers,
                ForAllManagers = announcement.ForAllManagers,
                TargetPropertyIds = announcement.TargetPropertyIds,
                ImageUrl = announcement.ImageUrl,
                ViewCount = announcement.ViewedBy.Count,
                IsNew = !string.IsNullOrEmpty(currentUserId) && !announcement.ViewedBy.Contains(currentUserId)
            };
        }
    }
}
