using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = Convert.ToBase64String(new byte[32]),
                ["Meta:VerifyToken"] = "test-verify-token",
                ["Meta:AppSecret"] = "test-app-secret",
                ["BootstrapAdmin:Email"] = "admin@test.com",
                ["BootstrapAdmin:Password"] = "Admin@123"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext configuration
            var descriptors = services.Where(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                     d.ServiceType == typeof(AppDbContext)).ToList();

            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            // Add SQLite in-memory database with unique name per factory instance
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite($"DataSource=file:{_dbName}?mode=memory&cache=shared");
            });
        });
    }

    public async Task<AppDbContext> GetDbContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
