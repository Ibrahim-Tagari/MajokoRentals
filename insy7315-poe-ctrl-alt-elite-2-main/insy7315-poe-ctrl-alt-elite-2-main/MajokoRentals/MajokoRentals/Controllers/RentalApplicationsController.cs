using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using MajokoRentals.Core.ViewModels;
using System.Security.Claims;

namespace MajokoRentals.Controllers
{
    [Authorize]
    public class RentalApplicationsController : Controller
    {
        private readonly IApiClientService _apiClient;
        private readonly IFileUploadService _fileUploadService;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly IUserProfileService _userProfile;
        private readonly ILogger<RentalApplicationsController> _logger;

        public RentalApplicationsController(
            IApiClientService apiClient,
            IFileUploadService fileUploadService,
            IEmailService emailService,
            IEmailTemplateService emailTemplateService,
            IUserProfileService userProfile,
            ILogger<RentalApplicationsController> logger)
        {
            _apiClient = apiClient;
            _fileUploadService = fileUploadService;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _userProfile = userProfile;
            _logger = logger;
        }

        private void SetupApiAuthentication()
        {
            var firebaseToken = HttpContext.Session.GetString("FirebaseToken");
            if (!string.IsNullOrEmpty(firebaseToken))
            {
                _apiClient.SetAuthToken(firebaseToken);
            }
        }

        // GET: RentalApplications (Admin/PropertyManager view)
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Index(ApplicationStatus? status = null)
        {
            try
            {
                SetupApiAuthentication();

                // Build query string
                var endpoint = "/api/applications";
                if (status.HasValue)
                {
                    endpoint += $"?status={status.Value}";
                }

                var applicationsData = await _apiClient.GetAsync<List<RentalApplicationDto>>(endpoint);

                if (applicationsData == null)
                {
                    applicationsData = new List<RentalApplicationDto>();
                }

                // Transform to ViewModels
                var applications = applicationsData.Select(r => new RentalApplicationListViewModel
                {
                    Id = r.Id,
                    ApplicantName = r.ApplicantName,
                    ApplicantEmail = r.ApplicantEmail,
                    PropertyName = r.PropertyName,
                    PropertyAddress = r.PropertyAddress,
                    ApplicationDate = r.ApplicationDate,
                    Status = r.Status,
                    ReviewedByName = r.ReviewedBy ?? "",
                    ReviewedDate = r.ReviewedDate
                }).ToList();

                // Pass enum values directly for iteration in the view
                ViewBag.StatusFilter = Enum.GetValues(typeof(ApplicationStatus)).Cast<ApplicationStatus>();
                ViewBag.CurrentStatus = status;

                return View(applications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rental applications");
                TempData["ErrorMessage"] = "Unable to load rental applications. Please try again.";
                return View(new List<RentalApplicationListViewModel>());
            }
        }

        // GET: RentalApplications/MyApplications (Applicant view)
        public async Task<IActionResult> MyApplications()
        {
            try
            {
                SetupApiAuthentication();

                // API automatically filters for current user
                var applicationsData = await _apiClient.GetAsync<List<RentalApplicationDto>>("/api/applications/my-applications");

                if (applicationsData == null)
                {
                    applicationsData = new List<RentalApplicationDto>();
                }

                // Transform to ViewModels
                var applications = applicationsData.Select(r => new RentalApplicationListViewModel
                {
                    Id = r.Id,
                    ApplicantName = r.ApplicantName,
                    ApplicantEmail = r.ApplicantEmail,
                    PropertyName = r.PropertyName,
                    PropertyAddress = r.PropertyAddress,
                    ApplicationDate = r.ApplicationDate,
                    Status = r.Status,
                    ReviewedByName = r.ReviewedBy ?? "",
                    ReviewedDate = r.ReviewedDate
                }).ToList();

                return View(applications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading my applications");
                return View(new List<RentalApplicationListViewModel>());
            }
        }

        // GET: RentalApplications/Details/5
        public async Task<IActionResult> Details(long id)
        {
            try
            {
                SetupApiAuthentication();

                var application = await _apiClient.GetAsync<RentalApplicationDto>($"/api/applications/{id}");

                if (application == null)
                {
                    return NotFound();
                }

                var viewModel = new RentalApplicationDetailsViewModel
                {
                    Id = application.Id,
                    PropertyId = application.PropertyId,
                    PropertyName = application.PropertyName,
                    PropertyAddress = application.PropertyAddress,
                    PropertyRent = 0,
                    ApplicantId = application.ApplicantId,
                    FirstName = application.ApplicantName.Split(' ')[0],
                    LastName = application.ApplicantName.Split(' ').Length > 1 ? application.ApplicantName.Split(' ')[1] : "",
                    Email = application.ApplicantEmail,
                    Phone = application.ApplicantPhone,
                    IdNumber = "",
                    EmploymentStatus = application.EmploymentStatus,
                    EmployerName = application.Employer ?? "",
                    MonthlyIncome = application.MonthlyIncome,
                    CurrentAddress = application.CurrentAddress ?? "",
                    PreferredMoveInDate = application.MoveInDate ?? DateTime.Today,
                    NumberOfOccupants = application.NumberOfOccupants ?? 1,
                    HasPets = application.HasPets,
                    PetDetails = application.PetDetails,
                    AdditionalInformation = application.AdditionalNotes ?? "",
                    IdDocumentUrl = "",
                    ProofOfIncomeUrl = "",
                    BankStatementUrl = "",
                    ReferenceLetterUrl = "",
                    Status = application.Status,
                    ApplicationDate = application.ApplicationDate,
                    ReviewedBy = application.ReviewedBy ?? "",
                    ReviewedByName = application.ReviewedBy ?? "",
                    ReviewedDate = application.ReviewedDate,
                    ReviewNotes = application.ReviewNotes,
                    RejectionReason = application.ReviewNotes
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading application {id}");
                return NotFound();
            }
        }

        // GET: RentalApplications/Create
        public async Task<IActionResult> Create(int? propertyId = null)
        {
            try
            {
                SetupApiAuthentication();

                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties?status=Available");

                if (properties == null || !properties.Any())
                {
                    TempData["ErrorMessage"] = "There are no available properties at the moment.";
                    return RedirectToAction("Index", "Properties");
                }

                ViewBag.Properties = new SelectList(properties, "Id", "Name", propertyId);

                // Get current user from Firebase/Firestore
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = userId != null ? await _userProfile.GetUserProfileAsync(userId) : null;
                var viewModel = new CreateRentalApplicationViewModel
                {
                    PropertyId = propertyId ?? 0,
                    FirstName = user?.FirstName ?? "",
                    LastName = user?.LastName ?? "",
                    Email = user?.Email ?? "",
                    Phone = user?.PhoneNumber ?? ""
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create application page");
                return RedirectToAction("Index", "Properties");
            }
        }

        // POST: RentalApplications/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateRentalApplicationViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                SetupApiAuthentication();
                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties?status=Available");
                ViewBag.Properties = new SelectList(properties, "Id", "Name", viewModel.PropertyId);
                return View(viewModel);
            }

            try
            {
                SetupApiAuthentication();

                // Upload documents
                string? idDocUrl = null, proofOfIncomeUrl = null, bankStatementUrl = null, referenceLetterUrl = null;

                if (viewModel.IdDocument != null)
                {
                    idDocUrl = await _fileUploadService.UploadFileAsync(viewModel.IdDocument, "applications");
                }

                if (viewModel.ProofOfIncome != null)
                {
                    proofOfIncomeUrl = await _fileUploadService.UploadFileAsync(viewModel.ProofOfIncome, "applications");
                }

                if (viewModel.BankStatement != null)
                {
                    bankStatementUrl = await _fileUploadService.UploadFileAsync(viewModel.BankStatement, "applications");
                }

                if (viewModel.ReferenceLetter != null)
                {
                    referenceLetterUrl = await _fileUploadService.UploadFileAsync(viewModel.ReferenceLetter, "applications");
                }

                // Create application via API
                var createDto = new CreateRentalApplicationDto
                {
                    PropertyId = viewModel.PropertyId,
                    PhoneNumber = viewModel.Phone,
                    MonthlyIncome = viewModel.MonthlyIncome ?? 0,
                    EmploymentStatus = viewModel.EmploymentStatus ?? "",
                    Employer = viewModel.EmployerName,
                    CurrentAddress = viewModel.CurrentAddress,
                    DesiredMoveInDate = viewModel.PreferredMoveInDate,
                    NumberOfOccupants = viewModel.NumberOfOccupants,
                    HasPets = viewModel.HasPets,
                    PetDetails = viewModel.PetDetails,
                    EmergencyContactName = "",
                    EmergencyContactPhone = "",
                    PreviousLandlordName = "",
                    PreviousLandlordPhone = "",
                    AdditionalNotes = viewModel.AdditionalInformation
                };

                var response = await _apiClient.PostAsync<CreateRentalApplicationDto, object>("/api/applications", createDto);

                if (response != null)
                {
                    // Try to send email notifications (don't fail if this fails)
                    try
                    {
                        // Get property details
                        var property = await _apiClient.GetAsync<PropertyDto>($"/api/properties/{viewModel.PropertyId}");
                        string propertyName = property?.Name ?? "the property";

                        // Generate application number for display
                        string applicationNumber = $"APP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";

                        // Send confirmation email to applicant using professional template
                        var applicantEmailBody = _emailTemplateService.GetApplicationReceivedEmailTemplate(
                            viewModel.FirstName,
                            propertyName,
                            applicationNumber
                        );

                        await _emailService.SendEmailAsync(
                            viewModel.Email,
                            "Rental Application Received",
                            applicantEmailBody,
                            isHtml: true
                        );

                        // Send notification email to property managers using professional template
                        var propertyManagers = await _userProfile.GetUsersByRoleAsync("PropertyManager");
                        foreach (var manager in propertyManagers)
                        {
                            if (!string.IsNullOrEmpty(manager.Email))
                            {
                                var managerEmailBody = _emailTemplateService.GetNewApplicationNotificationTemplate(
                                    propertyName,
                                    $"{viewModel.FirstName} {viewModel.LastName}",
                                    applicationNumber
                                );

                                await _emailService.SendEmailAsync(
                                    manager.Email,
                                    "New Rental Application Submitted",
                                    managerEmailBody,
                                    isHtml: true
                                );
                            }
                        }
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, "Failed to send email notifications for application");
                        // Continue anyway - application was submitted successfully
                    }

                    TempData["SuccessMessage"] = "Your rental application has been submitted successfully. You will be contacted once your application is reviewed.";
                    return RedirectToAction(nameof(MyApplications));
                }
                else
                {
                    ModelState.AddModelError("", "Failed to submit application. Please try again.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rental application");
                ModelState.AddModelError("", "An error occurred while submitting your application.");
            }

            SetupApiAuthentication();
            var props = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties?status=Available");
            ViewBag.Properties = new SelectList(props, "Id", "Name", viewModel.PropertyId);
            return View(viewModel);
        }

        // GET: RentalApplications/Review/5
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Review(long id)
        {
            try
            {
                SetupApiAuthentication();

                var application = await _apiClient.GetAsync<RentalApplicationDto>($"/api/applications/{id}");

                if (application == null)
                {
                    return NotFound();
                }

                if (application.Status != ApplicationStatus.Submitted)
                {
                    TempData["ErrorMessage"] = "This application has already been reviewed.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var viewModel = new ReviewApplicationViewModel
                {
                    ApplicationId = application.Id,
                    ApplicantName = application.ApplicantName,
                    PropertyName = application.PropertyName
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading application {id} for review");
                return NotFound();
            }
        }

        // POST: RentalApplications/Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Review(ReviewApplicationViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                SetupApiAuthentication();

                var application = await _apiClient.GetAsync<RentalApplicationDto>($"/api/applications/{viewModel.ApplicationId}");

                if (application == null)
                {
                    return NotFound();
                }

                if (application.Status != ApplicationStatus.Submitted)
                {
                    TempData["ErrorMessage"] = "This application has already been reviewed.";
                    return RedirectToAction(nameof(Details), new { id = viewModel.ApplicationId });
                }

                // Review application via API
                var reviewDto = new ReviewApplicationDto
                {
                    Status = viewModel.Status,
                    Notes = viewModel.ReviewNotes
                };

                var response = await _apiClient.PutAsync<ReviewApplicationDto, object>($"/api/applications/{viewModel.ApplicationId}/review", reviewDto);

                if (response != null)
                {
                    // Send email notification to applicant using professional template
                    string emailSubject;
                    string emailBody;
                    string firstName = application.ApplicantName.Split(' ')[0];

                    if (viewModel.Status == ApplicationStatus.Approved)
                    {
                        emailSubject = "Rental Application Approved";
                        emailBody = _emailTemplateService.GetApplicationApprovedEmailTemplate(
                            firstName,
                            application.PropertyName
                        );
                    }
                    else
                    {
                        emailSubject = "Rental Application Update";
                        emailBody = _emailTemplateService.GetApplicationRejectedEmailTemplate(
                            firstName,
                            application.PropertyName,
                            viewModel.RejectionReason
                        );
                    }

                    await _emailService.SendEmailAsync(
                        application.ApplicantEmail,
                        emailSubject,
                        emailBody,
                        isHtml: true
                    );

                    TempData["SuccessMessage"] = $"Application has been {viewModel.Status.ToString().ToLower()} successfully.";
                    return RedirectToAction(nameof(Details), new { id = viewModel.ApplicationId });
                }
                else
                {
                    ModelState.AddModelError("", "Failed to review application. Please try again.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing application");
                ModelState.AddModelError("", "An error occurred while reviewing the application.");
            }

            return View(viewModel);
        }
    }

    // DTOs for API calls
    public class RentalApplicationDto
    {
        public long Id { get; set; }
        public long PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public string ApplicantId { get; set; } = string.Empty;
        public string ApplicantName { get; set; } = string.Empty;
        public string ApplicantEmail { get; set; } = string.Empty;
        public string ApplicantPhone { get; set; } = string.Empty;
        public double MonthlyIncome { get; set; }
        public string EmploymentStatus { get; set; } = string.Empty;
        public string? Employer { get; set; }
        public string? CurrentAddress { get; set; }
        public DateTime? MoveInDate { get; set; }
        public int? NumberOfOccupants { get; set; }
        public bool HasPets { get; set; }
        public string? PetDetails { get; set; }
        public string? AdditionalNotes { get; set; }
        public DateTime ApplicationDate { get; set; }
        public ApplicationStatus Status { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string? ReviewedBy { get; set; }
        public string? ReviewNotes { get; set; }
    }

    public class CreateRentalApplicationDto
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
