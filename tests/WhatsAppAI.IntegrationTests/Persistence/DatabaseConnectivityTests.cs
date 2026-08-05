using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Persistence;

public sealed class DatabaseConnectivityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.3-alpine")
        .WithDatabase("whatsapp_ai_tests")
        .WithUsername("whatsapp_ai")
        .WithPassword($"test-{Guid.NewGuid():N}")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task DbContext_connects_without_migrations_or_business_schema()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new AppDbContext(options);

        Assert.True(await context.Database.CanConnectAsync());
        Assert.Empty(context.Database.GetMigrations());

        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';";

        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }
}
