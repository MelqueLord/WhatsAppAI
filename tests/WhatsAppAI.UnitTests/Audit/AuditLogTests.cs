using WhatsAppAI.Domain.Audit;
using Xunit;

namespace WhatsAppAI.UnitTests.Audit;

public class AuditLogTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var log = AuditLog.Create(tenantId, userId, "CREATE", "Tenant", "entity123", "Details", "192.168.1.1");

        Assert.Equal(tenantId, log.TenantId);
        Assert.Equal(userId, log.UserId);
        Assert.Equal("CREATE", log.Action);
        Assert.Equal("Tenant", log.EntityType);
        Assert.Equal("entity123", log.EntityId);
        Assert.Equal("Details", log.Details);
        Assert.Equal("192.168.1.1", log.IpAddress);
    }

    [Fact]
    public void Create_AllowsNullOptionalFields()
    {
        var log = AuditLog.Create(Guid.NewGuid(), null, "DELETE", "Message");

        Assert.Null(log.UserId);
        Assert.Null(log.EntityId);
        Assert.Null(log.Details);
        Assert.Null(log.IpAddress);
    }

    [Fact]
    public void Create_SetsOccurredAt()
    {
        var before = DateTime.UtcNow;
        var log = AuditLog.Create(Guid.NewGuid(), Guid.NewGuid(), "UPDATE", "Conversation");

        Assert.True(log.OccurredAt >= before);
        Assert.True(log.OccurredAt <= DateTime.UtcNow);
    }
}
