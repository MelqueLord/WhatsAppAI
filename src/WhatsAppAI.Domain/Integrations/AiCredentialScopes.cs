namespace WhatsAppAI.Domain.Integrations;

public static class AiCredentialScopes
{
    public const string TenantProject = "TenantProject";
    public const string SharedPlatform = "SharedPlatform";

    public static bool IsSupported(string? scope) =>
        string.Equals(scope, TenantProject, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(scope, SharedPlatform, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? scope) =>
        string.Equals(scope, SharedPlatform, StringComparison.OrdinalIgnoreCase)
            ? SharedPlatform
            : TenantProject;
}
