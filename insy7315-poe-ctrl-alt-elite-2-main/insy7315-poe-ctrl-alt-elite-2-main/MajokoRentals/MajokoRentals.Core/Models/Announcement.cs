using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace MajokoRentals.Core.Models
{
    public class Announcement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public AnnouncementType Type { get; set; } = AnnouncementType.General;

        [Required]
        public string CreatedBy { get; set; } = string.Empty;

        public string CreatedByName { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Target audience
        public bool ForAllTenants { get; set; } = true;
        public bool ForAllCaretakers { get; set; } = false;
        public bool ForAllManagers { get; set; } = false;

        public List<long> TargetPropertyIds { get; set; } = new(); // empty = all properties

        public string? ImageUrl { get; set; }

        public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

        public List<string> ViewedBy { get; set; } = new(); // User IDs who have viewed the announcement
    }

    // Firestore document model
    [FirestoreData]
    public class AnnouncementDocument
    {
        [FirestoreProperty]
        public string? Id { get; set; }

        [FirestoreProperty]
        public string Title { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Content { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Type { get; set; } = AnnouncementType.General.ToString();

        [FirestoreProperty]
        public string CreatedBy { get; set; } = string.Empty;

        [FirestoreProperty]
        public string CreatedByName { get; set; } = string.Empty;

        [FirestoreProperty]
        public Timestamp CreatedDate { get; set; }

        [FirestoreProperty]
        public Timestamp? ExpiryDate { get; set; }

        [FirestoreProperty]
        public bool IsActive { get; set; } = true;

        [FirestoreProperty]
        public bool ForAllTenants { get; set; } = true;

        [FirestoreProperty]
        public bool ForAllCaretakers { get; set; } = false;

        [FirestoreProperty]
        public bool ForAllManagers { get; set; } = false;

        [FirestoreProperty]
        public List<long> TargetPropertyIds { get; set; } = new();

        [FirestoreProperty]
        public string? ImageUrl { get; set; }

        [FirestoreProperty]
        public int Priority { get; set; } = 1;

        [FirestoreProperty]
        public List<string> ViewedBy { get; set; } = new();
    }
}

