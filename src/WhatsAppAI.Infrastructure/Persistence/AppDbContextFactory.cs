using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.Infrastructure.Persistence;

/// <summary>
/// Creates the context for EF tooling without bootstrapping the web host.
/// This keeps migrations independent from runtime-only services and credentials.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection must be set when running EF tooling.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                PersistenceServiceCollectionExtensions.LimitConnectionPool(connectionString),
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    "public"))
            .Options;

        return new AppDbContext(options, new CurrentTenant());
    }
}
