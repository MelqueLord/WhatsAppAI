namespace WhatsAppAI.Domain.Identity;

public sealed class SubscriptionPlan
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool AiEnabled { get; private set; }
    public bool OpenAiRequired { get; private set; }
    public bool AiMetrics { get; private set; }
    public int? MaxOperators { get; private set; }
    public int? MaxKnowledgeItems { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private SubscriptionPlan() { }

    public static SubscriptionPlan Create(string name, string code, string? description, bool aiEnabled)
    {
        return new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            Description = description?.Trim(),
            AiEnabled = aiEnabled,
            OpenAiRequired = aiEnabled,
            AiMetrics = aiEnabled,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static SubscriptionPlan CreateBot()
    {
        return Create("BOT", "BOT", "Todos os recursos exceto IA para atendimento", false);
    }

    public static SubscriptionPlan CreateAiBot()
    {
        return Create("IA + BOT", "IA_BOT", "Completo com IA para atendimento automatizado", true);
    }

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
