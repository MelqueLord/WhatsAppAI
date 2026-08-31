namespace WhatsAppAI.Domain.Knowledge;

public static class KnowledgeCategories
{
    public const string General = "General";
    public const string Faq = "Faq";
    public const string Service = "Service";
    public const string Pricing = "Pricing";
    public const string BusinessHours = "BusinessHours";
    public const string Payment = "Payment";
    public const string Location = "Location";
    public const string Policy = "Policy";

    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        General,
        Faq,
        Service,
        Pricing,
        BusinessHours,
        Payment,
        Location,
        Policy
    };

    public static bool IsSupported(string? category) =>
        string.IsNullOrWhiteSpace(category) || Supported.Contains(category.Trim());

    public static string Normalize(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return General;

        var normalized = category.Trim();
        var supported = Supported.FirstOrDefault(item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (supported is null)
            throw new ArgumentException("Unsupported knowledge category.", nameof(category));

        return supported;
    }
}
