using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        public string? IdNumber { get; set; }

        public string? ProfilePictureUrl { get; set; }

        public Language PreferredLanguage { get; set; } = Language.English;

        public Theme PreferredTheme { get; set; } = Theme.Light;

        public bool EmailNotificationsEnabled { get; set; } = true;

        public bool PushNotificationsEnabled { get; set; } = true;

        public bool SmsNotificationsEnabled { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Lease> Leases { get; set; } = new List<Lease>();
        public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
        public virtual ICollection<MaintenanceRequest> AssignedMaintenanceTasks { get; set; } = new List<MaintenanceRequest>();
        public virtual ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public virtual ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
        public virtual ICollection<RentalApplication> RentalApplications { get; set; } = new List<RentalApplication>();
        public virtual ICollection<ChatConversation> ChatConversations { get; set; } = new List<ChatConversation>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        public string FullName => $"{FirstName} {LastName}";
    }
}

