using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Contacts;

public sealed class ContactImportService(
    IContactImportFileReader fileReader,
    IContactRepository contactRepository)
{
    public const int MaxRows = 5_000;

    public async Task<ContactImportResult> ImportAsync(
        Guid tenantId,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var rows = await fileReader.ReadAsync(stream, fileName, MaxRows, cancellationToken);
        var existingContacts = await contactRepository.GetByTenantAsync(tenantId, cancellationToken);
        var knownPhones = existingContacts
            .Select(contact => NormalizePhone(contact.PhoneNumber))
            .Where(phone => phone is not null)
            .Select(phone => phone!)
            .ToHashSet(StringComparer.Ordinal);

        var importedPhones = new HashSet<string>(StringComparer.Ordinal);
        var contacts = new List<Contact>();
        var errors = new List<ContactImportError>();
        var skipped = 0;

        foreach (var row in rows)
        {
            var name = row.Name?.Trim();
            var phone = NormalizePhone(row.Contact);

            if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            {
                errors.Add(new ContactImportError(
                    row.RowNumber,
                    "invalid_name",
                    "Nome é obrigatório e deve ter no máximo 200 caracteres."));
                continue;
            }

            if (phone is null)
            {
                errors.Add(new ContactImportError(
                    row.RowNumber,
                    "invalid_contact",
                    "Contato deve conter de 8 a 15 dígitos."));
                continue;
            }

            if (knownPhones.Contains(phone) || !importedPhones.Add(phone))
            {
                skipped++;
                continue;
            }

            contacts.Add(Contact.Create(tenantId, phone, name));
        }

        if (contacts.Count > 0)
            await contactRepository.AddRangeAsync(contacts, cancellationToken);

        return new ContactImportResult(
            rows.Count,
            contacts.Count,
            skipped,
            errors.Count,
            errors);
    }

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(character => character is >= '0' and <= '9').ToArray());
        return digits.Length is >= 8 and <= 15 ? digits : null;
    }
}
