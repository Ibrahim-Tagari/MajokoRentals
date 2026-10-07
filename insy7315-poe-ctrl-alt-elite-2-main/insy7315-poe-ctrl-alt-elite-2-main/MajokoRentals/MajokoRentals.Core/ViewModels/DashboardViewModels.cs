using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.ViewModels
{
    public class AdminDashboardViewModel
    {
        // Property Statistics
        public int TotalProperties { get; set; }
        public int AvailableProperties { get; set; }
        public int OccupiedProperties { get; set; }
        public int UnderMaintenanceProperties { get; set; }
        public double OccupancyRate { get; set; }

        // Lease Statistics
        public int ActiveLeases { get; set; }
        public int ExpiringLeases { get; set; } // Within 60 days
        public int ExpiredLeases { get; set; }

        // Financial Statistics
        public double TotalMonthlyRent { get; set; }
        public double TotalOutstandingRent { get; set; }
        public double CollectionRate { get; set; }
        public int UnpaidInvoices { get; set; }
        public double TotalPaidThisMonth { get; set; }

        // Application Statistics
        public int PendingApplications { get; set; }
        public int ApprovedApplicationsThisMonth { get; set; }
        public int RejectedApplicationsThisMonth { get; set; }

        // Maintenance Statistics
        public int PendingMaintenanceRequests { get; set; }
        public int InProgressMaintenanceRequests { get; set; }
        public int ResolvedMaintenanceThisMonth { get; set; }
        public double AverageResolutionTime { get; set; } // In days

        // Messaging Statistics
        public int PendingChatRequests { get; set; }

        // Announcement Statistics
        public List<AnnouncementDto> RecentAnnouncements { get; set; } = new();

        // Recent Activity
        public List<RecentActivityViewModel> RecentActivities { get; set; } = new();

        // Property Performance
        public List<PropertyPerformanceViewModel> PropertyPerformance { get; set; } = new();

        // Monthly Revenue Chart Data
        public List<MonthlyRevenueViewModel> MonthlyRevenue { get; set; } = new();
    }

    public class TenantDashboardViewModel
    {
        // Lease Information
        public string LeaseId { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;
        public DateTime LeaseStartDate { get; set; }
        public DateTime LeaseEndDate { get; set; }
        public int DaysUntilExpiry { get; set; }
        public double MonthlyRent { get; set; }
        public long PropertyId { get; set; }

        // Payment Information
        public double OutstandingBalance { get; set; }
        public int UnpaidInvoices { get; set; }
        public DateTime? NextPaymentDue { get; set; }
        public double TotalPaidThisYear { get; set; }

        // Maintenance Requests
        public int OpenMaintenanceRequests { get; set; }
        public int ResolvedMaintenanceRequests { get; set; }

        // Announcements
        public List<AnnouncementDto> RecentAnnouncements { get; set; } = new();

        // Recent Invoices
        public List<InvoiceSummaryViewModel> RecentInvoices { get; set; } = new();

        // Recent Maintenance
        public List<MaintenanceRequestSummaryViewModel> RecentMaintenanceRequests { get; set; } = new();
    }

    public class RecentActivityViewModel
    {
        public string Type { get; set; } = string.Empty; // "Payment", "Lease", "Maintenance", "Application"
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = "primary";
    }

    public class PropertyPerformanceViewModel
    {
        public long PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public double MonthlyRent { get; set; }
        public int OccupancyMonths { get; set; }
        public int VacantMonths { get; set; }
        public int MaintenanceRequests { get; set; }
        public double MaintenanceCosts { get; set; }
        public double NetIncome { get; set; }
    }

    public class MonthlyRevenueViewModel
    {
        public string Month { get; set; } = string.Empty;
        public double ExpectedRevenue { get; set; }
        public double ActualRevenue { get; set; }
        public double CollectionRate { get; set; }
    }

    public class InvoiceSummaryViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public double Amount { get; set; }
        public double AmountPaid { get; set; }
        public PaymentStatus Status { get; set; }
        public bool IsOverdue => DueDate < DateTime.UtcNow && Status != PaymentStatus.Paid;
    }

    public class MaintenanceRequestSummaryViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime ReportedDate { get; set; }
        public MaintenanceStatus Status { get; set; }
        public MaintenancePriority Priority { get; set; }
    }
}

