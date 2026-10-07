using Microsoft.EntityFrameworkCore;
using MajokoRentals.Mobile.Data.LocalModels;

namespace MajokoRentals.Mobile.Data
{
    /// <summary>
    /// Local SQLite database context for offline data storage
    /// </summary>
    public class LocalDbContext : DbContext
    {
        public LocalDbContext(DbContextOptions<LocalDbContext> options)
            : base(options)
        {
        }

        // Local tables for offline storage
        public DbSet<LocalMaintenanceRequest> MaintenanceRequests { get; set; } = null!;
        public DbSet<LocalPayment> Payments { get; set; } = null!;
        public DbSet<SyncQueueItem> SyncQueue { get; set; } = null!;
        public DbSet<LocalProperty> Properties { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure LocalMaintenanceRequest
            modelBuilder.Entity<LocalMaintenanceRequest>(entity =>
            {
                entity.ToTable("MaintenanceRequests");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).IsRequired();
                entity.Property(e => e.ReportedById).IsRequired();
                entity.Property(e => e.Location).HasMaxLength(500);

                // Index for faster querying
                entity.HasIndex(e => e.IsSynced);
                entity.HasIndex(e => e.ReportedById);
                entity.HasIndex(e => e.Status);
            });

            // Configure LocalPayment
            modelBuilder.Entity<LocalPayment>(entity =>
            {
                entity.ToTable("Payments");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.Amount).IsRequired();
                entity.Property(e => e.PaymentMethod).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ReferenceNumber).HasMaxLength(100);

                // Index for faster querying
                entity.HasIndex(e => e.IsSynced);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.InvoiceId);
            });

            // Configure SyncQueueItem
            modelBuilder.Entity<SyncQueueItem>(entity =>
            {
                entity.ToTable("SyncQueue");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.EntityId).IsRequired();
                entity.Property(e => e.Operation).IsRequired();

                // Index for efficient queue processing
                entity.HasIndex(e => new { e.Priority, e.QueuedAt });
                entity.HasIndex(e => new { e.EntityType, e.EntityId });
            });

            // Configure LocalProperty
            modelBuilder.Entity<LocalProperty>(entity =>
            {
                entity.ToTable("Properties");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Address).IsRequired();

                // Index for faster lookups
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.CachedAt);
            });
        }
    }
}
