using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using MajokoRentals.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MajokoRentals.Controllers
{
    [Authorize]
    public class LeasesController : Controller
    {
        private readonly IApiClientService _apiClient;
        private readonly IFileUploadService _fileUploadService;
        private readonly IEmailService _emailService;
        private readonly IUserProfileService _userProfile;
        private readonly ILogger<LeasesController> _logger;

        public LeasesController(
            IApiClientService apiClient,
            IFileUploadService fileUploadService,
            IEmailService emailService,
            IUserProfileService userProfile,
            ILogger<LeasesController> logger)
        {
            _apiClient = apiClient;
            _fileUploadService = fileUploadService;
            _emailService = emailService;
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

        // GET: Leases
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Index(LeaseStatus? status, bool? expiringSoon)
        {
            try
            {
                SetupApiAuthentication();

                // Build query string
                var queryParams = new List<string>();
                if (status.HasValue) queryParams.Add($"status={status.Value}");
                if (expiringSoon.HasValue) queryParams.Add($"expiringSoon={expiringSoon.Value}");

                var endpoint = "/api/leases";
                if (queryParams.Any())
                {
                    endpoint += "?" + string.Join("&", queryParams);
                }

                var leasesData = await _apiClient.GetAsync<List<LeaseDto>>(endpoint);

                if (leasesData == null)
                {
                    leasesData = new List<LeaseDto>();
                }

                // Transform to ViewModels
                var leases = leasesData.Select(l => new LeaseListViewModel
                {
                    Id = l.Id,
                    PropertyName = l.PropertyName,
                    PropertyAddress = l.PropertyAddress,
                    TenantName = l.TenantName,
                    TenantEmail = l.TenantEmail,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    MonthlyRent = l.MonthlyRent,
                    Status = l.Status,
                    DaysUntilExpiry = l.DaysUntilExpiry,
                    IsExpiringSoon = l.IsExpiringSoon,
                    IsExpired = l.IsExpired
                }).ToList();

                ViewBag.Status = status;
                ViewBag.ExpiringSoon = expiringSoon;

                return View(leases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading leases");
                TempData["ErrorMessage"] = "Unable to load leases. Please try again.";
                return View(new List<LeaseListViewModel>());
            }
        }

        // GET: Leases/MyLease - For tenants to view their own lease
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> MyLease()
        {
            try
            {
                SetupApiAuthentication();

                // Get all leases - API will filter for tenant automatically
                var leasesData = await _apiClient.GetAsync<List<LeaseDto>>("/api/leases?status=Active");

                if (leasesData == null || !leasesData.Any())
                {
                    return View("NoActiveLease");
                }

                var lease = leasesData.First();

                // Get invoices for this lease
                var invoices = await _apiClient.GetAsync<List<InvoiceDto>>("/api/invoices");

                // Get maintenance requests for this property
                var maintenanceRequests = await _apiClient.GetAsync<List<MaintenanceRequestDto>>("/api/maintenance");

                var viewModel = new LeaseDetailsViewModel
                {
                    Id = lease.Id,
                    PropertyId = lease.PropertyId,
                    PropertyName = lease.PropertyName,
                    PropertyAddress = lease.PropertyAddress,
                    TenantId = lease.TenantId,
                    TenantName = lease.TenantName,
                    TenantEmail = lease.TenantEmail,
                    TenantPhone = "",
                    StartDate = lease.StartDate,
                    EndDate = lease.EndDate,
                    MonthlyRent = lease.MonthlyRent,
                    SecurityDeposit = lease.DepositAmount,
                    Status = lease.Status,
                    LeaseDocumentUrl = "",
                    CreatedAt = DateTime.UtcNow,
                    DaysUntilExpiry = lease.DaysUntilExpiry,
                    IsExpiringSoon = lease.IsExpiringSoon,
                    IsExpired = lease.IsExpired,
                    Invoices = new List<Invoice>(),
                    MaintenanceRequests = new List<MaintenanceRequest>()
                };

                return View("Details", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tenant's lease");
                return View("NoActiveLease");
            }
        }

        // GET: Leases/Details/5
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Details(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var lease = await _apiClient.GetAsync<LeaseDto>($"/api/leases/{id}");

                if (lease == null)
                {
                    return NotFound();
                }

                // Get invoices for this lease
                var invoices = await _apiClient.GetAsync<List<InvoiceDto>>("/api/invoices");

                // Get maintenance requests for this property
                var maintenanceRequests = await _apiClient.GetAsync<List<MaintenanceRequestDto>>("/api/maintenance");

                var viewModel = new LeaseDetailsViewModel
                {
                    Id = lease.Id,
                    PropertyId = lease.PropertyId,
                    PropertyName = lease.PropertyName,
                    PropertyAddress = lease.PropertyAddress,
                    TenantId = lease.TenantId,
                    TenantName = lease.TenantName,
                    TenantEmail = lease.TenantEmail,
                    TenantPhone = "",
                    StartDate = lease.StartDate,
                    EndDate = lease.EndDate,
                    MonthlyRent = lease.MonthlyRent,
                    SecurityDeposit = lease.DepositAmount,
                    Status = lease.Status,
                    LeaseDocumentUrl = "",
                    CreatedAt = DateTime.UtcNow,
                    DaysUntilExpiry = lease.DaysUntilExpiry,
                    IsExpiringSoon = lease.IsExpiringSoon,
                    IsExpired = lease.IsExpired,
                    Invoices = new List<Invoice>(),
                    MaintenanceRequests = new List<MaintenanceRequest>()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading lease {id}");
                return NotFound();
            }
        }

        // GET: Leases/Create
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Create()
        {
            SetupApiAuthentication();
            await PopulateDropdowns();
            return View(new CreateLeaseViewModel());
        }

        // POST: Leases/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Create(CreateLeaseViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    SetupApiAuthentication();

                    // Upload lease document if provided
                    string? documentUrl = null;
                    if (viewModel.LeaseDocument != null)
                    {
                        documentUrl = await _fileUploadService.UploadFileAsync(viewModel.LeaseDocument, "leases");
                    }

                    var createDto = new CreateLeaseDto
                    {
                        PropertyId = viewModel.PropertyId,
                        TenantId = viewModel.TenantId,
                        StartDate = viewModel.StartDate,
                        EndDate = viewModel.EndDate,
                        MonthlyRent = viewModel.MonthlyRent,
                        DepositAmount = viewModel.SecurityDeposit
                    };

                    var response = await _apiClient.PostAsync<CreateLeaseDto, object>("/api/leases", createDto);

                    if (response != null)
                    {
                        // Send email notification to tenant (non-critical)
                        try
                        {
                            var tenant = await _userProfile.GetUserProfileAsync(viewModel.TenantId);
                            if (tenant?.Email != null)
                            {
                                await _emailService.SendEmailAsync(
                                    tenant.Email,
                                    "New Lease Agreement",
                                    $"Your lease agreement has been created. Start date: {viewModel.StartDate:MMM dd, yyyy}, End date: {viewModel.EndDate:MMM dd, yyyy}."
                                );
                            }
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogWarning(emailEx, "Failed to send lease creation email notification");
                        }

                        TempData["SuccessMessage"] = "Lease created successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to create lease. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating lease");
                    ModelState.AddModelError("", "An error occurred while creating the lease.");
                }
            }

            await PopulateDropdowns(viewModel.PropertyId, viewModel.TenantId);
            return View(viewModel);
        }

        // GET: Leases/Edit/5
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Edit(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var lease = await _apiClient.GetAsync<LeaseDto>($"/api/leases/{id}");
                if (lease == null)
                {
                    return NotFound();
                }

                var viewModel = new EditLeaseViewModel
                {
                    Id = lease.Id,
                    PropertyId = lease.PropertyId,
                    TenantId = lease.TenantId,
                    StartDate = lease.StartDate,
                    EndDate = lease.EndDate,
                    MonthlyRent = lease.MonthlyRent,
                    SecurityDeposit = lease.DepositAmount,
                    Status = lease.Status,
                    ExistingLeaseDocumentUrl = ""
                };

                await PopulateDropdowns(viewModel.PropertyId, viewModel.TenantId);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading lease {id} for edit");
                return NotFound();
            }
        }

        // POST: Leases/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Edit(string id, EditLeaseViewModel viewModel)
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

                    // Upload new document if provided
                    string? documentUrl = null;
                    if (viewModel.NewLeaseDocument != null)
                    {
                        // Delete old document
                        if (!string.IsNullOrEmpty(viewModel.ExistingLeaseDocumentUrl))
                        {
                            await _fileUploadService.DeleteFileAsync(viewModel.ExistingLeaseDocumentUrl);
                        }
                        documentUrl = await _fileUploadService.UploadFileAsync(viewModel.NewLeaseDocument, "leases");
                    }

                    var updateDto = new UpdateLeaseDto
                    {
                        PropertyId = viewModel.PropertyId,
                        TenantId = viewModel.TenantId,
                        StartDate = viewModel.StartDate,
                        EndDate = viewModel.EndDate,
                        MonthlyRent = viewModel.MonthlyRent,
                        DepositAmount = viewModel.SecurityDeposit,
                        LeaseDocumentUrl = documentUrl ?? viewModel.ExistingLeaseDocumentUrl,
                        Status = viewModel.Status
                    };

                    // PutAsync returns null for 204 No Content (successful update with no response body)
                    await _apiClient.PutAsync<UpdateLeaseDto, object>($"/api/leases/{id}", updateDto);

                    TempData["SuccessMessage"] = "Lease updated successfully!";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating lease");
                    ModelState.AddModelError("", "An error occurred while updating the lease.");
                }
            }

            await PopulateDropdowns(viewModel.PropertyId, viewModel.TenantId);
            return View(viewModel);
        }

        // GET: Leases/Renew/5
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Renew(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var lease = await _apiClient.GetAsync<LeaseDto>($"/api/leases/{id}");

                if (lease == null)
                {
                    return NotFound();
                }

                var viewModel = new RenewLeaseViewModel
                {
                    LeaseId = lease.Id,
                    PropertyName = lease.PropertyName,
                    TenantName = lease.TenantName,
                    CurrentEndDate = lease.EndDate,
                    CurrentMonthlyRent = lease.MonthlyRent,
                    NewStartDate = lease.EndDate.AddDays(1),
                    NewEndDate = lease.EndDate.AddYears(1),
                    MonthlyRent = lease.MonthlyRent
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading lease {id} for renewal");
                return NotFound();
            }
        }

        // POST: Leases/Renew/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Renew(string id, RenewLeaseViewModel viewModel)
        {
            if (id != viewModel.LeaseId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    SetupApiAuthentication();

                    // Upload new lease document if provided
                    string? documentUrl = null;
                    if (viewModel.LeaseDocument != null)
                    {
                        documentUrl = await _fileUploadService.UploadFileAsync(viewModel.LeaseDocument, "leases");
                    }

                    var renewDto = new RenewLeaseDto
                    {
                        NewStartDate = viewModel.NewStartDate,
                        NewEndDate = viewModel.NewEndDate,
                        NewMonthlyRent = viewModel.MonthlyRent
                    };

                    var response = await _apiClient.PostAsync<RenewLeaseDto, object>($"/api/leases/{id}/renew", renewDto);

                    if (response != null)
                    {
                        // Send email notification to tenant (non-critical)
                        try
                        {
                            var lease = await _apiClient.GetAsync<LeaseDto>($"/api/leases/{id}");
                            if (lease != null)
                            {
                                var tenant = await _userProfile.GetUserProfileAsync(lease.TenantId);
                                if (tenant?.Email != null)
                                {
                                    await _emailService.SendEmailAsync(
                                        tenant.Email,
                                        "Lease Renewed",
                                        $"Your lease has been renewed! New start date: {viewModel.NewStartDate:MMM dd, yyyy}, End date: {viewModel.NewEndDate:MMM dd, yyyy}."
                                    );
                                }
                            }
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogWarning(emailEx, "Failed to send lease renewal email notification");
                        }

                        TempData["SuccessMessage"] = "Lease renewed successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to renew lease. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error renewing lease");
                    ModelState.AddModelError("", "An error occurred while renewing the lease.");
                }
            }

            return View(viewModel);
        }

        // GET: Leases/Terminate/5
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Terminate(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var lease = await _apiClient.GetAsync<LeaseDto>($"/api/leases/{id}");

                if (lease == null)
                {
                    return NotFound();
                }

                var viewModel = new TerminateLeaseViewModel
                {
                    LeaseId = lease.Id,
                    PropertyName = lease.PropertyName,
                    TenantName = lease.TenantName,
                    StartDate = lease.StartDate,
                    EndDate = lease.EndDate,
                    MonthlyRent = lease.MonthlyRent,
                    TerminationDate = DateTime.Today
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading lease {id} for termination");
                return NotFound();
            }
        }

        // POST: Leases/Terminate/5
        [HttpPost, ActionName("Terminate")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> TerminateConfirmed(string id, TerminateLeaseViewModel viewModel)
        {
            try
            {
                SetupApiAuthentication();

                var terminateDto = new TerminateLeaseDto
                {
                    TerminationDate = viewModel.TerminationDate,
                    Reason = viewModel.Reason
                };

                var response = await _apiClient.PostAsync<TerminateLeaseDto, object>($"/api/leases/{id}/terminate", terminateDto);

                // Send email notification to tenant (non-critical)
                try
                {
                    var lease = await _apiClient.GetAsync<LeaseDto>($"/api/leases/{id}");
                    if (lease != null)
                    {
                        var tenant = await _userProfile.GetUserProfileAsync(lease.TenantId);
                        if (tenant?.Email != null)
                        {
                            await _emailService.SendEmailAsync(
                                tenant.Email,
                                "Lease Terminated",
                                "Your lease has been terminated. Please contact the property manager for more details."
                            );
                        }
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Failed to send lease termination email notification");
                }

                TempData["SuccessMessage"] = "Lease terminated successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating lease");
                TempData["ErrorMessage"] = "An error occurred while terminating the lease.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns(long? selectedPropertyId = null, string? selectedTenantId = null)
        {
            try
            {
                // Get available properties via API
                var properties = await _apiClient.GetAsync<List<PropertyListDto>>("/api/properties?status=Available");

                ViewBag.Properties = properties?.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Name} - {p.Address}",
                    Selected = p.Id == selectedPropertyId
                }).ToList() ?? new List<SelectListItem>();

                // Get tenants from Firebase/Firestore
                var tenantProfiles = await _userProfile.GetUsersByRoleAsync("Tenant");
                var tenants = tenantProfiles
                    .OrderBy(u => u.FirstName)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Uid,
                        Text = $"{u.FirstName} {u.LastName} ({u.Email})",
                        Selected = u.Uid == selectedTenantId
                    })
                    .ToList();

                ViewBag.Tenants = tenants;
            }
            catch
            {
                ViewBag.Properties = new List<SelectListItem>();
                ViewBag.Tenants = new List<SelectListItem>();
            }
        }
    }

    // DTOs for API calls
    public class LeaseDto
    {
        public string Id { get; set; } = string.Empty;
        public long PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string TenantEmail { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double MonthlyRent { get; set; }
        public double DepositAmount { get; set; }
        public LeaseStatus Status { get; set; }
        public int DaysUntilExpiry { get; set; }
        public bool IsExpiringSoon { get; set; }
        public bool IsExpired { get; set; }
    }

    public class CreateLeaseDto
    {
        public long PropertyId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double MonthlyRent { get; set; }
        public double DepositAmount { get; set; }
    }

    public class UpdateLeaseDto
    {
        public long PropertyId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double MonthlyRent { get; set; }
        public double DepositAmount { get; set; }
        public string? LeaseDocumentUrl { get; set; }
        public LeaseStatus Status { get; set; }
    }

    public class RenewLeaseDto
    {
        public DateTime NewStartDate { get; set; }
        public DateTime NewEndDate { get; set; }
        public double NewMonthlyRent { get; set; }
    }

    public class TerminateLeaseDto
    {
        public DateTime TerminationDate { get; set; }
        public string? Reason { get; set; }
    }
}
