namespace WhatsAppAI.Domain.Messaging;

public sealed class Contact
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? Name { get; private set; }
    public string? ProfilePictureUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? LastMessageAt { get; private set; }

    private readonly List<Conversation> _conversations = [];
    public IReadOnlyCollection<Conversation> Conversations => _conversations.AsReadOnly();

    private Contact() { }

    public static Contact Create(Guid tenantId, string phoneNumber, string? name = null)
    {
        return new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PhoneNumber = phoneNumber,
            Name = NormalizeName(name),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateName(string? name)
    {
        var normalizedName = NormalizeName(name);
        if (normalizedName is not null && normalizedName != Name)
        {
            Name = normalizedName;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void UpdateNameFromWhatsApp(string? name)
    {
        // A missing profile name must never erase a name already known by the
        // tenant. A later valid WhatsApp profile name may refresh an import.
        UpdateName(name);
    }

    public void UpdatePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        PhoneNumber = phoneNumber;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfilePicture(string? url)
    {
        ProfilePictureUrl = url;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordMessage()
    {
        LastMessageAt = DateTime.UtcNow;
    }

    public void Anonymize()
    {
        PhoneNumber = $"anon-{Id:N}"[..20];
        Name = null;
        ProfilePictureUrl = null;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var withoutControlCharacters = new string(name
            .Where(character => !char.IsControl(character))
            .ToArray());
        var normalized = string.Join(' ', withoutControlCharacters
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length switch
        {
            0 => null,
            <= 200 => normalized,
            _ => normalized[..200].TrimEnd()
        };
    }
}
