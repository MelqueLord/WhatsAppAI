namespace WhatsAppAI.Application.Automation.Policy;

public static class TagCategorizationPolicy
{
    public static IReadOnlyList<Guid> ResolveAuthorizedTagIds(
        IReadOnlyList<string> requestedTagNames,
        IReadOnlyList<RoutingTagCandidate> authorizedTags)
    {
        var requested = requestedTagNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return authorizedTags
            .Where(tag => requested.Contains(tag.Name))
            .Select(tag => tag.Id)
            .Distinct()
            .ToList();
    }
}

public sealed record RoutingTagCandidate(Guid Id, string Name);
