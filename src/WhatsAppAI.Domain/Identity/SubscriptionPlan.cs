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
    public bool BotEnabled { get; private set; }
    public bool TagsEnabled { get; private set; }
    public bool AutomaticDistributionEnabled { get; private set; }
    public bool IsSelectable { get; private set; }
    public int DefaultLineCount { get; private set; }
    public int DefaultOperatorLimit { get; private set; }
    public int? DefaultMonthlyAiResponseLimit { get; private set; }
    public int? MaxOperators { get; private set; }
    public int? MaxKnowledgeItems { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private SubscriptionPlan() { }

    public static SubscriptionPlan Create(string name, string code, string? description, bool aiEnabled)
    {
        return Create(
            name, code, description, aiEnabled,
            botEnabled: true, tagsEnabled: true, automaticDistributionEnabled: true,
            isSelectable: false, defaultLineCount: 0,
            defaultOperatorLimit: 0, defaultMonthlyAiResponseLimit: null);
    }

    private static SubscriptionPlan Create(
        string name,
        string code,
        string? description,
        bool aiEnabled,
        bool botEnabled,
        bool tagsEnabled,
        bool automaticDistributionEnabled,
        bool isSelectable,
        int defaultLineCount,
        int defaultOperatorLimit,
        int? defaultMonthlyAiResponseLimit)
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
            BotEnabled = botEnabled,
            TagsEnabled = tagsEnabled,
            AutomaticDistributionEnabled = automaticDistributionEnabled,
            IsSelectable = isSelectable,
            DefaultLineCount = defaultLineCount,
            DefaultOperatorLimit = defaultOperatorLimit,
            DefaultMonthlyAiResponseLimit = defaultMonthlyAiResponseLimit,
            MaxOperators = defaultOperatorLimit > 0 ? defaultOperatorLimit : null,
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

    public static SubscriptionPlan CreateStar()
    {
        return Create(
            "STAR", "STAR", "O essencial para começar com profissionalismo", true,
            botEnabled: false, tagsEnabled: false, automaticDistributionEnabled: false,
            isSelectable: true, defaultLineCount: 1,
            defaultOperatorLimit: 2, defaultMonthlyAiResponseLimit: 1_500);
    }

    public static SubscriptionPlan CreateFlow()
    {
        return Create(
            "FLOW", "FLOW", "Para ganhar agilidade no atendimento", true,
            botEnabled: true, tagsEnabled: true, automaticDistributionEnabled: true,
            isSelectable: true, defaultLineCount: 2,
            defaultOperatorLimit: 4, defaultMonthlyAiResponseLimit: 5_000);
    }

    public static SubscriptionPlan CreateScala()
    {
        return Create(
            "SCALA", "SCALA", "Leve sua operação para o próximo nível", true,
            botEnabled: true, tagsEnabled: true, automaticDistributionEnabled: true,
            isSelectable: true, defaultLineCount: 3,
            defaultOperatorLimit: 8, defaultMonthlyAiResponseLimit: 12_000);
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
