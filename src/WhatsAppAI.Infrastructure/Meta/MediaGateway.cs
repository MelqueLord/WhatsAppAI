using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.Infrastructure.Meta;

internal sealed class MediaGateway(
    HttpClient httpClient,
    ILogger<MediaGateway> logger) : IMediaGateway
{
    private const string BaseUrl = "https://graph.facebook.com/v21.0";

    public async Task<MediaDownloadResult> DownloadAsync(
        string mediaId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Get media URL from Meta
            var metaResponse = await httpClient.GetAsync(
                $"{BaseUrl}/{mediaId}",
                cancellationToken);

            if (!metaResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch media info: {StatusCode}", metaResponse.StatusCode);
                return new MediaDownloadResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Failed to fetch media from WhatsApp."
                };
            }

            var mediaInfo = await metaResponse.Content.ReadFromJsonAsync<MetaMediaResponse>(
                cancellationToken: cancellationToken);

            if (mediaInfo?.Url is null)
            {
                return new MediaDownloadResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Media URL not available."
                };
            }

            // Download the actual media
            var mediaResponse = await httpClient.GetAsync(
                mediaInfo.Url,
                cancellationToken);

            if (!mediaResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to download media: {StatusCode}", mediaResponse.StatusCode);
                return new MediaDownloadResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Failed to download media."
                };
            }

            var contentType = mediaResponse.Content.Headers.ContentType?.MediaType
                ?? "application/octet-stream";
            var mediaBytes = await mediaResponse.Content.ReadAsByteArrayAsync(cancellationToken);

            return new MediaDownloadResult
            {
                IsSuccess = true,
                Content = mediaBytes,
                ContentType = contentType
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download media {MediaId}", mediaId);
            return new MediaDownloadResult
            {
                IsSuccess = false,
                ErrorMessage = "Failed to download media."
            };
        }
    }
}

internal sealed class MetaMediaResponse
{
    public string? Url { get; set; }
    public string? Mime { get; set; }
    public string? Sha256 { get; set; }
    public long? FileSize { get; set; }
    public string? Id { get; set; }
}
