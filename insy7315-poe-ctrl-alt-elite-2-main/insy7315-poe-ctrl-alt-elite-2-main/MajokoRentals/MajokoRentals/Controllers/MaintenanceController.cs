using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using MajokoRentals.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace MajokoRentals.Controllers
{
    [Authorize]
    public class MaintenanceController : Controller
    {
        private readonly IApiClientService _apiClient;
        private readonly IFileUploadService _fileUploadService;
        private readonly IEmailService _emailService;
        private readonly ILogger<MaintenanceController> _logger;
        private readonly IMessagingService _messagingService;
        private readonly IAnnouncementService _announcementService;

        public MaintenanceController(
            IApiClientService apiClient,
            IFileUploadService fileUploadService,
            IEmailService emailService,
            ILogger<MaintenanceController> logger,
            IMessagingService messagingService,
            IAnnouncementService announcementService)
        {
            _apiClient = apiClient;
            _fileUploadService = fileUploadService;
            _emailService = emailService;
            _logger = logger;
            _messagingService = messagingService;
            _announcementService = announcementService;
        }

        private void SetupApiAuthentication()
        {
            var firebaseToken = HttpContext.Session.GetString("FirebaseToken");
            if (!string.IsNullOrEmpty(firebaseToken))
            {
                _apiClient.SetAuthToken(firebaseToken);
            }
        }

        // GET: Maintenance
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        public async Task<IActionResult> Index(MaintenanceStatus? status, MaintenancePriority? priority, int? propertyId)
        {
            try
            {
                SetupApiAuthentication();

                // Build query string
                var queryParams = new List<string>();
                if (status.HasValue) queryParams.Add($"status={status.Value}");
                if (priority.HasValue) queryParams.Add($"priority={priority.Value}");

                var endpoint = "/api/maintenance";
                if (queryParams.Any())
                {
                    endpoint += "?" + string.Join("&", queryParams);
                }

                var requests = await _apiClient.GetAsync<List<MaintenanceRequestDto>>(endpoint);

                if (requests == null)
                {
                    requests = new List<MaintenanceRequestDto>();
                }

                // Transform to ViewModels
                var viewModels = requests.Select(m => new MaintenanceRequestListViewModel
                {
                    Id = m.Id,
                    Title = m.Title,
                    PropertyName = m.PropertyName,
                    PropertyAddress = "",  // Not in DTO
                    ReportedByName = m.ReportedByName,
                    Category = m.Category,
                    Priority = m.Priority,
                    Status = m.Status,
                    ReportedDate = m.ReportedDate,
                    AssignedToName = m.AssignedToName,
                    DaysOpen = (DateTime.UtcNow - m.ReportedDate).Days
                }).ToList();

                ViewBag.Status = status;
                ViewBag.Priority = priority;
                ViewBag.PropertyId = propertyId;

                // Get properties for filter - call API
                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");
                ViewBag.Properties = properties?.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                }).ToList() ?? new List<SelectListItem>();

                // Get pending chat requests for caretakers
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                if ((userRole == "Caretaker" || userRole == "Admin" || userRole == "PropertyManager") && !string.IsNullOrEmpty(userId))
                {
                    var pendingChatsSummary = await _messagingService.GetPendingSessionsAsync(userId);
                    ViewBag.PendingChatRequests = pendingChatsSummary.TotalPending;

                    // Get announcements for caretakers
                    var announcements = await _announcementService.GetAnnouncementsForUserAsync(userId, userRole, null);
                    ViewBag.RecentAnnouncements = announcements.Take(5).ToList();
                }
                else
                {
                    ViewBag.PendingChatRequests = 0;
                    ViewBag.RecentAnnouncements = new List<AnnouncementDto>();
                }

                return View(viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading maintenance requests");
                TempData["ErrorMessage"] = "Unable to load maintenance requests. Please try again.";
                return View(new List<MaintenanceRequestListViewModel>());
            }
        }

        // GET: Maintenance/MyRequests - For tenants
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> MyRequests()
        {
            try
            {
                SetupApiAuthentication();

                var requests = await _apiClient.GetAsync<List<MaintenanceRequestDto>>("/api/maintenance");

                if (requests == null)
                {
                    requests = new List<MaintenanceRequestDto>();
                }

                var viewModels = requests.Select(m => new MaintenanceRequestListViewModel
                {
                    Id = m.Id,
                    Title = m.Title,
                    PropertyName = m.PropertyName,
                    PropertyAddress = "",
                    ReportedByName = m.ReportedByName,
                    Category = m.Category,
                    Priority = m.Priority,
                    Status = m.Status,
                    ReportedDate = m.ReportedDate,
                    AssignedToName = m.AssignedToName,
                    DaysOpen = (DateTime.UtcNow - m.ReportedDate).Days
                }).ToList();

                return View("Index", viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading my maintenance requests");
                return View("Index", new List<MaintenanceRequestListViewModel>());
            }
        }

        // GET: Maintenance/Details/5
        public async Task<IActionResult> Details(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var request = await _apiClient.GetAsync<MaintenanceRequestDto>($"/api/maintenance/{id}");

                if (request == null)
                {
                    return NotFound();
                }

                var viewModel = new MaintenanceRequestDetailsViewModel
                {
                    Id = request.Id,
                    PropertyId = request.PropertyId,
                    PropertyName = request.PropertyName,
                    PropertyAddress = "",
                    ReportedById = "",
                    ReportedByName = request.ReportedByName,
                    ReportedByEmail = "",
                    AssignedToId = null,
                    AssignedToName = request.AssignedToName,
                    Title = request.Title,
                    Description = request.Description,
                    Category = request.Category,
                    Priority = request.Priority,
                    Status = request.Status,
                    ImageUrls = request.ImageUrls,
                    CompletionImageUrls = request.CompletionImageUrls,
                    Location = request.Location,
                    ReportedDate = request.ReportedDate,
                    AssignedDate = request.AssignedDate,
                    StartedDate = request.StartedDate,
                    CompletedDate = request.CompletedDate,
                    EstimatedCost = request.EstimatedCost,
                    ActualCost = request.ActualCost,
                    ResolutionNotes = request.ResolutionNotes,
                    Comments = "",
                    DaysOpen = (DateTime.UtcNow - request.ReportedDate).Days
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading maintenance request {id}");
                return NotFound();
            }
        }

        // GET: Maintenance/Create
        [Authorize(Roles = "Admin,PropertyManager,Tenant")]
        public async Task<IActionResult> Create()
        {
            SetupApiAuthentication();
            await PopulatePropertiesDropdown();
            return View(new CreateMaintenanceRequestViewModel());
        }

        // POST: Maintenance/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager,Tenant")]
        public async Task<IActionResult> Create(CreateMaintenanceRequestViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    SetupApiAuthentication();

                    // Upload images
                    var imageUrls = new List<string>();
                    if (viewModel.Images != null && viewModel.Images.Any())
                    {
                        foreach (var file in viewModel.Images)
                        {
                            var imageUrl = await _fileUploadService.UploadFileAsync(file, "maintenance");
                            imageUrls.Add(imageUrl);
                        }
                    }

                    var createDto = new CreateMaintenanceRequestDto
                    {
                        PropertyId = viewModel.PropertyId,
                        Title = viewModel.Title,
                        Description = viewModel.Description,
                        Category = viewModel.Category,
                        Priority = viewModel.Priority,
                        Location = viewModel.Location,
                        Comments = ""
                    };

                    var response = await _apiClient.PostAsync<CreateMaintenanceRequestDto, object>("/api/maintenance", createDto);

                    if (response != null)
                    {
                        TempData["SuccessMessage"] = "Maintenance request submitted successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to create maintenance request. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating maintenance request");
                    ModelState.AddModelError("", "An error occurred while creating the request.");
                }
            }

            await PopulatePropertiesDropdown(viewModel.PropertyId);
            return View(viewModel);
        }

        // GET: Maintenance/Assign/5
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Assign(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var request = await _apiClient.GetAsync<MaintenanceRequestDto>($"/api/maintenance/{id}");

                if (request == null)
                {
                    return NotFound();
                }

                var viewModel = new AssignMaintenanceRequestViewModel
                {
                    RequestId = request.Id,
                    Title = request.Title,
                    PropertyName = request.PropertyName
                };

                // Get caretakers for dropdown - ensure auth is set up again
                SetupApiAuthentication();
                await PopulateCaretakersDropdown();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading maintenance request {id} for assignment");
                return NotFound();
            }
        }

        // POST: Maintenance/Assign/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Assign(string id, AssignMaintenanceRequestViewModel viewModel)
        {
            if (id != viewModel.RequestId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    SetupApiAuthentication();

                    var assignDto = new AssignMaintenanceDto
                    {
                        AssignedToId = viewModel.AssignedToId,
                        EstimatedCost = viewModel.EstimatedCost,
                        Notes = viewModel.Notes
                    };

                    await _apiClient.PutAsync<AssignMaintenanceDto, object>($"/api/maintenance/{id}/assign", assignDto);

                    TempData["SuccessMessage"] = "Maintenance request assigned successfully!";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error assigning maintenance request");
                    ModelState.AddModelError("", "An error occurred while assigning the request.");
                }
            }

            // Re-populate caretakers dropdown on validation error
            SetupApiAuthentication();
            await PopulateCaretakersDropdown(viewModel.AssignedToId);
            return View(viewModel);
        }

        // POST: Maintenance/UpdateStatus/5
        [HttpPost]
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        public async Task<IActionResult> UpdateStatus(string id, MaintenanceStatus status)
        {
            try
            {
                SetupApiAuthentication();

                var updateDto = new UpdateMaintenanceStatusDto
                {
                    RequestId = id,
                    Status = status
                };

                var response = await _apiClient.PutAsync<UpdateMaintenanceStatusDto, object>($"/api/maintenance/{id}/status", updateDto);

                TempData["SuccessMessage"] = "Status updated successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status");
                TempData["ErrorMessage"] = "An error occurred while updating the status.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Maintenance/Complete/5
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        public async Task<IActionResult> Complete(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var request = await _apiClient.GetAsync<MaintenanceRequestDto>($"/api/maintenance/{id}");

                if (request == null)
                {
                    return NotFound();
                }

                var viewModel = new CompleteMaintenanceRequestViewModel
                {
                    RequestId = request.Id,
                    Title = request.Title
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading maintenance request {id} for completion");
                return NotFound();
            }
        }

        // POST: Maintenance/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        public async Task<IActionResult> Complete(string id, CompleteMaintenanceRequestViewModel viewModel)
        {
            if (id != viewModel.RequestId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    SetupApiAuthentication();

                    // Upload completion images if provided
                    if (viewModel.CompletionImages != null && viewModel.CompletionImages.Any())
                    {
                        using (var formData = new MultipartFormDataContent())
                        {
                            foreach (var file in viewModel.CompletionImages)
                            {
                                var streamContent = new StreamContent(file.OpenReadStream());
                                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                                formData.Add(streamContent, "images", file.FileName);
                            }

                            var uploadResponse = await _apiClient.PostMultipartAsync($"/api/maintenance/{id}/completion-images", formData);

                            if (!uploadResponse)
                            {
                                ModelState.AddModelError("", "Failed to upload images. Please try again.");
                                return View(viewModel);
                            }
                        }
                    }

                    // Update status to Resolved with resolution notes
                    var updateDto = new UpdateMaintenanceStatusDto
                    {
                        RequestId = id,
                        Status = MaintenanceStatus.Resolved,
                        Notes = viewModel.ResolutionNotes,
                        ActualCost = viewModel.ActualCost
                    };

                    await _apiClient.PutAsync<UpdateMaintenanceStatusDto, object>($"/api/maintenance/{id}/status", updateDto);

                    TempData["SuccessMessage"] = "Maintenance request completed successfully!";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error completing maintenance request");
                    ModelState.AddModelError("", "An error occurred while completing the request.");
                }
            }

            return View(viewModel);
        }

        private async Task PopulatePropertiesDropdown(long? selectedId = null)
        {
            try
            {
                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");

                ViewBag.Properties = properties?.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Name} - {p.Address}",
                    Selected = p.Id == selectedId
                }).ToList() ?? new List<SelectListItem>();
            }
            catch
            {
                ViewBag.Properties = new List<SelectListItem>();
            }
        }

        private async Task PopulateCaretakersDropdown(string? selectedId = null)
        {
            try
            {
                var users = await _apiClient.GetAsync<List<UserDto>>("/api/users/caretakers");

                ViewBag.Caretakers = users?.Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FirstName} {u.LastName}",
                    Selected = u.Id == selectedId
                }).ToList() ?? new List<SelectListItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading caretakers");
                ViewBag.Caretakers = new List<SelectListItem>();
            }
        }
    }

    // DTO for assigning maintenance request
    public class AssignMaintenanceDto
    {
        public string AssignedToId { get; set; } = string.Empty;
        public double? EstimatedCost { get; set; }
        public string? Notes { get; set; }
    }
}
