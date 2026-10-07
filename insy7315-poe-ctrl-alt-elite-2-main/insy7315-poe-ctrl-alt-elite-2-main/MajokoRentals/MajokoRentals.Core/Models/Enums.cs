namespace MajokoRentals.Core.Models
{
    public enum UserRole
    {
        Admin,
        PropertyManager,
        Caretaker,
        Tenant,
        Applicant
    }

    public enum PropertyStatus
    {
        Available,
        Occupied,
        UnderMaintenance,
        Archived
    }

    public enum LeaseStatus
    {
        Active,
        Expired,
        PendingRenewal,
        Terminated,
        Draft
    }

    public enum PaymentStatus
    {
        Pending,
        Paid,
        Overdue,
        PartiallyPaid,
        Cancelled
    }

    public enum PaymentMethod
    {
        Cash,
        BankTransfer,
        Card,
        EFT,
        Cheque,
        DebitOrder
    }

    public enum MaintenanceStatus
    {
        Pending,
        InProgress,
        Resolved,
        Cancelled
    }

    public enum MaintenancePriority
    {
        Low,
        Medium,
        High,
        Emergency
    }

    public enum MaintenanceCategory
    {
        Plumbing,
        Electrical,
        Carpentry,
        Painting,
        Cleaning,
        Appliance,
        Security,
        Other
    }

    public enum ApplicationStatus
    {
        Submitted,
        UnderReview,
        Approved,
        Rejected,
        Withdrawn
    }

    public enum NotificationType
    {
        Email,
        Push,
        SMS,
        InApp
    }

    public enum Language
    {
        English,
        Zulu,
        Sesotho
    }

    public enum Theme
    {
        Light,
        Dark,
        Auto
    }

    public enum AnnouncementType
    {
        General,
        Maintenance,
        Inspection,
        Emergency,
        Event,
        RentReminder,
        PolicyUpdate
    }

    public enum AnnouncementPriority
    {
        Normal = 1,
        Important = 2,
        Urgent = 3
    }
}

