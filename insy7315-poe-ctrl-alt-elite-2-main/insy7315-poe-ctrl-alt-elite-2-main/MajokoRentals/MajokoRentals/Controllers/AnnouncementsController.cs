 using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using System.Security.Claims;

namespace MajokoRentals.Controllers
{
    [Authorize]
    public class AnnouncementsController : Controller
    {
        private readonly IAnnouncementService _announcementService;
        private readonly IApiClientService _apiClient;
        private readonly ILogger<AnnouncementsController> _logger;

        public AnnouncementsController(
            IAnnouncementService announcementService,
            IApiClientService apiClient,
            ILogger<AnnouncementsController> logger)
        {
            _announcementService = announcementService;
            _apiClient = apiClient;
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

        // GET: Announcements
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var announcements = await _announcementService.GetAllAnnouncementsAsync(activeOnly: false);
                return View(announcements);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading announcements");
                TempData["ErrorMessage"] = "Unable to load announcements. Please try again.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        // GET: Announcements/MyAnnouncements - For all users to see their targeted announcements
        public async Task<IActionResult> MyAnnouncements()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
                {
                    return RedirectToAction("Login", "Account");
                }

                // Get user's property IDs if tenant
                List<long>? propertyIds = null;
                if (userRole == "Tenant")
                {
                    SetupApiAuthentication();
                    var leases = await _apiClient.GetAsync<List<LeaseDto>>("/api/leases?status=Active");
                    if (leases != null && leases.Any())
                    {
                        propertyIds = leases.Select(l => l.PropertyId).Distinct().ToList();
                    }
                }

                var announcements = await _announcementService.GetAnnouncementsForUserAsync(userId, userRole, propertyIds);

                return View(announcements);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user announcements");
                TempData["ErrorMessage"] = "Unable to load announcements. Please try again.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        // GET: Announcements/Create
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Create()
        {
            try
            {
                SetupApiAuthentication();

                // Get properties for targeting
                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");
                ViewBag.Properties = properties ?? new List<PropertyListDto>();

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create announcement form");
                TempData["ErrorMessage"] = "Unable to load the form. Please try again.";
                return RedirectToAction("Index");
            }
        }

        // POST: Announcements/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Create(CreateAnnouncementRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    SetupApiAuthentication();
                    var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");
                    ViewBag.Properties = properties ?? new List<PropertyListDto>();
                    return View(model);
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var announcement = await _announcementService.CreateAnnouncementAsync(userId, model);

                TempData["SuccessMessage"] = "Announcement created successfully!";
                return RedirectToAction(nameof(Details), new { id = announcement.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating announcement");
                ModelState.AddModelError("", "Unable to create announcement. Please try again.");

                SetupApiAuthentication();
                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");
                ViewBag.Properties = properties ?? new List<PropertyListDto>();
                return View(model);
            }
        }

        // GET: Announcements/Details/5
        public async Task<IActionResult> Details(string id)
        {
            try
            {
                var announcement = await _announcementService.GetAnnouncementByIdAsync(id);
                if (announcement == null)
                {
                    TempData["ErrorMessage"] = "Announcement not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Mark as viewed
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    await _announcementService.MarkAsViewedAsync(id, userId);
                }

                return View(announcement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading announcement {id}");
                TempData["ErrorMessage"] = "Unable to load announcement. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Announcements/Edit/5
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Edit(string id)
        {
            try
            {
                var announcement = await _announcementService.GetAnnouncementByIdAsync(id);
                if (announcement == null)
                {
                    TempData["ErrorMessage"] = "Announcement not found.";
                    return RedirectToAction(nameof(Index));
                }

                SetupApiAuthentication();
                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");
                ViewBag.Properties = properties ?? new List<PropertyListDto>();

                var model = new UpdateAnnouncementRequest
                {
                    Title = announcement.Title,
                    Content = announcement.Content,
                    Type = announcement.Type,
                    Priority = announcement.Priority,
                    ExpiryDate = announcement.ExpiryDate,
                    IsActive = announcement.IsActive,
                    ForAllTenants = announcement.ForAllTenants,
                    ForAllCaretakers = announcement.ForAllCaretakers,
                    ForAllManagers = announcement.ForAllManagers,
                    TargetPropertyIds = announcement.TargetPropertyIds,
                    ImageUrl = announcement.ImageUrl
                };

                ViewBag.AnnouncementId = id;
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading announcement {id} for edit");
                TempData["ErrorMessage"] = "Unable to load announcement. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Announcements/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Edit(string id, UpdateAnnouncementRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    SetupApiAuthentication();
                    var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");
                    ViewBag.Properties = properties ?? new List<PropertyListDto>();
                    ViewBag.AnnouncementId = id;
                    return View(model);
                }

                await _announcementService.UpdateAnnouncementAsync(id, model);

                TempData["SuccessMessage"] = "Announcement updated successfully!";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating announcement {id}");
                ModelState.AddModelError("", "Unable to update announcement. Please try again.");

                SetupApiAuthentication();
                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");
                ViewBag.Properties = properties ?? new List<PropertyListDto>();
                ViewBag.AnnouncementId = id;
                return View(model);
            }
        }

        // POST: Announcements/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _announcementService.DeleteAnnouncementAsync(id);
                TempData["SuccessMessage"] = "Announcement deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting announcement {id}");
                TempData["ErrorMessage"] = "Unable to delete announcement. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Helper class for API
        public class PropertyListDto
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
        }
    }
}
