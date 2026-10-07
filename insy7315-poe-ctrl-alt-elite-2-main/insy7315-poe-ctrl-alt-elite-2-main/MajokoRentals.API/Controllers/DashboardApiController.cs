using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;

namespace MajokoRentals.API.Controllers
{
    [Route("api/dashboard")]
    [ApiController]
    [Authorize]
    public class DashboardApiController : ControllerBase
    {
        private readonly IFirestoreService _firestore;
        private readonly ILogger<DashboardApiController> _logger;

        public DashboardApiController(
            IFirestoreService firestore,
            ILogger<DashboardApiController> logger)
        {
            _firestore = firestore;
            _logger = logger;
        }

        /// <summary>
        /// Get admin dashboard data with aggregated metrics
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(typeof(AdminDashboardViewModel), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAdminDashboard()
        {
            try
            {
                // Fetch all necessary data from Firestore in parallel
                var propertiesTask = _firestore.GetCollectionAsync<Dictionary<string, object>>("properties");
                var leasesTask = _firestore.GetCollectionAsync<Dictionary<string, object>>("leases");
                var invoicesTask = _firestore.GetCollectionAsync<Dictionary<string, object>>("invoices");
                var applicationsTask = _firestore.GetCollectionAsync<Dictionary<string, object>>("applications");
                var maintenanceTask = _firestore.GetCollectionAsync<Dictionary<string, object>>("maintenance");

                await Task.WhenAll(propertiesTask, leasesTask, invoicesTask, applicationsTask, maintenanceTask);

                var properties = propertiesTask.Result;
                var leases = leasesTask.Result;
                var invoices = invoicesTask.Result;
                var applications = applicationsTask.Result;
                var maintenance = maintenanceTask.Result;

                // Calculate dashboard metrics
                var totalProperties = properties.Count;
                var availableProperties = properties.Count(p =>
                    p.ContainsKey("status") && p["status"]?.ToString() == "Available");
                var occupiedProperties = leases.Count(l =>
                    l.ContainsKey("status") && l["status"]?.ToString() == "Active");

                var occupancyRate = totalProperties > 0
                    ? (double)occupiedProperties / totalProperties * 100
                    : 0;

                // Active leases
                var activeLeases = leases.Where(l =>
                    l.ContainsKey("status") && l["status"]?.ToString() == "Active").ToList();

                var totalMonthlyRent = activeLeases
                    .Where(l => l.ContainsKey("monthlyRent"))
                    .Sum(l => Convert.ToDouble(l["monthlyRent"]));

                // Unpaid invoices
                var unpaidInvoices = invoices.Where(i =>
                    i.ContainsKey("status") &&
                    (i["status"]?.ToString() == "Pending" || i["status"]?.ToString() == "Overdue")).ToList();

                var totalOutstandingRent = unpaidInvoices
                    .Where(i => i.ContainsKey("amount"))
                    .Sum(i => Convert.ToDouble(i["amount"]));

                // Expiring leases (within 60 days)
                var expiringDate = DateTime.UtcNow.AddDays(60);
                var expiringLeases = activeLeases.Count(l =>
                {
                    if (l.ContainsKey("endDate"))
                    {
                        var endDate = Convert.ToDateTime(l["endDate"]);
                        return endDate <= expiringDate;
                    }
                    return false;
                });

                // Collection rate
                var totalInvoiced = invoices.Where(i => i.ContainsKey("amount"))
                    .Sum(i => Convert.ToDouble(i["amount"]));
                var totalPaid = invoices.Where(i =>
                    i.ContainsKey("status") && i["status"]?.ToString() == "Paid" && i.ContainsKey("amount"))
                    .Sum(i => Convert.ToDouble(i["amount"]));
                var collectionRate = totalInvoiced > 0 ? (totalPaid / totalInvoiced * 100) : 0;

                // Pending applications
                var pendingApplications = applications.Count(a =>
                    a.ContainsKey("status") && a["status"]?.ToString() == "Pending");

                // Maintenance statistics
                var pendingMaintenance = maintenance.Count(m =>
                    m.ContainsKey("status") && m["status"]?.ToString() == "Pending");
                var inProgressMaintenance = maintenance.Count(m =>
                    m.ContainsKey("status") && m["status"]?.ToString() == "InProgress");

                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var resolvedThisMonth = maintenance.Count(m =>
                {
                    if (m.ContainsKey("status") && m["status"]?.ToString() == "Resolved" && m.ContainsKey("resolvedDate"))
                    {
                        var resolvedDate = Convert.ToDateTime(m["resolvedDate"]);
                        return resolvedDate >= startOfMonth;
                    }
                    return false;
                });

                // Average resolution time
                var resolvedRequests = maintenance.Where(m =>
                    m.ContainsKey("status") && m["status"]?.ToString() == "Resolved" &&
                    m.ContainsKey("createdDate") && m.ContainsKey("resolvedDate")).ToList();

                var avgResolutionTime = 0.0;
                if (resolvedRequests.Any())
                {
                    var totalDays = resolvedRequests.Sum(m =>
                    {
                        var created = Convert.ToDateTime(m["createdDate"]);
                        var resolved = Convert.ToDateTime(m["resolvedDate"]);
                        return (resolved - created).TotalDays;
                    });
                    avgResolutionTime = totalDays / resolvedRequests.Count;
                }

                // Property performance (top 5 by net income)
                var propertyPerformance = properties.Select(p =>
                {
                    var propertyId = p.ContainsKey("id") ? p["id"]?.ToString() : "";
                    var propertyLeases = activeLeases.Where(l =>
                        l.ContainsKey("propertyId") && l["propertyId"]?.ToString() == propertyId).ToList();

                    var monthlyRent = propertyLeases
                        .Where(l => l.ContainsKey("monthlyRent"))
                        .Sum(l => Convert.ToDouble(l["monthlyRent"]));

                    var maintenanceCosts = maintenance
                        .Where(m => m.ContainsKey("propertyId") && m["propertyId"]?.ToString() == propertyId &&
                                   m.ContainsKey("cost") && m.ContainsKey("status") &&
                                   m["status"]?.ToString() == "Resolved")
                        .Sum(m => Convert.ToDouble(m["cost"]));

                    return new PropertyPerformanceDto
                    {
                        PropertyId = propertyId ?? "",
                        PropertyName = p.ContainsKey("name") ? p["name"]?.ToString() ?? "" : "",
                        MonthlyRent = monthlyRent,
                        MaintenanceCosts = maintenanceCosts,
                        NetIncome = monthlyRent - maintenanceCosts
                    };
                })
                .OrderByDescending(p => p.NetIncome)
                .Take(5)
                .ToList();

                // Recent activities (last 10)
                var recentActivities = new List<ActivityDto>();

                // Add recent leases
                recentActivities.AddRange(activeLeases
                    .Where(l => l.ContainsKey("startDate"))
                    .OrderByDescending(l => Convert.ToDateTime(l["startDate"]))
                    .Take(3)
                    .Select(l => new ActivityDto
                    {
                        Description = $"New lease started for {(l.ContainsKey("propertyId") ? l["propertyId"]?.ToString() : "Unknown")}",
                        Date = Convert.ToDateTime(l["startDate"]),
                        Icon = "bi bi-file-earmark-text",
                        Color = "success"
                    }));

                // Add recent applications
                recentActivities.AddRange(applications
                    .Where(a => a.ContainsKey("createdAt"))
                    .OrderByDescending(a => Convert.ToDateTime(a["createdAt"]))
                    .Take(3)
                    .Select(a => new ActivityDto
                    {
                        Description = $"New application received for {(a.ContainsKey("propertyId") ? a["propertyId"]?.ToString() : "Unknown")}",
                        Date = Convert.ToDateTime(a["createdAt"]),
                        Icon = "bi bi-person-check",
                        Color = "info"
                    }));

                // Add recent maintenance
                recentActivities.AddRange(maintenance
                    .Where(m => m.ContainsKey("createdDate"))
                    .OrderByDescending(m => Convert.ToDateTime(m["createdDate"]))
                    .Take(4)
                    .Select(m => new ActivityDto
                    {
                        Description = $"Maintenance request: {(m.ContainsKey("description") ? m["description"]?.ToString() : "N/A")}",
                        Date = Convert.ToDateTime(m["createdDate"]),
                        Icon = "bi bi-tools",
                        Color = "warning"
                    }));

                recentActivities = recentActivities
                    .OrderByDescending(a => a.Date)
                    .Take(10)
                    .ToList();

                var dashboard = new AdminDashboardViewModel
                {
                    TotalProperties = totalProperties,
                    AvailableProperties = availableProperties,
                    OccupiedProperties = occupiedProperties,
                    OccupancyRate = occupancyRate,
                    TotalMonthlyRent = totalMonthlyRent,
                    ActiveLeases = activeLeases.Count,
                    TotalOutstandingRent = totalOutstandingRent,
                    UnpaidInvoices = unpaidInvoices.Count,
                    ExpiringLeases = expiringLeases,
                    CollectionRate = collectionRate,
                    PendingApplications = pendingApplications,
                    PendingMaintenanceRequests = pendingMaintenance,
                    InProgressMaintenanceRequests = inProgressMaintenance,
                    ResolvedMaintenanceThisMonth = resolvedThisMonth,
                    AverageResolutionTime = avgResolutionTime,
                    PropertyPerformance = propertyPerformance,
                    RecentActivities = recentActivities
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin dashboard data");
                return StatusCode(500, new { message = "An error occurred while loading dashboard data", error = ex.Message });
            }
        }

        /// <summary>
        /// Get tenant dashboard data
        /// </summary>
        [HttpGet("tenant")]
        [Authorize(Roles = "Tenant")]
        [ProducesResponseType(typeof(TenantDashboardViewModel), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTenantDashboard()
        {
            try
            {
                // Get user ID from claims
                var userId = User.FindFirst("user_id")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }

                // Fetch tenant's active lease
                var leases = await _firestore.QueryCollectionAsync<Dictionary<string, object>>("leases", "tenantId", userId);
                var activeLease = leases.FirstOrDefault(l =>
                    l.ContainsKey("status") && l["status"]?.ToString() == "Active");

                if (activeLease == null)
                {
                    return Ok(new TenantDashboardViewModel
                    {
                        PropertyName = "",
                        PropertyAddress = "",
                        PropertyId = "",
                        LeaseId = "",
                        MonthlyRent = 0,
                        DaysUntilExpiry = 0,
                        OutstandingBalance = 0,
                        UnpaidInvoices = 0,
                        TotalPaidThisYear = 0,
                        OpenMaintenanceRequests = 0,
                        ResolvedMaintenanceRequests = 0
                    });
                }

                // Get lease details
                var propertyId = activeLease.ContainsKey("propertyId") ? Convert.ToInt64(activeLease["propertyId"]) : 0L;
                var leaseId = activeLease.ContainsKey("id") ? Convert.ToInt64(activeLease["id"]) : 0L;
                var leaseDocId = activeLease.ContainsKey("uid") ? activeLease["uid"]?.ToString() : "";

                // Fetch property details
                var allProperties = await _firestore.GetCollectionAsync<Dictionary<string, object>>("properties");
                var property = allProperties.FirstOrDefault(p => p.ContainsKey("id") && Convert.ToInt64(p["id"]) == propertyId);

                // Fetch tenant's invoices
                var invoices = await _firestore.QueryCollectionAsync<Dictionary<string, object>>("invoices", "tenantId", userId);

                var unpaidInvoices = invoices.Where(i =>
                    i.ContainsKey("status") &&
                    (i["status"]?.ToString() == "Pending" || i["status"]?.ToString() == "Overdue")).ToList();

                var outstandingBalance = unpaidInvoices
                    .Where(i => i.ContainsKey("amount"))
                    .Sum(i => Convert.ToDouble(i["amount"]));

                var totalPaidThisYear = invoices
                    .Where(i => i.ContainsKey("status") && i["status"]?.ToString() == "Paid" &&
                               i.ContainsKey("paidDate"))
                    .Where(i => Convert.ToDateTime(i["paidDate"]).Year == DateTime.UtcNow.Year)
                    .Sum(i => i.ContainsKey("amount") ? Convert.ToDouble(i["amount"]) : 0);

                // Fetch maintenance requests
                var maintenanceRequests = await _firestore.QueryCollectionAsync<Dictionary<string, object>>("maintenance", "reportedById", userId);

                var openRequests = maintenanceRequests.Count(m =>
                    m.ContainsKey("status") &&
                    (m["status"]?.ToString() == "Pending" || m["status"]?.ToString() == "InProgress"));

                var resolvedRequests = maintenanceRequests.Count(m =>
                    m.ContainsKey("status") && m["status"]?.ToString() == "Resolved");

                // Recent invoices (last 5)
                var recentInvoices = invoices
                    .Where(i => i.ContainsKey("dueDate"))
                    .OrderByDescending(i => Convert.ToDateTime(i["dueDate"]))
                    .Take(5)
                    .Select(i =>
                    {
                        var dueDate = i.ContainsKey("dueDate") ? Convert.ToDateTime(i["dueDate"]) : DateTime.MinValue;
                        var status = i.ContainsKey("status") ? i["status"]?.ToString() ?? "Pending" : "Pending";
                        var isOverdue = dueDate < DateTime.UtcNow && status != "Paid";

                        return new InvoiceSummaryDto
                        {
                            Id = i.ContainsKey("uid") ? i["uid"]?.ToString() ?? "" : "",
                            InvoiceNumber = i.ContainsKey("invoiceNumber") ? i["invoiceNumber"]?.ToString() ?? "" : "",
                            Amount = i.ContainsKey("amount") ? Convert.ToDouble(i["amount"]) : 0,
                            DueDate = dueDate,
                            Status = status,
                            IsOverdue = isOverdue
                        };
                    })
                    .ToList();

                // Recent maintenance requests (last 5)
                var recentMaintenanceRequests = maintenanceRequests
                    .Where(m => m.ContainsKey("reportedDate"))
                    .OrderByDescending(m => Convert.ToDateTime(m["reportedDate"]))
                    .Take(5)
                    .Select(m => new MaintenanceRequestSummaryDto
                    {
                        Id = m.ContainsKey("uid") ? m["uid"]?.ToString() ?? "" : "",
                        Title = m.ContainsKey("title") ? m["title"]?.ToString() ?? "" : "",
                        ReportedDate = m.ContainsKey("reportedDate") ? Convert.ToDateTime(m["reportedDate"]) : DateTime.MinValue,
                        Status = m.ContainsKey("status") ? m["status"]?.ToString() ?? "Pending" : "Pending",
                        Priority = m.ContainsKey("priority") ? m["priority"]?.ToString() ?? "Medium" : "Medium"
                    })
                    .ToList();

                // Next payment due date
                var nextPaymentDue = unpaidInvoices
                    .Where(i => i.ContainsKey("dueDate"))
                    .OrderBy(i => Convert.ToDateTime(i["dueDate"]))
                    .Select(i => Convert.ToDateTime(i["dueDate"]))
                    .FirstOrDefault();

                // Lease expiry
                var endDate = activeLease.ContainsKey("endDate") ? Convert.ToDateTime(activeLease["endDate"]) : DateTime.MaxValue;
                var daysUntilExpiry = (endDate - DateTime.UtcNow).Days;

                var dashboard = new TenantDashboardViewModel
                {
                    PropertyName = property != null && property.ContainsKey("name") ? property["name"]?.ToString() ?? "" : "",
                    PropertyAddress = property != null && property.ContainsKey("address") ? property["address"]?.ToString() ?? "" : "",
                    PropertyId = propertyId.ToString(),
                    LeaseId = leaseDocId ?? "",
                    LeaseStartDate = activeLease.ContainsKey("startDate") ? Convert.ToDateTime(activeLease["startDate"]) : DateTime.MinValue,
                    LeaseEndDate = endDate,
                    DaysUntilExpiry = daysUntilExpiry,
                    MonthlyRent = activeLease.ContainsKey("monthlyRent") ? Convert.ToDouble(activeLease["monthlyRent"]) : 0,
                    OutstandingBalance = outstandingBalance,
                    TotalPaidThisYear = totalPaidThisYear,
                    UnpaidInvoices = unpaidInvoices.Count,
                    OpenMaintenanceRequests = openRequests,
                    ResolvedMaintenanceRequests = resolvedRequests,
                    NextPaymentDue = nextPaymentDue != DateTime.MinValue ? (DateTime?)nextPaymentDue : null,
                    RecentInvoices = recentInvoices,
                    RecentMaintenanceRequests = recentMaintenanceRequests
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant dashboard data");
                return StatusCode(500, new { message = "An error occurred while loading dashboard data", error = ex.Message });
            }
        }
    }

    // DTOs
    public class AdminDashboardViewModel
    {
        public int TotalProperties { get; set; }
        public int AvailableProperties { get; set; }
        public int OccupiedProperties { get; set; }
        public double OccupancyRate { get; set; }
        public double TotalMonthlyRent { get; set; }
        public int ActiveLeases { get; set; }
        public double TotalOutstandingRent { get; set; }
        public int UnpaidInvoices { get; set; }
        public int ExpiringLeases { get; set; }
        public double CollectionRate { get; set; }
        public int PendingApplications { get; set; }
        public int PendingMaintenanceRequests { get; set; }
        public int InProgressMaintenanceRequests { get; set; }
        public int ResolvedMaintenanceThisMonth { get; set; }
        public double AverageResolutionTime { get; set; }
        public List<PropertyPerformanceDto> PropertyPerformance { get; set; } = new();
        public List<ActivityDto> RecentActivities { get; set; } = new();
    }

    public class TenantDashboardViewModel
    {
        public string PropertyName { get; set; } = "";
        public string PropertyAddress { get; set; } = "";
        public string PropertyId { get; set; } = "";
        public string LeaseId { get; set; } = "";
        public DateTime LeaseStartDate { get; set; }
        public DateTime LeaseEndDate { get; set; }
        public double MonthlyRent { get; set; }
        public int DaysUntilExpiry { get; set; }
        public double OutstandingBalance { get; set; }
        public int UnpaidInvoices { get; set; }
        public double TotalPaidThisYear { get; set; }
        public int OpenMaintenanceRequests { get; set; }
        public int ResolvedMaintenanceRequests { get; set; }
        public DateTime? NextPaymentDue { get; set; }
        public List<InvoiceSummaryDto> RecentInvoices { get; set; } = new();
        public List<MaintenanceRequestSummaryDto> RecentMaintenanceRequests { get; set; } = new();
    }

    public class InvoiceSummaryDto
    {
        public string Id { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";
        public DateTime DueDate { get; set; }
        public double Amount { get; set; }
        public string Status { get; set; } = "";
        public bool IsOverdue { get; set; }
    }

    public class MaintenanceRequestSummaryDto
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime ReportedDate { get; set; }
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
    }

    public class PropertyPerformanceDto
    {
        public string PropertyId { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public double MonthlyRent { get; set; }
        public double MaintenanceCosts { get; set; }
        public double NetIncome { get; set; }
    }

    public class ActivityDto
    {
        public string Description { get; set; } = "";
        public DateTime Date { get; set; }
        public string Icon { get; set; } = "bi bi-circle-fill";
        public string Color { get; set; } = "primary";
    }
}
