using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using MajokoRentals.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajokoRentals.Controllers
{
    [Authorize]
    public class PropertiesController : Controller
    {
        private readonly IApiClientService _apiClient;
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<PropertiesController> _logger;

        public PropertiesController(
            IApiClientService apiClient,
            IFileUploadService fileUploadService,
            ILogger<PropertiesController> logger)
        {
            _apiClient = apiClient;
            _fileUploadService = fileUploadService;
            _logger = logger;
        }

        private void SetupApiAuthentication()
        {
            // Get Firebase token from session (stored during login)
            var firebaseToken = HttpContext.Session.GetString("FirebaseToken");
            if (!string.IsNullOrEmpty(firebaseToken))
            {
                _apiClient.SetAuthToken(firebaseToken);
            }
        }

        // GET: Properties
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? search, PropertyStatus? status)
        {
            try
            {
                // Setup API authentication if user is logged in
                if (User.Identity?.IsAuthenticated == true)
                {
                    SetupApiAuthentication();
                }

                // Call API to get properties
                var endpoint = "/api/properties";
                if (status.HasValue)
                {
                    endpoint += $"?status={status.Value}";
                }

                var apiProperties = await _apiClient.GetAsync<List<PropertyListDto>>(endpoint);

                if (apiProperties == null)
                {
                    _logger.LogWarning("Failed to fetch properties from API");
                    return View(new List<PropertyListViewModel>());
                }

                // Client-side search filter (since API doesn't support search)
                var filteredProperties = apiProperties;
                if (!string.IsNullOrWhiteSpace(search))
                {
                    filteredProperties = apiProperties
                        .Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                   p.Address.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                   p.City.Contains(search, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Transform to ViewModels
                var properties = filteredProperties.Select(p => new PropertyListViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Address = p.Address,
                    City = p.City,
                    MonthlyRent = p.MonthlyRent,
                    Bedrooms = p.Bedrooms,
                    Bathrooms = p.Bathrooms,
                    Status = p.Status,
                    MainImage = p.MainImage,
                    HasActiveLease = p.Status == PropertyStatus.Occupied
                }).ToList();

                ViewBag.Search = search;
                ViewBag.Status = status;

                return View(properties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading properties");
                TempData["ErrorMessage"] = "Unable to load properties. Please try again.";
                return View(new List<PropertyListViewModel>());
            }
        }

        // GET: Properties/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                // Call API to get property details
                // Note: We need to find the Firestore document ID first
                // For now, we'll search through all properties
                var allProperties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties");
                var propertyListItem = allProperties?.FirstOrDefault(p => p.Id == id.Value);

                if (propertyListItem == null)
                {
                    return NotFound();
                }

                // Get full property details (we don't have the document ID yet, so this is a workaround)
                // In production, you'd store the document ID in your views/URLs
                var propertyDto = await _apiClient.GetAsync<PropertyDto>($"/api/properties/{id}");

                if (propertyDto == null)
                {
                    return NotFound();
                }

                var viewModel = new PropertyDetailsViewModel
                {
                    Id = propertyDto.Id,
                    Name = propertyDto.Name,
                    Description = propertyDto.Description,
                    Address = propertyDto.Address,
                    City = propertyDto.City,
                    Province = propertyDto.Province ?? "",
                    PostalCode = propertyDto.PostalCode ?? "",
                    Latitude = propertyDto.Latitude,  // Keep null if not set
                    Longitude = propertyDto.Longitude,  // Keep null if not set
                    Bedrooms = propertyDto.Bedrooms,
                    Bathrooms = propertyDto.Bathrooms,
                    SquareMeters = propertyDto.SquareMeters ?? 0,
                    MonthlyRent = propertyDto.MonthlyRent,
                    SecurityDeposit = propertyDto.DepositAmount,
                    Status = propertyDto.Status,
                    Amenities = propertyDto.Amenities ?? new List<string>(),
                    Images = propertyDto.Images ?? new List<string>(),
                    CreatedAt = propertyDto.CreatedAt,
                    ActiveLease = null, // TODO: Get lease info from API
                    RecentMaintenanceRequests = new List<MaintenanceRequest>() // TODO: Get maintenance from API
                };

                _logger.LogInformation($"Property Details - Lat: {viewModel.Latitude}, Lng: {viewModel.Longitude}, HasLat: {viewModel.Latitude.HasValue}, HasLng: {viewModel.Longitude.HasValue}, Images Count: {viewModel.Images.Count}");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading property {id}");
                return NotFound();
            }
        }

        // GET: Properties/Create
        [Authorize(Roles = "Admin,PropertyManager")]
        public IActionResult Create()
        {
            return View(new CreatePropertyViewModel());
        }

        // POST: Properties/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Create(CreatePropertyViewModel viewModel)
        {
            _logger.LogInformation("=== CREATE PROPERTY POST ACTION CALLED ===");

            // Remove Latitude/Longitude validation errors (culture issue with decimal parsing)
            ModelState.Remove("Latitude");
            ModelState.Remove("Longitude");

            // Manually parse coordinates from form if they exist
            if (Request.Form.ContainsKey("Latitude") && double.TryParse(Request.Form["Latitude"],
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat))
            {
                viewModel.Latitude = lat;
                _logger.LogInformation("Manually parsed Latitude: {Latitude}", lat);
            }

            if (Request.Form.ContainsKey("Longitude") && double.TryParse(Request.Form["Longitude"],
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
            {
                viewModel.Longitude = lng;
                _logger.LogInformation("Manually parsed Longitude: {Longitude}", lng);
            }

            _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("=== MODELSTATE VALIDATION ERRORS ===");
                foreach (var error in ModelState)
                {
                    if (error.Value.Errors.Any())
                    {
                        foreach (var err in error.Value.Errors)
                        {
                            _logger.LogWarning("Field: {Field}, Error: {Error}", error.Key, err.ErrorMessage);
                        }
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var firebaseToken = HttpContext.Session.GetString("FirebaseToken");
                    _logger.LogInformation("Firebase Token in session: {HasToken}", !string.IsNullOrEmpty(firebaseToken));
                    if (!string.IsNullOrEmpty(firebaseToken))
                    {
                        _logger.LogInformation("Firebase Token preview: {Token}", firebaseToken.Substring(0, Math.Min(20, firebaseToken.Length)) + "...");
                    }
                    else
                    {
                        _logger.LogWarning("Firebase Token is NULL or EMPTY - API calls will fail!");
                    }

                    SetupApiAuthentication();

                    // Upload images first
                    var imageUrls = new List<string>();
                    if (viewModel.ImageFiles != null && viewModel.ImageFiles.Any())
                    {
                        foreach (var file in viewModel.ImageFiles)
                        {
                            var imageUrl = await _fileUploadService.UploadFileAsync(file, "properties");
                            imageUrls.Add(imageUrl);
                        }
                    }

                    // Create DTO for API
                    var createDto = new CreatePropertyDto
                    {
                        Name = viewModel.Name,
                        Description = viewModel.Description,
                        Address = viewModel.Address,
                        City = viewModel.City,
                        Province = viewModel.Province,
                        PostalCode = viewModel.PostalCode,
                        Latitude = viewModel.Latitude,
                        Longitude = viewModel.Longitude,
                        Bedrooms = viewModel.Bedrooms,
                        Bathrooms = viewModel.Bathrooms,
                        SquareMeters = viewModel.SquareMeters,
                        MonthlyRent = viewModel.MonthlyRent,
                        DepositAmount = viewModel.SecurityDeposit,
                        Status = viewModel.Status,
                        Amenities = viewModel.Amenities,
                        Images = imageUrls,
                        IsFurnished = false,
                        PetsAllowed = false
                    };

                    _logger.LogInformation("Sending DTO to API - Lat: {Lat}, Lng: {Lng}, Name: {Name}",
                        createDto.Latitude, createDto.Longitude, createDto.Name);

                    // Call API to create property
                    var response = await _apiClient.PostAsync<CreatePropertyDto, object>("/api/properties", createDto);

                    if (response != null)
                    {
                        TempData["SuccessMessage"] = "Property created successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to create property. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating property");
                    ModelState.AddModelError("", "An error occurred while creating the property. Please try again.");
                }
            }

            return View(viewModel);
        }

        // GET: Properties/Edit/5
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                // Get property from API
                var propertyDto = await _apiClient.GetAsync<PropertyDto>($"/api/properties/{id}");

                if (propertyDto == null)
                {
                    return NotFound();
                }

                var viewModel = new EditPropertyViewModel
                {
                    Id = propertyDto.Id,
                    Name = propertyDto.Name,
                    Description = propertyDto.Description ?? "",
                    Address = propertyDto.Address,
                    City = propertyDto.City,
                    Province = propertyDto.Province ?? "",
                    PostalCode = propertyDto.PostalCode ?? "",
                    Latitude = propertyDto.Latitude ?? 0,
                    Longitude = propertyDto.Longitude ?? 0,
                    Bedrooms = propertyDto.Bedrooms,
                    Bathrooms = propertyDto.Bathrooms,
                    SquareMeters = propertyDto.SquareMeters ?? 0,
                    MonthlyRent = propertyDto.MonthlyRent,
                    SecurityDeposit = propertyDto.DepositAmount,
                    Status = propertyDto.Status,
                    Amenities = propertyDto.Amenities,
                    ExistingImages = propertyDto.Images
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading property {id} for edit");
                return NotFound();
            }
        }

        // POST: Properties/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Edit(int id, EditPropertyViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    SetupApiAuthentication();

                    // Handle image uploads
                    var existingImages = viewModel.ExistingImages;

                    // Remove deleted images
                    if (viewModel.ImagesToDelete != null && viewModel.ImagesToDelete.Any())
                    {
                        foreach (var imageToDelete in viewModel.ImagesToDelete)
                        {
                            existingImages.Remove(imageToDelete);
                            await _fileUploadService.DeleteFileAsync(imageToDelete);
                        }
                    }

                    // Upload new images
                    if (viewModel.NewImageFiles != null && viewModel.NewImageFiles.Any())
                    {
                        foreach (var file in viewModel.NewImageFiles)
                        {
                            var imageUrl = await _fileUploadService.UploadFileAsync(file, "properties");
                            existingImages.Add(imageUrl);
                        }
                    }

                    // Create DTO for API
                    var updateDto = new UpdatePropertyDto
                    {
                        Id = viewModel.Id,
                        Name = viewModel.Name,
                        Description = viewModel.Description,
                        Address = viewModel.Address,
                        City = viewModel.City,
                        Province = viewModel.Province,
                        PostalCode = viewModel.PostalCode,
                        Latitude = viewModel.Latitude,
                        Longitude = viewModel.Longitude,
                        Bedrooms = viewModel.Bedrooms,
                        Bathrooms = viewModel.Bathrooms,
                        SquareMeters = viewModel.SquareMeters,
                        MonthlyRent = viewModel.MonthlyRent,
                        DepositAmount = viewModel.SecurityDeposit,
                        Status = viewModel.Status,
                        Amenities = viewModel.Amenities,
                        IsFurnished = false,
                        PetsAllowed = false
                    };

                    // Call API to update property
                    var response = await _apiClient.PutAsync<UpdatePropertyDto, object>($"/api/properties/{id}", updateDto);

                    if (response != null || true) // PutAsync returns null for 204 NoContent
                    {
                        TempData["SuccessMessage"] = "Property updated successfully!";
                        return RedirectToAction(nameof(Details), new { id = viewModel.Id });
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to update property. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating property");
                    ModelState.AddModelError("", "An error occurred while updating the property. Please try again.");
                }
            }

            return View(viewModel);
        }

        // GET: Properties/Delete/5
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var propertyDto = await _apiClient.GetAsync<PropertyDto>($"/api/properties/{id}");

                if (propertyDto == null)
                {
                    return NotFound();
                }

                // TODO: Check if property has active leases via API
                // For now, we'll allow deletion

                // Transform to property model for the view
                var property = new Property
                {
                    Id = propertyDto.Id,
                    Name = propertyDto.Name,
                    Address = propertyDto.Address,
                    City = propertyDto.City,
                    MonthlyRent = propertyDto.MonthlyRent,
                    Status = propertyDto.Status
                };

                return View(property);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading property {id} for deletion");
                return NotFound();
            }
        }

        // POST: Properties/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            try
            {
                SetupApiAuthentication();

                // Get property to delete its images
                var propertyDto = await _apiClient.GetAsync<PropertyDto>($"/api/properties/{id}");

                if (propertyDto != null)
                {
                    // Delete associated images
                    if (propertyDto.Images != null && propertyDto.Images.Any())
                    {
                        foreach (var image in propertyDto.Images)
                        {
                            await _fileUploadService.DeleteFileAsync(image);
                        }
                    }

                    // Call API to delete property
                    var success = await _apiClient.DeleteAsync($"/api/properties/{id}");

                    if (success)
                    {
                        TempData["SuccessMessage"] = "Property deleted successfully!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to delete property.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting property");
                TempData["ErrorMessage"] = "An error occurred while deleting the property.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
