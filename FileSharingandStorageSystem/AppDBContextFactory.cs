using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FileSharingandStorageSystem
{
    // Used only by the EF Core tools (e.g. "dotnet ef migrations add") at design time.
    // Keeps migration commands working without a running database or executing the
    // app's startup logic. The connection string here is never used to connect during
    // "migrations add"; a real one is supplied at runtime via configuration.
    public class AppDBContextFactory : IDesignTimeDbContextFactory<AppDBContext>
    {
        public AppDBContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=FileSharingDb;Username=postgres;Password=postgres";

            var options = new DbContextOptionsBuilder<AppDBContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new AppDBContext(options);
        }
    }
}
