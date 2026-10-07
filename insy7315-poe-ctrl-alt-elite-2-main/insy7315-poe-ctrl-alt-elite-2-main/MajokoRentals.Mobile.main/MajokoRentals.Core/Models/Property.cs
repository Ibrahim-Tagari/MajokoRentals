using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MajokoRentals.Core.Models
{
    public class Property
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        public string City { get; set; } = "Johannesburg";

        [StringLength(100)]
        public string Province { get; set; } = "Gauteng";

        [StringLength(10)]
        public string PostalCode { get; set; } = string.Empty;

        // GPS Coordinates for Google Maps
        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [Required]
        public double MonthlyRent { get; set; }

        public double? DepositAmount { get; set; }

        public int Bedrooms { get; set; }

        public int Bathrooms { get; set; }

        public int? ParkingSpaces { get; set; }

        public double? SquareMeters { get; set; }

        public PropertyStatus Status { get; set; } = PropertyStatus.Available;

        public bool IsFurnished { get; set; }

        public bool PetsAllowed { get; set; }

        // Amenities (stored as JSON or separate table)
        public string? Amenities { get; set; } // JSON: ["WiFi", "Security", "Pool", etc.]

        // Images
        public string? ImageUrls { get; set; } // JSON array of image URLs

        public string? VideoUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsArchived { get; set; } = false;

        // Navigation properties
        public virtual ICollection<Lease> Leases { get; set; } = new List<Lease>();
        public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
        public virtual ICollection<RentalApplication> RentalApplications { get; set; } = new List<RentalApplication>();
    }
}

