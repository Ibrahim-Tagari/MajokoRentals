using MajokoRentals.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.DTOs
{
    // Request DTOs

    public class CreateAnnouncementRequest
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public AnnouncementType Type { get; set; } = AnnouncementType.General;

        public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

        public DateTime? ExpiryDate { get; set; }

        public bool ForAllTenants { get; set; } = true;
        public bool ForAllCaretakers { get; set; } = false;
        public bool ForAllManagers { get; set; } = false;

        public List<long> TargetPropertyIds { get; set; } = new();

        public string? ImageUrl { get; set; }
    }

    /// <summary>
    /// Request to update an announcement
    /// </summary>
    public class UpdateAnnouncementRequest
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public AnnouncementType Type { get; set; } = AnnouncementType.General;

        public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

        public DateTime? ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        public bool ForAllTenants { get; set; } = true;
        public bool ForAllCaretakers { get; set; } = false;
        public bool ForAllManagers { get; set; } = false;

        public List<long> TargetPropertyIds { get; set; } = new();

        public string? ImageUrl { get; set; }
    }

    // Response DTOs

    /// <summary>
    /// Announcement response DTO
    /// </summary>
    public class AnnouncementDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public AnnouncementType Type { get; set; }
        public AnnouncementPriority Priority { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public bool ForAllTenants { get; set; }
        public bool ForAllCaretakers { get; set; }
        public bool ForAllManagers { get; set; }
        public List<long> TargetPropertyIds { get; set; } = new();
        public List<string> TargetPropertyNames { get; set; } = new();
        public string? ImageUrl { get; set; }
        public int ViewCount { get; set; }
        public bool IsNew { get; set; } // Whether current user has viewed it
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow;
    }

    /// <summary>
    /// Summary of active announcements
    /// </summary>
    public class AnnouncementsSummary
    {
        public int TotalActive { get; set; }
        public int UnreadCount { get; set; }
        public List<AnnouncementDto> RecentAnnouncements { get; set; } = new();
    }
}
