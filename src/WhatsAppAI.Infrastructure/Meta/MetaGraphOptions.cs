namespace WhatsAppAI.Infrastructure.Meta;

public sealed class MetaGraphOptions
{
    public const string SectionName = "Meta";
    public string GraphVersion { get; init; } = "v26.0";

    public string BaseUrl => $"https://graph.facebook.com/{GraphVersion.Trim('/')}";
}
