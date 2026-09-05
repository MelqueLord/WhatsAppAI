namespace WhatsAppAI.Domain.Automation;

public sealed class AiResponseExample
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string CustomerMessage { get; private set; } = string.Empty;
    public string IdealResponse { get; private set; } = string.Empty;
    public AiResponseExampleSource Source { get; private set; }
    public Guid? SourceInteractionId { get; private set; }
    public bool IsActive { get; private set; }
    public uint Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private AiResponseExample() { }

    public static AiResponseExample Create(Guid tenantId, string customerMessage, string idealResponse)
    {
        return CreateInternal(tenantId, customerMessage, idealResponse, AiResponseExampleSource.Manual, null);
    }

    public static AiResponseExample CreateFromOperatorFeedback(
        Guid tenantId,
        Guid interactionId,
        string customerMessage,
        string idealResponse)
    {
        if (interactionId == Guid.Empty)
            throw new ArgumentException("The source interaction is required.", nameof(interactionId));

        return CreateInternal(
            tenantId,
            customerMessage,
            idealResponse,
            AiResponseExampleSource.OperatorFeedback,
            interactionId);
    }

    public void Update(string customerMessage, string idealResponse, uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        Validate(customerMessage, idealResponse);
        CustomerMessage = customerMessage.Trim();
        IdealResponse = idealResponse.Trim();
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Deactivate(uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (!IsActive)
            throw new InvalidOperationException("Example is already inactive.");
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Reactivate(uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (IsActive)
            throw new InvalidOperationException("Example is already active.");
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    private void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
            throw new ConcurrencyException($"Version conflict: expected {expectedVersion}, actual {Version}.");
    }

    private static AiResponseExample CreateInternal(
        Guid tenantId,
        string customerMessage,
        string idealResponse,
        AiResponseExampleSource source,
        Guid? sourceInteractionId)
    {
        Validate(customerMessage, idealResponse);
        return new AiResponseExample
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerMessage = customerMessage.Trim(),
            IdealResponse = idealResponse.Trim(),
            Source = source,
            SourceInteractionId = sourceInteractionId,
            IsActive = true,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static void Validate(string customerMessage, string idealResponse)
    {
        if (string.IsNullOrWhiteSpace(customerMessage))
            throw new ArgumentException("Customer message is required.", nameof(customerMessage));
        if (string.IsNullOrWhiteSpace(idealResponse))
            throw new ArgumentException("Ideal response is required.", nameof(idealResponse));
        if (customerMessage.Trim().Length > 500)
            throw new ArgumentException("Customer message cannot exceed 500 characters.", nameof(customerMessage));
        if (idealResponse.Trim().Length > 500)
            throw new ArgumentException("Ideal response cannot exceed 500 characters.", nameof(idealResponse));
    }
}

public enum AiResponseExampleSource
{
    Manual = 0,
    OperatorFeedback = 1
}
