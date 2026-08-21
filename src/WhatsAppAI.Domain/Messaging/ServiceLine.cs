namespace WhatsAppAI.Domain.Messaging;

public sealed class ServiceLine
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Color { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ServiceLine() { }

    public static ServiceLine Create(Guid tenantId, string name, string? description = null, string? color = null, int sortOrder = 0)
    {
        return new ServiceLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Color = color?.Trim(),
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description, string? color, int sortOrder)
    {
        Name = name.Trim();
        Description = description?.Trim();
        Color = color?.Trim();
        SortOrder = sortOrder;
    }

    public void Deactivate() { IsActive = false; }
    public void Activate() { IsActive = true; }
}
