using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.Infrastructure.Persistence.Repositories;

namespace WhatsAppAI.UnitTests.Persistence;

public sealed class RepositoryLookupTests
{
    [Fact]
    public async Task ContactLookup_FiltersByTenantAndPhoneInDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext(connection, tenantId);
        await db.Database.EnsureCreatedAsync();

        var expected = Contact.Create(tenantId, "5511999990000", "Ana");
        db.Contacts.AddRange(expected, Contact.Create(otherTenantId, "5511999990000", "Outro"));
        await db.SaveChangesAsync();

        var result = await new ContactRepository(db).GetByPhoneAsync(tenantId, "+55 (11) 99999-0000");

        Assert.NotNull(result);
        Assert.Equal(expected.Id, result.Id);
    }

    [Fact]
    public async Task ConversationLookup_FiltersByTenantContactAndLine()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext(connection, tenantId);
        await db.Database.EnsureCreatedAsync();

        var contact = Contact.Create(tenantId, "5511999990000");
        var otherContact = Contact.Create(otherTenantId, "5511999990001");
        var expected = Conversation.Create(tenantId, contact.Id, "line-1");
        db.Contacts.AddRange(contact, otherContact);
        db.Conversations.AddRange(expected, Conversation.Create(otherTenantId, otherContact.Id, "line-1"));
        await db.SaveChangesAsync();

        var result = await new ConversationRepository(db)
            .GetByContactAndPhoneAsync(tenantId, contact.Id, "line-1");

        Assert.NotNull(result);
        Assert.Equal(expected.Id, result.Id);
        Assert.NotNull(result.Contact);
    }

    [Fact]
    public async Task WhatsAppAccountLookup_FiltersByTenantSlotAndActiveState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext(connection, tenantId);
        await db.Database.EnsureCreatedAsync();

        var expected = WhatsAppAccount.Create(tenantId, "waba", "phone-1", "secret");
        var inactive = WhatsAppAccount.Create(tenantId, "waba", "phone-2", "secret", lineNumber: 2);
        inactive.Deactivate();
        db.WhatsAppAccounts.AddRange(expected, inactive,
            WhatsAppAccount.Create(otherTenantId, "waba", "phone-3", "secret"));
        await db.SaveChangesAsync();

        var repository = new WhatsAppAccountRepository(db);
        var result = await repository.GetByTenantAndSlotAsync(
            tenantId, WhatsAppConnectionType.OfficialApi, 1);
        var inactiveResult = await repository.GetByTenantAndSlotAsync(
            tenantId, WhatsAppConnectionType.OfficialApi, 2);

        Assert.NotNull(result);
        Assert.Equal(expected.Id, result.Id);
        Assert.Null(inactiveResult);
    }

    private static AppDbContext CreateContext(SqliteConnection connection, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options, new TestCurrentTenant(tenantId));
    }

    private sealed class TestCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => null;
        public string? UserRole => "TenantOwner";
        public bool IsPlatformAdmin => false;
        public bool IsAuthenticated => true;
        public SupportSessionInfo? SupportSession => null;
        public void SetContext(Guid? tenantId, Guid userId, string role, bool isPlatformAdmin) { }
        public void EnterSupportSession(Guid tenantId, string reason) { }
        public void ExitSupportSession() { }
        public void Clear() { }
    }
}
