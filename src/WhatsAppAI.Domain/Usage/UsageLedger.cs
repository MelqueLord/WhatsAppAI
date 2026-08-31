namespace WhatsAppAI.Domain.Usage;

public sealed class UsageLedger
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Metric { get; private set; } = string.Empty;
    public string SourceId { get; private set; } = string.Empty;
    public long Quantity { get; private set; }
    public string? Unit { get; private set; }
    public long? CostMinorUnits { get; private set; }
    public string? Currency { get; private set; }
    public int? PriceVersion { get; private set; }
    public AiResponseQuotaPackageType? AiResponseQuotaPackageType { get; private set; }
    public string? AiResponseQuotaPackageReference { get; private set; }
    public DateTime RecordedAt { get; private set; }

    private UsageLedger() { }

    public static UsageLedger Create(
        Guid tenantId,
        string provider,
        string metric,
        string sourceId,
        long quantity,
        string? unit,
        long? costMinorUnits = null,
        string? currency = null,
        int? priceVersion = null,
        AiResponseQuotaPackageType? aiResponseQuotaPackageType = null,
        string? aiResponseQuotaPackageReference = null)
    {
        return new UsageLedger
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = provider,
            Metric = metric,
            SourceId = sourceId,
            Quantity = quantity,
            Unit = unit,
            CostMinorUnits = costMinorUnits,
            Currency = currency,
            PriceVersion = priceVersion,
            AiResponseQuotaPackageType = aiResponseQuotaPackageType,
            AiResponseQuotaPackageReference = aiResponseQuotaPackageReference?.Trim(),
            RecordedAt = DateTime.UtcNow
        };
    }
}
