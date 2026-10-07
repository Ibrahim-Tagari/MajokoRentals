using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using MajokoRentals.Core.ViewModels;
using System.Security.Claims;

namespace MajokoRentals.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IApiClientService _apiClient;
        private readonly ILogger<DashboardController> _logger;
        private readonly IMessagingService _messagingService;
        private readonly IAnnouncementService _announcementService;

        public DashboardController(
            IApiClientService apiClient,
            ILogger<DashboardController> logger,
            IMessagingService messagingService,
            IAnnouncementService announcementService)
        {
            _apiClient = apiClient;
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

        // GET: Dashboard
        public IActionResult Index()
        {
            if (User.IsInRole("Admin") || User.IsInRole("PropertyManager"))
            {
                return RedirectToAction(nameof(AdminDashboard));
            }
            else if (User.IsInRole("Tenant"))
            {
                return RedirectToAction(nameof(TenantDashboard));
            }
            else if (User.IsInRole("Caretaker"))
            {
                return RedirectToAction("Index", "Maintenance");
            }
            else
            {
                return RedirectToAction("MyApplications", "RentalApplications");
            }
        }

        // GET: Dashboard/AdminDashboard
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> AdminDashboard()
        {
            try
            {
                SetupApiAuthentication();

                var now = DateTime.UtcNow;
                var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
                var firstDayOfYear = new DateTime(now.Year, 1, 1);

                // Make parallel API calls to fetch all data
                var propertiesTask = _apiClient.GetAsync<List<PropertyDto>>("/api/properties");
                var leasesTask = _apiClient.GetAsync<List<LeaseDto>>("/api/leases");
                var invoicesTask = _apiClient.GetAsync<List<InvoiceDto>>("/api/invoices");
                var applicationsTask = _apiClient.GetAsync<List<RentalApplicationDto>>("/api/applications");
                var maintenanceTask = _apiClient.GetAsync<List<MaintenanceRequestDto>>("/api/maintenance");

                await Task.WhenAll(propertiesTask, leasesTask, invoicesTask, applicationsTask, maintenanceTask);

                var properties = await propertiesTask ?? new List<PropertyDto>();
                var leases = await leasesTask ?? new List<LeaseDto>();
                var invoices = await invoicesTask ?? new List<InvoiceDto>();
                var applications = await applicationsTask ?? new List<RentalApplicationDto>();
                var maintenance = await maintenanceTask ?? new List<MaintenanceRequestDto>();

                // Property Statistics
                var totalProperties = properties.Count;
                var availableProperties = properties.Count(p => p.Status == PropertyStatus.Available);
                var occupiedProperties = properties.Count(p => p.Status == PropertyStatus.Occupied);
                var underMaintenanceProperties = properties.Count(p => p.Status == PropertyStatus.UnderMaintenance);
                var occupancyRate = totalProperties > 0 ? (double)occupiedProperties / totalProperties * 100 : 0;

                // Lease Statistics
                var activeLeases = leases.Count(l => l.Status == LeaseStatus.Active);
                var expiringLeases = leases.Count(l => l.Status == LeaseStatus.Active && l.EndDate <= now.AddDays(60) && l.EndDate > now);
                var expiredLeases = leases.Count(l => l.EndDate < now && l.Status == LeaseStatus.Active);

                // Financial Statistics
                var totalMonthlyRent = leases.Where(l => l.Status == LeaseStatus.Active).Sum(l => l.MonthlyRent);
                var unpaidInvoices = invoices.Where(i => i.Status != PaymentStatus.Paid).ToList();
                var totalOutstandingRent = unpaidInvoices.Sum(i => i.AmountDue);
                var unpaidInvoicesCount = unpaidInvoices.Count;

                var allPayments = invoices.SelectMany(i => i.Payments ?? new List<PaymentDto>()).ToList();
                var paidThisMonth = allPayments.Where(p => p.PaymentDate >= firstDayOfMonth).Sum(p => p.Amount);

                var totalInvoiced = invoices.Sum(i => i.Amount);
                var totalPaid = invoices.Sum(i => i.AmountPaid);
                var collectionRate = totalInvoiced > 0 ? (totalPaid / totalInvoiced) * 100 : 0;

                // Application Statistics
                var pendingApplications = applications.Count(a => a.Status == ApplicationStatus.Submitted);
                var approvedThisMonth = applications.Count(a => a.Status == ApplicationStatus.Approved && a.ReviewedDate >= firstDayOfMonth);
                var rejectedThisMonth = applications.Count(a => a.Status == ApplicationStatus.Rejected && a.ReviewedDate >= firstDayOfMonth);

                // Maintenance Statistics
                var pendingMaintenance = maintenance.Count(m => m.Status == MaintenanceStatus.Pending);
                var inProgressMaintenance = maintenance.Count(m => m.Status == MaintenanceStatus.InProgress);
                var resolvedThisMonth = maintenance.Count(m => m.Status == MaintenanceStatus.Resolved && m.CompletedDate >= firstDayOfMonth);

                var resolvedRequests = maintenance.Where(m => m.Status == MaintenanceStatus.Resolved && m.CompletedDate.HasValue).ToList();
                var avgResolutionTime = resolvedRequests.Any()
                    ? resolvedRequests.Average(m => (m.CompletedDate!.Value - m.ReportedDate).TotalDays)
                    : 0;

                // Messaging Statistics
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var pendingChatsSummary = await _messagingService.GetPendingSessionsAsync(userId ?? "");
                var pendingChatRequests = pendingChatsSummary.TotalPending;

                // Announcements
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var recentAnnouncements = await _announcementService.GetAnnouncementsForUserAsync(userId ?? "", userRole ?? "", null);
                recentAnnouncements = recentAnnouncements.Take(5).ToList();

                // Recent Activity
                var recentActivities = new List<RecentActivityViewModel>();

                // Recent payments (from invoices)
                var recentPayments = allPayments.OrderByDescending(p => p.PaymentDate).Take(5).ToList();
                recentActivities.AddRange(recentPayments.Select(p => new RecentActivityViewModel
                {
                    Type = "Payment",
                    Description = $"{p.TenantName} paid R{p.Amount:N2}",
                    Date = p.PaymentDate,
                    Icon = "bi-currency-dollar",
                    Color = "success"
                }));

                // Recent maintenance
                var recentMaintenance = maintenance.OrderByDescending(m => m.ReportedDate).Take(5).ToList();
                recentActivities.AddRange(recentMaintenance.Select(m => new RecentActivityViewModel
                {
                    Type = "Maintenance",
                    Description = $"Maintenance request: {m.Title} at {m.PropertyName}",
                    Date = m.ReportedDate,
                    Icon = "bi-tools",
                    Color = m.Status == MaintenanceStatus.Resolved ? "success" : "warning"
                }));

                // Recent applications
                var recentApplications = applications.OrderByDescending(a => a.ApplicationDate).Take(5).ToList();
                recentActivities.AddRange(recentApplications.Select(a => new RecentActivityViewModel
                {
                    Type = "Application",
                    Description = $"New application for {a.PropertyName} from {a.ApplicantName}",
                    Date = a.ApplicationDate,
                    Icon = "bi-file-earmark-text",
                    Color = "info"
                }));

                recentActivities = recentActivities.OrderByDescending(a => a.Date).Take(10).ToList();

                // Property Performance (simplified - actual costs not available in DTOs)
                var propertyPerformance = properties.Select(p =>
                {
                    var propertyLeases = leases.Where(l => l.PropertyId == p.Id && l.Status == LeaseStatus.Active).ToList();
                    var propertyMaintenance = maintenance.Where(m => m.PropertyId == p.Id && m.ReportedDate >= firstDayOfYear).ToList();

                    return new PropertyPerformanceViewModel
                    {
                        PropertyId = p.Id,
                        PropertyName = p.Name,
                        MonthlyRent = p.MonthlyRent,
                        MaintenanceRequests = propertyMaintenance.Count,
                        MaintenanceCosts = propertyMaintenance.Sum(m => m.ActualCost ?? 0),
                        NetIncome = propertyLeases.Sum(l => l.MonthlyRent) * 12 - propertyMaintenance.Sum(m => m.ActualCost ?? 0)
                    };
                }).OrderByDescending(p => p.NetIncome).ToList();

                // Monthly Revenue (last 6 months - simplified)
                var monthlyRevenue = new List<MonthlyRevenueViewModel>();
                for (int i = 5; i >= 0; i--)
                {
                    var month = now.AddMonths(-i);
                    var monthStart = new DateTime(month.Year, month.Month, 1);
                    var monthEnd = monthStart.AddMonths(1);

                    var expectedRevenue = leases
                        .Where(l => l.StartDate <= monthStart && l.EndDate >= monthStart && l.Status == LeaseStatus.Active)
                        .Sum(l => l.MonthlyRent);

                    var actualRevenue = allPayments
                        .Where(p => p.PaymentDate >= monthStart && p.PaymentDate < monthEnd)
                        .Sum(p => p.Amount);

                    monthlyRevenue.Add(new MonthlyRevenueViewModel
                    {
                        Month = month.ToString("MMM yyyy"),
                        ExpectedRevenue = expectedRevenue,
                        ActualRevenue = actualRevenue,
                        CollectionRate = expectedRevenue > 0 ? (actualRevenue / expectedRevenue) * 100 : 0
                    });
                }

                var viewModel = new AdminDashboardViewModel
                {
                    TotalProperties = totalProperties,
                    AvailableProperties = availableProperties,
                    OccupiedProperties = occupiedProperties,
                    UnderMaintenanceProperties = underMaintenanceProperties,
                    OccupancyRate = occupancyRate,
                    ActiveLeases = activeLeases,
                    ExpiringLeases = expiringLeases,
                    ExpiredLeases = expiredLeases,
                    TotalMonthlyRent = totalMonthlyRent,
                    TotalOutstandingRent = totalOutstandingRent,
                    CollectionRate = collectionRate,
                    UnpaidInvoices = unpaidInvoicesCount,
                    TotalPaidThisMonth = paidThisMonth,
                    PendingApplications = pendingApplications,
                    ApprovedApplicationsThisMonth = approvedThisMonth,
                    RejectedApplicationsThisMonth = rejectedThisMonth,
                    PendingMaintenanceRequests = pendingMaintenance,
                    InProgressMaintenanceRequests = inProgressMaintenance,
                    ResolvedMaintenanceThisMonth = resolvedThisMonth,
                    AverageResolutionTime = (double)avgResolutionTime,
                    PendingChatRequests = pendingChatRequests,
                    RecentAnnouncements = recentAnnouncements,
                    RecentActivities = recentActivities,
                    PropertyPerformance = propertyPerformance,
                    MonthlyRevenue = monthlyRevenue
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["ErrorMessage"] = "Unable to load dashboard data. Please try again.";
                return View(new AdminDashboardViewModel());
            }
        }

        // GET: Dashboard/TenantDashboard
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> TenantDashboard()
        {
            try
            {
                SetupApiAuthentication();

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Get active lease (API automatically filters for tenant)
                var leases = await _apiClient.GetAsync<List<LeaseDto>>("/api/leases?status=Active");

                if (leases == null || !leases.Any())
                {
                    TempData["ErrorMessage"] = "No active lease found. Please contact the property manager.";
                    return RedirectToAction("Index", "Home");
                }

                var lease = leases.First();

                // Get invoices and maintenance in parallel
                var invoicesTask = _apiClient.GetAsync<List<InvoiceDto>>("/api/invoices");
                var maintenanceTask = _apiClient.GetAsync<List<MaintenanceRequestDto>>("/api/maintenance");

                await Task.WhenAll(invoicesTask, maintenanceTask);

                var invoices = await invoicesTask ?? new List<InvoiceDto>();
                var maintenanceRequests = await maintenanceTask ?? new List<MaintenanceRequestDto>();

                // Payment Information
                var unpaidInvoices = invoices.Where(i => i.Status != PaymentStatus.Paid).ToList();
                var outstandingBalance = unpaidInvoices.Sum(i => i.AmountDue);
                var unpaidInvoicesCount = unpaidInvoices.Count;

                var nextInvoice = unpaidInvoices.OrderBy(i => i.DueDate).FirstOrDefault();

                var firstDayOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1);
                var allPayments = invoices.SelectMany(i => i.Payments ?? new List<PaymentDto>()).ToList();
                var totalPaidThisYear = allPayments
                    .Where(p => p.PaymentDate >= firstDayOfYear)
                    .Sum(p => p.Amount);

                // Maintenance Requests
                var openMaintenance = maintenanceRequests.Count(m => m.Status != MaintenanceStatus.Resolved);
                var resolvedMaintenance = maintenanceRequests.Count(m => m.Status == MaintenanceStatus.Resolved);

                // Recent Invoices
                var recentInvoices = invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(5)
                    .Select(i => new InvoiceSummaryViewModel
                    {
                        Id = i.Id,
                        InvoiceNumber = i.InvoiceNumber,
                        InvoiceDate = i.InvoiceDate,
                        DueDate = i.DueDate,
                        Amount = i.Amount,
                        AmountPaid = i.AmountPaid,
                        Status = i.Status
                    })
                    .ToList();

                // Recent Maintenance Requests
                var recentMaintenanceRequests = maintenanceRequests
                    .OrderByDescending(m => m.ReportedDate)
                    .Take(5)
                    .Select(m => new MaintenanceRequestSummaryViewModel
                    {
                        Id = m.Id,
                        Title = m.Title,
                        ReportedDate = m.ReportedDate,
                        Status = m.Status,
                        Priority = m.Priority
                    })
                    .ToList();

                // Announcements for tenant
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var propertyIds = new List<long> { lease.PropertyId };
                var recentAnnouncements = await _announcementService.GetAnnouncementsForUserAsync(userId ?? "", userRole ?? "", propertyIds);
                recentAnnouncements = recentAnnouncements.Take(5).ToList();

                var viewModel = new TenantDashboardViewModel
                {
                    LeaseId = lease.Id,
                    PropertyName = lease.PropertyName,
                    PropertyAddress = lease.PropertyAddress,
                    LeaseStartDate = lease.StartDate,
                    LeaseEndDate = lease.EndDate,
                    DaysUntilExpiry = (lease.EndDate - DateTime.UtcNow).Days,
                    MonthlyRent = lease.MonthlyRent,
                    PropertyId = lease.PropertyId,
                    OutstandingBalance = outstandingBalance,
                    UnpaidInvoices = unpaidInvoicesCount,
                    NextPaymentDue = nextInvoice?.DueDate,
                    TotalPaidThisYear = totalPaidThisYear,
                    OpenMaintenanceRequests = openMaintenance,
                    ResolvedMaintenanceRequests = resolvedMaintenance,
                    RecentAnnouncements = recentAnnouncements,
                    RecentInvoices = recentInvoices,
                    RecentMaintenanceRequests = recentMaintenanceRequests
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tenant dashboard");
                TempData["ErrorMessage"] = "Unable to load dashboard data. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}
