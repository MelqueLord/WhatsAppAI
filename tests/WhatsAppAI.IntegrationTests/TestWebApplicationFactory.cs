using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace WhatsAppAI.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DataProtectionCertificatePassword = "integration-test-dataprotection-password";
    private readonly string _dataProtectionCertificatePath = Path.Combine(
        Path.GetTempPath(),
        $"whatsappai-integration-dataprotection-{Guid.NewGuid():N}.pfx");
    private readonly string _dataProtectionKeysPath = Path.Combine(
        Path.GetTempPath(),
        $"whatsappai-integration-dataprotection-keys-{Guid.NewGuid():N}");
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("whatsappai_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public TestWebApplicationFactory()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=whatsappai-integration",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1));

        File.WriteAllBytes(
            _dataProtectionCertificatePath,
            certificate.Export(X509ContentType.Pfx, DataProtectionCertificatePassword));
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var testSettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
            ["Encryption:Key"] = Convert.ToBase64String(new byte[32]),
            ["Jwt:Secret"] = "integration-test-jwt-secret-at-least-32-bytes",
            ["Jwt:Issuer"] = "whatsappai-tests",
            ["Jwt:Audience"] = "whatsappai-tests",
            ["Meta:VerifyToken"] = "test-verify-token",
            ["Meta:AppSecret"] = "test-app-secret",
            ["WHATSAPP_WEB_WEBHOOK_SECRET"] = "integration-test-whatsapp-web-secret-at-least-32-bytes",
            ["DataProtection:KeysPath"] = _dataProtectionKeysPath,
            ["DataProtection:CertificatePath"] = _dataProtectionCertificatePath,
            ["DataProtection:CertificatePassword"] = DataProtectionCertificatePassword,
            // HTTP integration tests exercise request handlers and repositories.
            // Hosted workers are covered separately and would retry the webhook
            // fixtures' intentionally unconfigured phone number indefinitely.
            ["Workers:Enabled"] = "false",
            ["BootstrapAdmin:Email"] = "admin@test.com",
            ["BootstrapAdmin:Password"] = "Admin@12345!"
        };

        // UseSetting also flows to derived hosts created by WithWebHostBuilder,
        // including the production-mode CSRF test host.
        foreach (var (key, value) in testSettings)
            builder.UseSetting(key, value);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(testSettings);
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
                options.UseNpgsql(_postgres.GetConnectionString());
            });
        });
    }

    public async Task<AppDbContext> GetDbContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

        if (!await context.SubscriptionPlans.AnyAsync())
        {
            await context.SubscriptionPlans.AddRangeAsync(
                WhatsAppAI.Domain.Identity.SubscriptionPlan.CreateBot(),
                WhatsAppAI.Domain.Identity.SubscriptionPlan.CreateAiBot());
            await context.SaveChangesAsync();
        }

        return context;
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();

        if (File.Exists(_dataProtectionCertificatePath))
            File.Delete(_dataProtectionCertificatePath);

        if (Directory.Exists(_dataProtectionKeysPath))
            Directory.Delete(_dataProtectionKeysPath, recursive: true);
    }
}
