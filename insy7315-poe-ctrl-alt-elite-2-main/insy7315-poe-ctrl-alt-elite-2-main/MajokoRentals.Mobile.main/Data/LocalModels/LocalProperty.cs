using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Mobile.Data.LocalModels
{
    /// <summary>
    /// Cached property data for offline use
    /// </summary>
    public class LocalProperty
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        public string? City { get; set; }
        public string? Province { get; set; }
        public string? PostalCode { get; set; }

        public string? PropertyType { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }

        public double? RentalPrice { get; set; }

        // Caching metadata
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
