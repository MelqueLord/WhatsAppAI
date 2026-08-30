using Npgsql;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.UnitTests.Persistence;

public sealed class PersistenceServiceCollectionExtensionsTests
{
    [Fact]
    public void LimitConnectionPool_EnablesPoolingWithoutArtificialTenConnectionCap()
    {
        var connectionString = PersistenceServiceCollectionExtensions.LimitConnectionPool(
            "Host=localhost;Database=whatsappai;Username=user;Password=password");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.True(builder.Pooling);
        Assert.Equal(100, builder.MaxPoolSize);
    }

    [Fact]
    public void LimitConnectionPool_PreservesLowerConfiguredLimit()
    {
        var connectionString = PersistenceServiceCollectionExtensions.LimitConnectionPool(
            "Host=localhost;Database=whatsappai;Username=user;Password=password;Maximum Pool Size=5");

        Assert.Equal(5, new NpgsqlConnectionStringBuilder(connectionString).MaxPoolSize);
    }

    [Fact]
    public void LimitConnectionPool_AppliesConfiguredPoolSize()
    {
        var connectionString = PersistenceServiceCollectionExtensions.LimitConnectionPool(
            "Host=localhost;Database=whatsappai;Username=user;Password=password",
            50);

        Assert.Equal(50, new NpgsqlConnectionStringBuilder(connectionString).MaxPoolSize);
    }
}
