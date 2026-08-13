namespace WhatsAppAI.Application.Abstractions;

public interface IMediaGateway
{
    Task<MediaDownloadResult> DownloadAsync(
        string mediaId,
        string accessToken,
        CancellationToken cancellationToken = default);
}

public sealed record MediaDownloadResult
{
    public bool IsSuccess { get; init; }
    public ReadOnlyMemory<byte> Content { get; init; }
    public string? ContentType { get; init; }
    public string? ErrorMessage { get; init; }
}
