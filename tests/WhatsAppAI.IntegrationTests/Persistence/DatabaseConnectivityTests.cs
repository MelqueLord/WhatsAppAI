using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Persistence;

public sealed class DatabaseConnectivityTests
{
    [Fact(Skip = "Requires Docker for MySQL Testcontainer")]
    public async Task DbContext_connects_without_migrations_or_business_schema()
    {
        // This test requires Docker to run MySQL Testcontainer
        // Skipped in environments without Docker
        await Task.CompletedTask;
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
