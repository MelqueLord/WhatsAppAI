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
            Name = name,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateName(string? name)
    {
        if (name is not null && name != Name)
        {
            Name = name;
            UpdatedAt = DateTime.UtcNow;
        }
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
        PhoneNumber = $"anon-{Id:N}";
        Name = null;
        ProfilePictureUrl = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
