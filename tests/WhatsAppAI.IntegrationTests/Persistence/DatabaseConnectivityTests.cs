using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Persistence;

public sealed class DatabaseConnectivityTests : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder("mysql:8.4-lts")
        .WithDatabase("whatsapp_ai_tests")
        .WithUsername("testuser")
        .WithPassword($"test-{Guid.NewGuid():N}")
        .Build();

    public Task InitializeAsync() => _mysql.StartAsync();

    public Task DisposeAsync() => _mysql.DisposeAsync().AsTask();

    [Fact]
    public async Task DbContext_connects_without_migrations_or_business_schema()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(_mysql.GetConnectionString())
            .Options;

        await using var context = new AppDbContext(options, new TestCurrentTenant());

        Assert.True(await context.Database.CanConnectAsync());

        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE();";

        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }
}

internal sealed class TestCurrentTenant : WhatsAppAI.Application.Abstractions.ICurrentTenant
{
    public Guid? TenantId => null;
    public Guid? UserId => null;
    public string? UserRole => null;
    public bool IsPlatformAdmin => false;
    public bool IsAuthenticated => false;
    public WhatsAppAI.Application.Abstractions.SupportSessionInfo? SupportSession => null;
    public void SetContext(Guid? tenantId, Guid userId, string role, bool isPlatformAdmin) { }
    public void EnterSupportSession(Guid tenantId, string reason) { }
    public void ExitSupportSession() { }
    public void Clear() { }
}
