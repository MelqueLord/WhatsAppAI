using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly string ConnectionString =
        "Server=127.0.0.1;Port=3306;Database=whatsappai_test;User=root;Password=root;CharSet=utf8mb4";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "MySQL",
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Encryption:Key"] = Convert.ToBase64String(new byte[32]),
                ["Meta:VerifyToken"] = "test-verify-token",
                ["Meta:AppSecret"] = "test-app-secret",
                ["BootstrapAdmin:Email"] = "admin@test.com",
                ["BootstrapAdmin:Password"] = "Admin@123"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                     d.ServiceType == typeof(AppDbContext)).ToList();

            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseMySQL(ConnectionString);
            });
        });
    }

    public async Task<AppDbContext> GetDbContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        if (!await context.SubscriptionPlans.AnyAsync())
        {
            await context.SubscriptionPlans.AddRangeAsync(
                WhatsAppAI.Domain.Identity.SubscriptionPlan.CreateBot(),
                WhatsAppAI.Domain.Identity.SubscriptionPlan.CreateAiBot());
            await context.SaveChangesAsync();
        }

        return context;
    }
}
