namespace WhatsAppAI.Domain.Messaging;

public sealed class HandoffEvent
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public ConversationMode FromMode { get; private set; }
    public ConversationMode ToMode { get; private set; }
    public Guid? OperatorUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    private HandoffEvent() { }

    public static HandoffEvent Create(
        Guid tenantId,
        Guid conversationId,
        ConversationMode fromMode,
        ConversationMode toMode,
        Guid? operatorUserId,
        string reason)
    {
        return new HandoffEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            FromMode = fromMode,
            ToMode = toMode,
            OperatorUserId = operatorUserId,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        };
    }
}
