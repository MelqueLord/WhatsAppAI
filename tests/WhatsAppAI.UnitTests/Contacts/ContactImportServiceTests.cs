using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Contacts;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.UnitTests.Contacts;

public sealed class ContactImportServiceTests
{
    [Fact]
    public async Task ImportAsync_ImportsValidRowsForCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeContactRepository();
        var service = new ContactImportService(
            new StubFileReader([
                new ContactImportRow(2, " Ana ", "+55 (11) 99999-0000"),
                new ContactImportRow(3, "Bruno", "5511888880000")
            ]),
            repository);

        var result = await service.ImportAsync(tenantId, Stream.Null, "contatos.csv");

        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Invalid);
        Assert.All(repository.Added, contact => Assert.Equal(tenantId, contact.TenantId));
        Assert.Contains(repository.Added, contact => contact.PhoneNumber == "5511999990000" && contact.Name == "Ana");
    }

    [Fact]
    public async Task ImportAsync_ReportsInvalidRowsWithoutExposingContactAndContinues()
    {
        var repository = new FakeContactRepository();
        var service = new ContactImportService(
            new StubFileReader([
                new ContactImportRow(2, "", "5511999990000"),
                new ContactImportRow(3, "Bruno", "123"),
                new ContactImportRow(4, "Carla", "5511777770000")
            ]),
            repository);

        var result = await service.ImportAsync(Guid.NewGuid(), Stream.Null, "contatos.csv");

        Assert.Equal(1, result.Imported);
        Assert.Equal(2, result.Invalid);
        Assert.Equal([2, 3], result.Errors.Select(error => error.Row));
        Assert.DoesNotContain(result.Errors, error => error.Message.Contains("5511", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_SkipsExistingAndRepeatedPhonesWithoutOverwriting()
    {
        var tenantId = Guid.NewGuid();
        var existing = Contact.Create(tenantId, "5511999990000", "Nome existente");
        var repository = new FakeContactRepository(existing);
        var service = new ContactImportService(
            new StubFileReader([
                new ContactImportRow(2, "Novo nome", "55 11 99999-0000"),
                new ContactImportRow(3, "Carla", "5511777770000"),
                new ContactImportRow(4, "Carla repetida", "55 11 77777-0000")
            ]),
            repository);

        var result = await service.ImportAsync(tenantId, Stream.Null, "contatos.csv");

        Assert.Equal(1, result.Imported);
        Assert.Equal(2, result.Skipped);
        Assert.Equal("Nome existente", existing.Name);
    }

    [Fact]
    public async Task ImportAsync_DoesNotTreatAnotherTenantContactAsDuplicate()
    {
        var currentTenant = Guid.NewGuid();
        var repository = new FakeContactRepository(
            Contact.Create(Guid.NewGuid(), "5511999990000", "Outro tenant"));
        var service = new ContactImportService(
            new StubFileReader([new ContactImportRow(2, "Ana", "5511999990000")]),
            repository);

        var result = await service.ImportAsync(currentTenant, Stream.Null, "contatos.csv");

        Assert.Equal(1, result.Imported);
        Assert.Equal(currentTenant, Assert.Single(repository.Added).TenantId);
    }

    private sealed class StubFileReader(IReadOnlyList<ContactImportRow> rows) : IContactImportFileReader
    {
        public Task<IReadOnlyList<ContactImportRow>> ReadAsync(
            Stream stream,
            string fileName,
            int maxRows,
            CancellationToken cancellationToken = default) => Task.FromResult(rows);
    }

    private sealed class FakeContactRepository(params Contact[] existing) : IContactRepository
    {
        private readonly List<Contact> _contacts = [.. existing];
        public List<Contact> Added { get; } = [];

        public Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_contacts.Find(contact => contact.Id == id));

        public Task<Contact?> GetByPhoneAsync(Guid tenantId, string phoneNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(_contacts.Find(contact => contact.TenantId == tenantId && contact.PhoneNumber == phoneNumber));

        public Task<IReadOnlyList<Contact>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Contact>>(_contacts.Where(contact => contact.TenantId == tenantId).ToList());

        public Task AddAsync(Contact contact, CancellationToken cancellationToken = default)
        {
            Added.Add(contact);
            _contacts.Add(contact);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Contact> contacts, CancellationToken cancellationToken = default)
        {
            var additions = contacts.ToList();
            Added.AddRange(additions);
            _contacts.AddRange(additions);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Contact contact, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
