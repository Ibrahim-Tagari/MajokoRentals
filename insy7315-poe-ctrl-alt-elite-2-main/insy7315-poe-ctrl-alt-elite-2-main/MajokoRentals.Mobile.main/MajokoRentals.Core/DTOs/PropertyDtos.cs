using MajokoRentals.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace MajokoRentals.Core.DTOs
{
    public class PropertyDto
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
        public double MonthlyRent { get; set; }
        public double DepositAmount { get; set; }
        public int Bedrooms { get; set; }
        public double Bathrooms { get; set; }
        public int? ParkingSpaces { get; set; }
        public double? SquareMeters { get; set; }
        public PropertyStatus Status { get; set; }
        public bool IsFurnished { get; set; }
        public bool PetsAllowed { get; set; }
        public List<string> Amenities { get; set; } = new();
        public List<string> Images { get; set; } = new();
        public string? VideoUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreatePropertyDto
    {
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

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double MonthlyRent { get; set; }

        [Range(0, double.MaxValue)]
        public double? DepositAmount { get; set; }

        [Required]
        [Range(1, 100)]
        public int Bedrooms { get; set; }

        [Required]
        [Range(1, 100)]
        public double Bathrooms { get; set; }

        public int? ParkingSpaces { get; set; }
        public double? SquareMeters { get; set; }
        public PropertyStatus Status { get; set; } = PropertyStatus.Available;
        public bool IsFurnished { get; set; }
        public bool PetsAllowed { get; set; }
        public List<string>? Amenities { get; set; }
        public List<string>? Images { get; set; }
        public string? VideoUrl { get; set; }
    }

    public class UpdatePropertyDto : CreatePropertyDto
    {
        [Required]
        public long Id { get; set; }
    }

    public class PropertyListDto
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
    }
}

