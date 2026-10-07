using MajokoRentals.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MajokoRentals.Core.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<Property> Properties { get; set; } = null!;
        public DbSet<Lease> Leases { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; } = null!;
        public DbSet<RentalApplication> RentalApplications { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Announcement> Announcements { get; set; } = null!;
        public DbSet<ChatConversation> ChatConversations { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        // Messaging & Escalation DbSets
        public DbSet<MessagingSession> MessagingSessions { get; set; } = null!;
        public DbSet<SessionMessage> SessionMessages { get; set; } = null!;
        public DbSet<ChatbotEscalation> ChatbotEscalations { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure ApplicationUser relationships
            builder.Entity<ApplicationUser>()
                .HasMany(u => u.Leases)
                .WithOne(l => l.Tenant)
                .HasForeignKey(l => l.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasMany(u => u.MaintenanceRequests)
                .WithOne(m => m.ReportedBy)
                .HasForeignKey(m => m.ReportedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasMany(u => u.AssignedMaintenanceTasks)
                .WithOne(m => m.AssignedTo)
                .HasForeignKey(m => m.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasMany(u => u.SentMessages)
                .WithOne(m => m.Sender)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasMany(u => u.ReceivedMessages)
                .WithOne(m => m.Recipient)
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Property relationships
            builder.Entity<Property>()
                .HasMany(p => p.Leases)
                .WithOne(l => l.Property)
                .HasForeignKey(l => l.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Property>()
                .HasMany(p => p.MaintenanceRequests)
                .WithOne(m => m.Property)
                .HasForeignKey(m => m.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Lease relationships
            builder.Entity<Lease>()
                .HasMany(l => l.Invoices)
                .WithOne(i => i.Lease)
                .HasForeignKey(i => i.LeaseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Invoice relationships
            builder.Entity<Invoice>()
                .HasMany(i => i.Payments)
                .WithOne(p => p.Invoice)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure ChatConversation relationships
            builder.Entity<ChatConversation>()
                .HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure decimal precision
            builder.Entity<Property>()
                .Property(p => p.MonthlyRent)
                .HasPrecision(18, 2);

            builder.Entity<Property>()
                .Property(p => p.DepositAmount)
                .HasPrecision(18, 2);

            builder.Entity<Lease>()
                .Property(l => l.MonthlyRent)
                .HasPrecision(18, 2);

            builder.Entity<Lease>()
                .Property(l => l.DepositAmount)
                .HasPrecision(18, 2);

            builder.Entity<Invoice>()
                .Property(i => i.Amount)
                .HasPrecision(18, 2);

            builder.Entity<Invoice>()
                .Property(i => i.AmountPaid)
                .HasPrecision(18, 2);

            builder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            // Indexes for performance
            builder.Entity<Property>()
                .HasIndex(p => p.Status);

            builder.Entity<Lease>()
                .HasIndex(l => l.Status);

            builder.Entity<Lease>()
                .HasIndex(l => l.EndDate);

            builder.Entity<Invoice>()
                .HasIndex(i => i.Status);

            builder.Entity<Invoice>()
                .HasIndex(i => i.DueDate);

            builder.Entity<MaintenanceRequest>()
                .HasIndex(m => m.Status);

            builder.Entity<MaintenanceRequest>()
                .HasIndex(m => m.Priority);

            builder.Entity<Message>()
                .HasIndex(m => m.IsRead);

            builder.Entity<Notification>()
                .HasIndex(n => n.IsRead);

            // Configure MessagingSession relationships
            builder.Entity<MessagingSession>()
                .HasOne(ms => ms.Initiator)
                .WithMany()
                .HasForeignKey(ms => ms.InitiatorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MessagingSession>()
                .HasOne(ms => ms.Participant)
                .WithMany()
                .HasForeignKey(ms => ms.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MessagingSession>()
                .HasOne(ms => ms.ApprovedBy)
                .WithMany()
                .HasForeignKey(ms => ms.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MessagingSession>()
                .HasMany(ms => ms.Messages)
                .WithOne(sm => sm.Session)
                .HasForeignKey(sm => sm.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MessagingSession>()
                .HasOne(ms => ms.RelatedProperty)
                .WithMany()
                .HasForeignKey(ms => ms.RelatedPropertyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MessagingSession>()
                .HasOne(ms => ms.RelatedMaintenanceRequest)
                .WithMany()
                .HasForeignKey(ms => ms.RelatedMaintenanceRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MessagingSession>()
                .HasOne(ms => ms.RelatedLease)
                .WithMany()
                .HasForeignKey(ms => ms.RelatedLeaseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MessagingSession>()
                .HasOne(ms => ms.RelatedChatbotEscalation)
                .WithOne(ce => ce.MessagingSession)
                .HasForeignKey<MessagingSession>(ms => ms.RelatedChatbotEscalationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure SessionMessage relationships
            builder.Entity<SessionMessage>()
                .HasOne(sm => sm.Sender)
                .WithMany()
                .HasForeignKey(sm => sm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SessionMessage>()
                .HasOne(sm => sm.ReplyToMessage)
                .WithMany(sm => sm.Replies)
                .HasForeignKey(sm => sm.ReplyToMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure ChatbotEscalation relationships
            builder.Entity<ChatbotEscalation>()
                .HasOne(ce => ce.ChatConversation)
                .WithMany()
                .HasForeignKey(ce => ce.ChatConversationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChatbotEscalation>()
                .HasOne(ce => ce.User)
                .WithMany()
                .HasForeignKey(ce => ce.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChatbotEscalation>()
                .HasOne(ce => ce.AssignedToAdmin)
                .WithMany()
                .HasForeignKey(ce => ce.AssignedToAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for messaging performance
            builder.Entity<MessagingSession>()
                .HasIndex(ms => ms.ApprovalStatus);

            builder.Entity<MessagingSession>()
                .HasIndex(ms => ms.IsActive);

            builder.Entity<MessagingSession>()
                .HasIndex(ms => ms.SessionType);

            builder.Entity<SessionMessage>()
                .HasIndex(sm => sm.IsRead);

            builder.Entity<SessionMessage>()
                .HasIndex(sm => sm.SentAt);

            builder.Entity<ChatbotEscalation>()
                .HasIndex(ce => ce.Status);

            builder.Entity<ChatbotEscalation>()
                .HasIndex(ce => ce.Priority);

            builder.Entity<ChatbotEscalation>()
                .HasIndex(ce => ce.CreatedAt);
        }
    }
}

