using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Persistence;

public sealed class DatabaseConnectivityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DatabaseConnectivityTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task DbContext_connects_to_database()
    {
        var db = await _factory.GetDbContextAsync();
        Assert.True(await db.Database.CanConnectAsync());
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
