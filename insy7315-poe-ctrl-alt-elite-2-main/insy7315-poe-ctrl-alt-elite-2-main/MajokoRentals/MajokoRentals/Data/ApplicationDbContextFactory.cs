using MajokoRentals.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MajokoRentals.Data
{
    /// <summary>
    /// Design-time factory for ApplicationDbContext to support EF Core migrations
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            // Use the connection string from appsettings.json
            // Specify MajokoRentals as the migrations assembly since migrations are stored in this project
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=MajokoRentalsDb;Trusted_Connection=True;MultipleActiveResultSets=true",
                b => b.MigrationsAssembly("MajokoRentals"));

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
