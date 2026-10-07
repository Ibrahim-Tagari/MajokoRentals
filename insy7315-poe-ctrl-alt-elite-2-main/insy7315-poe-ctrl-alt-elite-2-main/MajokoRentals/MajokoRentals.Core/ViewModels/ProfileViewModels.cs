using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.ViewModels
{
    public class ProfileViewModel
    {
        public string Uid { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Profile Image")]
        public string? ProfileImageUrl { get; set; }

        [Display(Name = "Upload New Profile Image")]
        public IFormFile? NewProfileImage { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
