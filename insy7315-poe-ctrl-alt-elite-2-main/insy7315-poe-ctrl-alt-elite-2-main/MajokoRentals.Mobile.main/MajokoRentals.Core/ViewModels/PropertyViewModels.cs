
using MajokoRentals.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.ViewModels
{
    public class PropertyListViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public double MonthlyRent { get; set; }
        public int Bedrooms { get; set; }
        public double Bathrooms { get; set; }
        public PropertyStatus Status { get; set; }
        public string? MainImage { get; set; }
        public bool HasActiveLease { get; set; }
    }

    public class PropertyDetailsViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int Bedrooms { get; set; }
        public double Bathrooms { get; set; }
        public double SquareMeters { get; set; }
        public double MonthlyRent { get; set; }
        public double SecurityDeposit { get; set; }
        public PropertyStatus Status { get; set; }
        public List<string> Amenities { get; set; } = new();
        public List<string> Images { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public Lease? ActiveLease { get; set; }
        public List<MaintenanceRequest> RecentMaintenanceRequests { get; set; } = new();
    }

    public class CreatePropertyViewModel
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Province { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string PostalCode { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [Required]
        [Range(1, 10)]
        public int Bedrooms { get; set; }

        [Required]
        [Range(1, 10)]
        public double Bathrooms { get; set; }

        [Required]
        [Range(1, 10000)]
        public double SquareMeters { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public double MonthlyRent { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public double SecurityDeposit { get; set; }

        public PropertyStatus Status { get; set; } = PropertyStatus.Available;

        public List<string> Amenities { get; set; } = new();

    }

    public class EditPropertyViewModel
    {
        public long Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Province { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string PostalCode { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [Required]
        [Range(1, 10)]
        public int Bedrooms { get; set; }

        [Required]
        [Range(1, 10)]
        public double Bathrooms { get; set; }

        [Required]
        [Range(1, 10000)]
        public double SquareMeters { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public double MonthlyRent { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public double SecurityDeposit { get; set; }

        public PropertyStatus Status { get; set; }

        public List<string> Amenities { get; set; } = new();

        public List<string> ExistingImages { get; set; } = new();


        public List<string> ImagesToDelete { get; set; } = new();
    }
}

