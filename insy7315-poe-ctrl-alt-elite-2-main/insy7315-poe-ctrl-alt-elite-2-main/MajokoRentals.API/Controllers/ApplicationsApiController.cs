using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using System.Security.Claims;

namespace MajokoRentals.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ApplicationsController : ControllerBase
    {
        private readonly IFirestoreService _firestore;
        private readonly IUserProfileService _userProfile;
        private readonly ILogger<ApplicationsController> _logger;

        public ApplicationsController(
            IFirestoreService firestore,
            IUserProfileService userProfile,
            ILogger<ApplicationsController> logger)
        {
            _firestore = firestore;
            _userProfile = userProfile;
            _logger = logger;
        }

        /// <summary>
        /// Get all rental applications (filtered by role)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetApplications(
            [FromQuery] ApplicationStatus? status = null,
            [FromQuery] int? propertyId = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var allApplications = await _firestore.GetCollectionAsync<Dictionary<string, object>>("applications");

                // Filter by role
                var filteredApplications = allApplications.Where(a =>
                {
                    // Applicant filtering
                    if (User.IsInRole("Applicant") && a.ContainsKey("applicantId"))
                    {
                        if (a["applicantId"].ToString() != userId) return false;
                    }

                    // Status filter
                    if (status.HasValue && a.ContainsKey("status"))
                    {
                        var appStatus = Enum.Parse<ApplicationStatus>(a["status"].ToString()!);
                        if (appStatus != status.Value) return false;
                    }

                    // Property filter
                    if (propertyId.HasValue && a.ContainsKey("propertyId"))
                    {
                        var propId = Convert.ToInt64(a["propertyId"]);
                        if (propId != propertyId.Value) return false;
                    }

                    return true;
                });

                var applications = filteredApplications
                    .OrderByDescending(a => a.ContainsKey("applicationDate") ? DateTime.Parse(a["applicationDate"].ToString()!) : DateTime.MinValue)
                    .Select(a => new
                    {
                        id = a.ContainsKey("id") ? Convert.ToInt64(a["id"]) : 0,
                        propertyId = a.ContainsKey("propertyId") ? Convert.ToInt64(a["propertyId"]) : 0,
                        propertyName = a.ContainsKey("propertyName") ? a["propertyName"].ToString() : "",
                        propertyAddress = a.ContainsKey("propertyAddress") ? a["propertyAddress"].ToString() : "",
                        applicantId = a.ContainsKey("applicantId") ? a["applicantId"].ToString() : "",
                        applicantName = a.ContainsKey("applicantName") ? a["applicantName"].ToString() : "",
                        applicantEmail = a.ContainsKey("applicantEmail") ? a["applicantEmail"].ToString() : "",
                        applicantPhone = a.ContainsKey("applicantPhone") ? a["applicantPhone"].ToString() : "",
                        monthlyIncome = a.ContainsKey("monthlyIncome") ? Convert.ToDouble(a["monthlyIncome"]) : 0,
                        employmentStatus = a.ContainsKey("employmentStatus") ? a["employmentStatus"].ToString() : "",
                        moveInDate = a.ContainsKey("moveInDate") ? DateTime.Parse(a["moveInDate"].ToString()!) : (DateTime?)null,
                        applicationDate = a.ContainsKey("applicationDate") ? DateTime.Parse(a["applicationDate"].ToString()!) : DateTime.UtcNow,
                        status = a.ContainsKey("status") ? Enum.Parse<ApplicationStatus>(a["status"].ToString()!) : ApplicationStatus.Submitted,
                        reviewedDate = a.ContainsKey("reviewedDate") ? DateTime.Parse(a["reviewedDate"].ToString()!) : (DateTime?)null,
                        reviewNotes = a.ContainsKey("reviewNotes") ? a["reviewNotes"].ToString() : null
                    })
                    .ToList();

                return Ok(applications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching applications from Firestore");
                return StatusCode(500, new { message = "Error fetching applications" });
            }
        }

        /// <summary>
        /// Get a specific rental application by numeric ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetApplication(string id)
        {
            try
            {
                Dictionary<string, object>? application = null;

                // Try to parse as numeric ID first (the actual application ID)
                if (long.TryParse(id, out long numericId))
                {
                    _logger.LogInformation($"Looking up application with numeric ID: {numericId}");

                    // Search for application by numeric ID field
                    var allApplications = await _firestore.GetCollectionAsync<Dictionary<string, object>>("applications");
                    application = allApplications.FirstOrDefault(a =>
                        a.ContainsKey("id") && Convert.ToInt64(a["id"]) == numericId);
                }
                else
                {
                    // Try as Firestore document ID
                    _logger.LogInformation($"Looking up application with document ID: {id}");
                    application = await _firestore.GetDocumentAsync<Dictionary<string, object>>("applications", id);
                }

                if (application == null)
                {
                    _logger.LogWarning($"Application {id} not found");
                    return NotFound();
                }

                // Authorization check
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("Admin") && !User.IsInRole("PropertyManager"))
                {
                    var applicantId = application.ContainsKey("applicantId") ? application["applicantId"].ToString() : "";
                    if (applicantId != userId)
                    {
                        _logger.LogWarning($"User {userId} not authorized to view application {id}");
                        return Forbid();
                    }
                }

                _logger.LogInformation($"Successfully retrieved application {id}");

                // Format the response to match the expected DTO structure
                var response = new
                {
                    Id = application.ContainsKey("id") ? Convert.ToInt64(application["id"]) : 0,
                    PropertyId = application.ContainsKey("propertyId") ? Convert.ToInt64(application["propertyId"]) : 0,
                    PropertyName = application.ContainsKey("propertyName") ? application["propertyName"].ToString() : "",
                    PropertyAddress = application.ContainsKey("propertyAddress") ? application["propertyAddress"].ToString() : "",
                    ApplicantId = application.ContainsKey("applicantId") ? application["applicantId"].ToString() : "",
                    ApplicantName = application.ContainsKey("applicantName") ? application["applicantName"].ToString() : "",
                    ApplicantEmail = application.ContainsKey("applicantEmail") ? application["applicantEmail"].ToString() : "",
                    ApplicantPhone = application.ContainsKey("applicantPhone") ? application["applicantPhone"].ToString() : "",
                    MonthlyIncome = application.ContainsKey("monthlyIncome") ? Convert.ToDouble(application["monthlyIncome"]) : 0,
                    EmploymentStatus = application.ContainsKey("employmentStatus") ? application["employmentStatus"].ToString() : "",
                    Employer = application.ContainsKey("employer") ? application["employer"].ToString() : null,
                    CurrentAddress = application.ContainsKey("currentAddress") ? application["currentAddress"].ToString() : null,
                    MoveInDate = application.ContainsKey("moveInDate") && !string.IsNullOrEmpty(application["moveInDate"].ToString()) ? DateTime.Parse(application["moveInDate"].ToString()!) : (DateTime?)null,
                    NumberOfOccupants = application.ContainsKey("numberOfOccupants") ? Convert.ToInt32(application["numberOfOccupants"]) : (int?)null,
                    HasPets = application.ContainsKey("hasPets") && Convert.ToBoolean(application["hasPets"]),
                    PetDetails = application.ContainsKey("petDetails") ? application["petDetails"].ToString() : null,
                    AdditionalNotes = application.ContainsKey("additionalNotes") ? application["additionalNotes"].ToString() : null,
                    ApplicationDate = application.ContainsKey("applicationDate") ? DateTime.Parse(application["applicationDate"].ToString()!) : DateTime.UtcNow,
                    Status = application.ContainsKey("status") ? Enum.Parse<ApplicationStatus>(application["status"].ToString()!) : ApplicationStatus.Submitted,
                    ReviewedDate = application.ContainsKey("reviewedDate") && !string.IsNullOrEmpty(application["reviewedDate"].ToString()) ? DateTime.Parse(application["reviewedDate"].ToString()!) : (DateTime?)null,
                    ReviewedBy = application.ContainsKey("reviewedBy") ? application["reviewedBy"].ToString() : null,
                    ReviewNotes = application.ContainsKey("reviewNotes") ? application["reviewNotes"].ToString() : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching application {id} from Firestore");
                return StatusCode(500, new { message = "Error fetching application" });
            }
        }

        /// <summary>
        /// Submit a new rental application
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateApplication([FromBody] CreateApplicationDto dto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation($"Creating application for user ID: {userId}");

                var userProfile = await _userProfile.GetUserProfileAsync(userId!);

                if (userProfile == null)
                {
                    _logger.LogWarning($"User profile not found for user ID: {userId}");
                    return BadRequest(new { message = "User profile not found" });
                }

                _logger.LogInformation($"User profile found: {userProfile.FirstName} {userProfile.LastName} ({userProfile.Email})");

                // Get property details
                var allProperties = await _firestore.GetCollectionAsync<Dictionary<string, object>>("properties");
                var property = allProperties.FirstOrDefault(p => p.ContainsKey("id") && Convert.ToInt64(p["id"]) == dto.PropertyId);

                var id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var applicationData = new Dictionary<string, object>
                {
                    { "id", id },
                    { "propertyId", dto.PropertyId },
                    { "propertyName", property != null && property.ContainsKey("name") ? property["name"].ToString()! : "" },
                    { "propertyAddress", property != null && property.ContainsKey("address") ? property["address"].ToString()! : "" },
                    { "applicantId", userId! },
                    { "applicantName", $"{userProfile.FirstName} {userProfile.LastName}" },
                    { "applicantEmail", userProfile.Email },
                    { "applicantPhone", dto.PhoneNumber ?? userProfile.PhoneNumber ?? "" },
                    { "monthlyIncome", dto.MonthlyIncome },
                    { "employmentStatus", dto.EmploymentStatus },
                    { "employer", dto.Employer ?? "" },
                    { "currentAddress", dto.CurrentAddress ?? "" },
                    { "moveInDate", dto.DesiredMoveInDate?.ToString("o") ?? "" },
                    { "numberOfOccupants", dto.NumberOfOccupants ?? 1 },
                    { "hasPets", dto.HasPets },
                    { "petDetails", dto.PetDetails ?? "" },
                    { "emergencyContactName", dto.EmergencyContactName ?? "" },
                    { "emergencyContactPhone", dto.EmergencyContactPhone ?? "" },
                    { "previousLandlordName", dto.PreviousLandlordName ?? "" },
                    { "previousLandlordPhone", dto.PreviousLandlordPhone ?? "" },
                    { "additionalNotes", dto.AdditionalNotes ?? "" },
                    { "applicationDate", DateTime.UtcNow.ToString("o") },
                    { "status", ApplicationStatus.Submitted.ToString() },
                    { "createdAt", DateTime.UtcNow.ToString("o") }
                };

                var docId = await _firestore.AddDocumentAsync("applications", applicationData);

                _logger.LogInformation($"Application created in Firestore with ID: {docId}");

                return CreatedAtAction(nameof(GetApplication), new { id = docId }, new { id = docId, applicationId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating application in Firestore");
                return StatusCode(500, new { message = "Error creating application" });
            }
        }

        /// <summary>
        /// Review a rental application (Admin/PropertyManager only)
        /// </summary>
        [HttpPut("{id}/review")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReviewApplication(string id, [FromBody] ReviewApplicationDto dto)
        {
            try
            {
                Dictionary<string, object>? application = null;
                string? documentId = null;

                // Try to parse as numeric ID first
                if (long.TryParse(id, out long numericId))
                {
                    _logger.LogInformation($"Looking up application with numeric ID: {numericId} for review");

                    // Search for application by numeric ID and get the Firestore document ID
                    documentId = await _firestore.FindDocumentIdByFieldAsync("applications", "id", numericId);
                    if (documentId != null)
                    {
                        application = await _firestore.GetDocumentAsync<Dictionary<string, object>>("applications", documentId);
                    }
                }
                else
                {
                    // Try as Firestore document ID
                    documentId = id;
                    application = await _firestore.GetDocumentAsync<Dictionary<string, object>>("applications", id);
                }

                if (application == null || documentId == null)
                {
                    _logger.LogWarning($"Application {id} not found for review");
                    return NotFound();
                }

                application["status"] = dto.Status.ToString();
                application["reviewedDate"] = DateTime.UtcNow.ToString("o");
                application["reviewedBy"] = User.FindFirstValue(ClaimTypes.NameIdentifier);
                application["reviewNotes"] = dto.Notes ?? "";
                application["updatedAt"] = DateTime.UtcNow.ToString("o");

                await _firestore.UpdateDocumentAsync("applications", documentId, application);

                _logger.LogInformation($"Application {id} (document ID: {documentId}) reviewed with status: {dto.Status}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reviewing application {id} in Firestore");
                return StatusCode(500, new { message = "Error reviewing application" });
            }
        }

        /// <summary>
        /// Get my applications (current user)
        /// </summary>
        [HttpGet("my-applications")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyApplications()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation($"GetMyApplications called for user ID: {userId}");

                var allApplications = await _firestore.GetCollectionAsync<Dictionary<string, object>>("applications");
                _logger.LogInformation($"Retrieved {allApplications.Count} total applications from Firestore");

                // Log all applicant IDs for debugging
                foreach (var app in allApplications)
                {
                    var appId = app.ContainsKey("applicantId") ? app["applicantId"].ToString() : "N/A";
                    var propName = app.ContainsKey("propertyName") ? app["propertyName"].ToString() : "N/A";
                    _logger.LogInformation($"Application for '{propName}' has applicantId: {appId}");
                }

                var myApplications = allApplications
                    .Where(a => a.ContainsKey("applicantId") && a["applicantId"].ToString() == userId)
                    .OrderByDescending(a => a.ContainsKey("applicationDate") ? DateTime.Parse(a["applicationDate"].ToString()!) : DateTime.MinValue)
                    .Select(a => new
                    {
                        Id = a.ContainsKey("id") ? Convert.ToInt64(a["id"]) : 0,
                        PropertyId = a.ContainsKey("propertyId") ? Convert.ToInt64(a["propertyId"]) : 0,
                        PropertyName = a.ContainsKey("propertyName") ? a["propertyName"].ToString() : "",
                        PropertyAddress = a.ContainsKey("propertyAddress") ? a["propertyAddress"].ToString() : "",
                        ApplicantId = a.ContainsKey("applicantId") ? a["applicantId"].ToString() : "",
                        ApplicantName = a.ContainsKey("applicantName") ? a["applicantName"].ToString() : "",
                        ApplicantEmail = a.ContainsKey("applicantEmail") ? a["applicantEmail"].ToString() : "",
                        ApplicantPhone = a.ContainsKey("applicantPhone") ? a["applicantPhone"].ToString() : "",
                        MonthlyIncome = a.ContainsKey("monthlyIncome") ? Convert.ToDouble(a["monthlyIncome"]) : 0,
                        EmploymentStatus = a.ContainsKey("employmentStatus") ? a["employmentStatus"].ToString() : "",
                        Employer = a.ContainsKey("employer") ? a["employer"].ToString() : null,
                        CurrentAddress = a.ContainsKey("currentAddress") ? a["currentAddress"].ToString() : null,
                        MoveInDate = a.ContainsKey("moveInDate") && !string.IsNullOrEmpty(a["moveInDate"].ToString()) ? DateTime.Parse(a["moveInDate"].ToString()!) : (DateTime?)null,
                        NumberOfOccupants = a.ContainsKey("numberOfOccupants") ? Convert.ToInt32(a["numberOfOccupants"]) : (int?)null,
                        HasPets = a.ContainsKey("hasPets") && Convert.ToBoolean(a["hasPets"]),
                        PetDetails = a.ContainsKey("petDetails") ? a["petDetails"].ToString() : null,
                        AdditionalNotes = a.ContainsKey("additionalNotes") ? a["additionalNotes"].ToString() : null,
                        ApplicationDate = a.ContainsKey("applicationDate") ? DateTime.Parse(a["applicationDate"].ToString()!) : DateTime.UtcNow,
                        Status = a.ContainsKey("status") ? Enum.Parse<ApplicationStatus>(a["status"].ToString()!) : ApplicationStatus.Submitted,
                        ReviewedDate = a.ContainsKey("reviewedDate") && !string.IsNullOrEmpty(a["reviewedDate"].ToString()) ? DateTime.Parse(a["reviewedDate"].ToString()!) : (DateTime?)null,
                        ReviewedBy = a.ContainsKey("reviewedBy") ? a["reviewedBy"].ToString() : null,
                        ReviewNotes = a.ContainsKey("reviewNotes") ? a["reviewNotes"].ToString() : null
                    })
                    .ToList();

                _logger.LogInformation($"Returning {myApplications.Count} applications for user {userId}");

                return Ok(myApplications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching my applications from Firestore");
                return StatusCode(500, new { message = "Error fetching applications" });
            }
        }
    }

    // DTOs for Applications
    public class CreateApplicationDto
    {
        public long PropertyId { get; set; }
        public string? PhoneNumber { get; set; }
        public double MonthlyIncome { get; set; }
        public string EmploymentStatus { get; set; } = null!;
        public string? Employer { get; set; }
        public string? CurrentAddress { get; set; }
        public DateTime? DesiredMoveInDate { get; set; }
        public int? NumberOfOccupants { get; set; }
        public bool HasPets { get; set; }
        public string? PetDetails { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? PreviousLandlordName { get; set; }
        public string? PreviousLandlordPhone { get; set; }
        public string? AdditionalNotes { get; set; }
    }

    public class ReviewApplicationDto
    {
        public ApplicationStatus Status { get; set; }
        public string? Notes { get; set; }
    }
}
