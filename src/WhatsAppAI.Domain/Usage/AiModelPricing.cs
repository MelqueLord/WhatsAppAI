namespace WhatsAppAI.Domain.Usage;

/// <summary>
/// Versioned platform price for a provider/model pair.
/// Values are stored in the minor currency unit per 1,000 tokens.
/// </summary>
public sealed class AiModelPricing
{
    public Guid Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ModelId { get; private set; } = string.Empty;
    public decimal InputCostPer1KMinorUnits { get; private set; }
    public decimal OutputCostPer1KMinorUnits { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AiModelPricing() { }

    public static AiModelPricing Create(
        string provider,
        string modelId,
        decimal inputCostPer1KMinorUnits,
        decimal outputCostPer1KMinorUnits,
        string currency,
        int version,
        DateTime effectiveFrom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentOutOfRangeException.ThrowIfNegative(inputCostPer1KMinorUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(outputCostPer1KMinorUnits);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        if (currency.Length != 3)
            throw new ArgumentException("Currency must be an ISO 4217 code.", nameof(currency));

        return new AiModelPricing
        {
            Id = Guid.NewGuid(),
            Provider = provider.Trim().ToLowerInvariant(),
            ModelId = modelId.Trim(),
            InputCostPer1KMinorUnits = inputCostPer1KMinorUnits,
            OutputCostPer1KMinorUnits = outputCostPer1KMinorUnits,
            Currency = currency.Trim().ToUpperInvariant(),
            Version = version,
            EffectiveFrom = effectiveFrom.Kind == DateTimeKind.Utc
                ? effectiveFrom
                : effectiveFrom.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void CloseAt(DateTime effectiveTo)
    {
        var utc = effectiveTo.Kind == DateTimeKind.Utc ? effectiveTo : effectiveTo.ToUniversalTime();
        if (utc <= EffectiveFrom)
            throw new ArgumentException("Price end must be after its start.", nameof(effectiveTo));

        EffectiveTo = utc;
    }

    public void Reopen()
    {
        EffectiveTo = null;
    }

    public long CalculateCostMinorUnits(long tokens, bool input)
    {
        if (tokens <= 0)
            return 0;

        var price = input ? InputCostPer1KMinorUnits : OutputCostPer1KMinorUnits;
        return decimal.ToInt64(decimal.Ceiling(tokens / 1000m * price));
    }
}
