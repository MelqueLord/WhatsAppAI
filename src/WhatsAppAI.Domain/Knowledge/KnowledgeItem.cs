namespace WhatsAppAI.Domain.Knowledge;

public sealed class KnowledgeItem
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }
    public uint Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeactivatedAt { get; private set; }
    public DateTime? ReactivatedAt { get; private set; }

    private KnowledgeItem() { }

    public static KnowledgeItem Create(
        Guid tenantId,
        string title,
        string content,
        int priority = 0)
    {
        return new KnowledgeItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            Content = content.Trim(),
            Priority = priority,
            IsActive = true,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, string content, int priority, uint expectedVersion)
    {
        if (Version != expectedVersion)
            throw new ConcurrencyException($"Version conflict: expected {expectedVersion}, actual {Version}.");

        Title = title.Trim();
        Content = content.Trim();
        Priority = priority;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate(uint expectedVersion)
    {
        if (Version != expectedVersion)
            throw new ConcurrencyException($"Version conflict: expected {expectedVersion}, actual {Version}.");

        if (!IsActive)
            throw new InvalidOperationException("Already deactivated.");

        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Reactivate(uint expectedVersion)
    {
        if (Version != expectedVersion)
            throw new ConcurrencyException($"Version conflict: expected {expectedVersion}, actual {Version}.");

        if (IsActive)
            throw new InvalidOperationException("Already active.");

        IsActive = true;
        ReactivatedAt = DateTime.UtcNow;
        Version++;
    }
}
