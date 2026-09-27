using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.WebApi.Configuration;

namespace WhatsAppAI.IntegrationTests.Persistence;

public sealed class PostgreSqlMigrationConfigurationTests
{
    [Fact]
    public void Production_does_not_apply_migrations_on_application_startup_by_default()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.False(MigrationStartupPolicy.ShouldApply(
            new TestHostEnvironment { EnvironmentName = Environments.Production },
            configuration));
    }

    [Fact]
    public void Non_production_applies_migrations_on_application_startup_by_default()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.True(MigrationStartupPolicy.ShouldApply(
            new TestHostEnvironment { EnvironmentName = "Testing" },
            configuration));
    }

    [Fact]
    public void Explicit_configuration_overrides_environment_default()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:ApplyMigrationsOnStartup"] = "true"
            })
            .Build();

        Assert.True(MigrationStartupPolicy.ShouldApply(
            new TestHostEnvironment { EnvironmentName = Environments.Production },
            configuration));
    }

    [Fact]
    public void Production_requires_data_protection_certificate()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => services.AddIdentityServices(
            new TestHostEnvironment { EnvironmentName = Environments.Production },
            configuration));
    }

    [Fact]
    public void Production_accepts_data_protection_pfx_with_private_key()
    {
        var certificatePath = Path.Combine(
            Path.GetTempPath(),
            $"whatsappai-dataprotection-{Guid.NewGuid():N}.pfx");
        var keysPath = Path.Combine(
            Path.GetTempPath(),
            $"whatsappai-dataprotection-keys-{Guid.NewGuid():N}");
        const string certificatePassword = "Dataprotection-test!";

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=WhatsAppAI Data Protection Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1));
        File.WriteAllBytes(
            certificatePath,
            certificate.Export(X509ContentType.Pfx, certificatePassword));

        try
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:KeysPath"] = keysPath,
                    ["DataProtection:CertificatePath"] = certificatePath,
                    ["DataProtection:CertificatePassword"] = certificatePassword
                })
                .Build();

            services.AddIdentityServices(
                new TestHostEnvironment { EnvironmentName = Environments.Production },
                configuration);

            Assert.Contains(
                services,
                descriptor => descriptor.ServiceType.Equals(typeof(IDataProtectionProvider)));
        }
        finally
        {
            File.Delete(certificatePath);

            if (Directory.Exists(keysPath))
                Directory.Delete(keysPath, recursive: true);
        }
    }

    [Fact]
    public void Supabase_uses_dedicated_postgresql_migrations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentTenant, TestCurrentTenant>();
        services.AddPersistence(
            "Host=localhost;Database=not-used;Username=postgres;Password=not-used");

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var migrations = context.Database.GetMigrations().ToArray();

        Assert.Contains("20260827000000_PostgreSqlBaseline", migrations);
        Assert.Contains("20260927004154_AddWhatsAppTemplateManagement", migrations);
        Assert.Contains("20260927124054_AddWebhookRoutingForTemplates", migrations);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = nameof(PostgreSqlMigrationConfigurationTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
