using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.DTOs
{
    public class RentalApplicationDto
    {
        public string Id { get; set; } = string.Empty;
        public string PropertyId { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string ApplicantId { get; set; } = string.Empty;
        public string ApplicantName { get; set; } = string.Empty;
        public string ApplicantEmail { get; set; } = string.Empty;
        public string ApplicantPhone { get; set; } = string.Empty;
        public ApplicationStatus Status { get; set; }
        public DateTime ApplicationDate { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string? ReviewedBy { get; set; }
        public string? ReviewNotes { get; set; }
    }

    public class CreateRentalApplicationDto
    {
        public string PropertyId { get; set; } = string.Empty;
        public string ApplicantId { get; set; } = string.Empty;
    }

    public class ReviewApplicationDto
    {
        public ApplicationStatus Status { get; set; }
        public string? ReviewNotes { get; set; }
    }
}
