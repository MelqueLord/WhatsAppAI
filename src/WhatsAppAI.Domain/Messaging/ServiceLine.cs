using System.Globalization;
using System.Text;

namespace WhatsAppAI.Domain.Messaging;

public sealed class ServiceLine
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Color { get; private set; }
    public string? Keywords { get; private set; }
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

    public void SetKeywords(string? keywords)
    {
        Keywords = string.IsNullOrWhiteSpace(keywords) ? null : keywords.Trim();
    }

    public bool MatchesKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(Keywords) || string.IsNullOrWhiteSpace(text))
            return false;

        var normalizedText = $" {NormalizeForKeywordMatch(text)} ";
        var keywords = Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Array.Exists(keywords, keyword =>
        {
            var normalizedKeyword = NormalizeForKeywordMatch(keyword);
            return !string.IsNullOrWhiteSpace(normalizedKeyword) &&
                normalizedText.Contains($" {normalizedKeyword}", StringComparison.Ordinal);
        });
    }

    private static string NormalizeForKeywordMatch(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        var previousWasSpace = true;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToLowerInvariant(character));
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                normalized.Append(' ');
                previousWasSpace = true;
            }
        }

        return normalized.ToString().Trim();
    }

    public void Deactivate() { IsActive = false; }
    public void Activate() { IsActive = true; }
}
