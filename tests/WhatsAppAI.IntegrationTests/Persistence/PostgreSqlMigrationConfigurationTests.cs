using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Persistence;

public sealed class PostgreSqlMigrationConfigurationTests
{
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

        Assert.Equal(
            [
                "20260827000000_PostgreSqlBaseline",
                "20260827120000_RemoveBotTokenLimit",
                "20260827133343_AddPrivacyControls",
                "20260827160000_AddAiMessageRetry",
                "20260827210000_AddBotConfidenceThreshold",
                "20260828010000_AddOperatorQueueAssignment"
            ],
            context.Database.GetMigrations());
    }
}
