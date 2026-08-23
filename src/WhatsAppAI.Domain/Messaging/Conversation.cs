namespace WhatsAppAI.Domain.Messaging;

public sealed class Conversation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ContactId { get; private set; }
    public string PhoneNumberId { get; private set; } = string.Empty;
    public ConversationMode Mode { get; private set; }
    public ConversationStatus Status { get; private set; }
    public string? AssignedToUserId { get; private set; }
    public Guid? QueueId { get; private set; }
    public uint Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? LastMessageAt { get; private set; }
    public DateTime? WindowExpiresAt { get; private set; }

    public Contact Contact { get; private set; } = null!;

    private readonly List<Message> _messages = [];
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation() { }

    public static Conversation Create(
        Guid tenantId,
        Guid contactId,
        string phoneNumberId,
        ConversationMode mode = ConversationMode.Automatic)
    {
        return new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contactId,
            PhoneNumberId = phoneNumberId,
            Mode = mode,
            Status = ConversationStatus.Open,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };
    }

    public ConversationMode SwitchMode(ConversationMode newMode, uint expectedVersion, string? assignedToUserId = null)
    {
        if (Version != expectedVersion)
            throw new ConcurrencyException($"Version conflict: expected {expectedVersion}, actual {Version}.");

        var previous = Mode;
        Mode = newMode;
        AssignedToUserId = newMode == ConversationMode.Human ? assignedToUserId : null;
        Version++;
        UpdatedAt = DateTime.UtcNow;
        return previous;
    }

    public void RenewWindow()
    {
        WindowExpiresAt = DateTime.UtcNow.AddHours(24);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPhoneNumberId(string phoneNumberId)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberId))
            throw new ArgumentException("Phone number ID is required.", nameof(phoneNumberId));

        PhoneNumberId = phoneNumberId;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsWindowOpen(DateTime utcNow) => WindowExpiresAt.HasValue && WindowExpiresAt.Value > utcNow;

    public void RecordMessage()
    {
        LastMessageAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Close()
    {
        Status = ConversationStatus.Closed;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Reopen()
    {
        Status = ConversationStatus.Open;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void AssignQueue(Guid? queueId)
    {
        QueueId = queueId;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum ConversationMode
{
    Automatic = 0,
    Human = 1,
    Paused = 2
}

public enum ConversationStatus
{
    Open = 0,
    Closed = 1
}
