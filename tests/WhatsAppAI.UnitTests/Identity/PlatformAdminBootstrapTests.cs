using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.UnitTests.Identity;

public sealed class PlatformAdminBootstrapTests
{
    [Fact]
    public async Task EnsureAsync_CreatesOneTemporaryPlatformAdminAndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options, new EmptyTenantContext());
        await context.Database.EnsureCreatedAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Email"] = "admin@example.com",
                ["BootstrapAdmin:Password"] = "Str0ng-bootstrap!"
            })
            .Build();

        static string HashPassword(string password) => $"hashed:{password}";

        await PlatformAdminBootstrap.EnsureAsync(context, configuration, HashPassword);
        await PlatformAdminBootstrap.EnsureAsync(context, configuration, HashPassword);

        var admins = await context.Users.Where(u => u.IsPlatformAdmin).ToListAsync();
        var admin = Assert.Single(admins);
        Assert.True(admin.IsActive);
        Assert.True(admin.MustChangePassword);
        Assert.Equal("hashed:Str0ng-bootstrap!", admin.PasswordHash);
    }

    [Fact]
    public void ValidateCredentials_AcceptsStrongCredentials()
    {
        var exception = Record.Exception(() =>
            PlatformAdminBootstrap.ValidateCredentials(
                "admin@example.com",
                "Str0ng-bootstrap!"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null, "Str0ng-bootstrap!")]
    [InlineData("not-an-email", "Str0ng-bootstrap!")]
    [InlineData("admin@example.com", "Admin@123")]
    [InlineData("admin@example.com", "alllowercase123!")]
    public void ValidateCredentials_RejectsMissingOrWeakCredentials(string? email, string password)
    {
        Assert.Throws<InvalidOperationException>(() =>
            PlatformAdminBootstrap.ValidateCredentials(email, password));
    }

    private sealed class EmptyTenantContext : ICurrentTenant
    {
        public Guid? TenantId => null;
        public Guid? UserId => null;
        public string? UserRole => null;
        public bool IsPlatformAdmin => false;
        public bool IsAuthenticated => false;
        public SupportSessionInfo? SupportSession => null;

        public void SetContext(Guid? tenantId, Guid userId, string role, bool isPlatformAdmin) { }
        public void EnterSupportSession(Guid tenantId, string reason) { }
        public void ExitSupportSession() { }
        public void Clear() { }
    }
}
