using WhatsAppAI.Domain.Usage;
using Xunit;

namespace WhatsAppAI.UnitTests.Usage;

public class UsageLedgerTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var ledger = UsageLedger.Create(tenantId, "OpenAI", "tokens", "msg123", 1500, "tokens", 50, "BRL", 2);

        Assert.Equal(tenantId, ledger.TenantId);
        Assert.Equal("OpenAI", ledger.Provider);
        Assert.Equal("tokens", ledger.Metric);
        Assert.Equal("msg123", ledger.SourceId);
        Assert.Equal(1500, ledger.Quantity);
        Assert.Equal("tokens", ledger.Unit);
        Assert.Equal(50, ledger.CostMinorUnits);
        Assert.Equal("BRL", ledger.Currency);
        Assert.Equal(2, ledger.PriceVersion);
    }

    [Fact]
    public void Create_AllowsNullCost()
    {
        var ledger = UsageLedger.Create(Guid.NewGuid(), "WhatsApp", "messages", "msg123", 1, "messages");

        Assert.Null(ledger.CostMinorUnits);
        Assert.Null(ledger.Currency);
    }

    [Fact]
    public void Create_SetsRecordedAt()
    {
        var before = DateTime.UtcNow;
        var ledger = UsageLedger.Create(Guid.NewGuid(), "OpenAI", "tokens", "msg123", 100, "tokens");

        Assert.True(ledger.RecordedAt >= before);
        Assert.True(ledger.RecordedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_SetsUnit()
    {
        var ledger = UsageLedger.Create(Guid.NewGuid(), "OpenAI", "tokens", "msg123", 100, "tokens");

        Assert.Equal("tokens", ledger.Unit);
    }
}
