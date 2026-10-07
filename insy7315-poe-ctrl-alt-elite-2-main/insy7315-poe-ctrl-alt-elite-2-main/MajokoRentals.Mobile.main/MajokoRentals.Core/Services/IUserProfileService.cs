using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.Services
{
    public interface IUserProfileService
    {
        Task<UserProfile?> GetUserProfileAsync(string uid);
        Task<UserProfile?> GetUserProfileByEmailAsync(string email);
        Task CreateUserProfileAsync(string uid, UserProfile profile);
        Task UpdateUserProfileAsync(string uid, UserProfile profile);
        Task DeleteUserProfileAsync(string uid);
        Task<List<UserProfile>> GetUsersByRoleAsync(string role);
        Task<bool> UserHasRoleAsync(string uid, string role);
    }

    public class UserProfile
    {
        public string Uid { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = "Applicant";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? ProfileImageUrl { get; set; }
        public Language PreferredLanguage { get; set; } = Language.English;
        public Theme Theme { get; set; } = Theme.Light;
    }
}
