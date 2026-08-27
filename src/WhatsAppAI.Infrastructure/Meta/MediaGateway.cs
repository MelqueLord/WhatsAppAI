using System.Net.Http.Headers;
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
            // Get media URL from Meta
            using var metaRequest = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/{mediaId}");
            metaRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var metaResponse = await httpClient.SendAsync(metaRequest, cancellationToken);

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
            using var mediaRequest = new HttpRequestMessage(HttpMethod.Get, mediaInfo.Url);
            mediaRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var mediaResponse = await httpClient.SendAsync(mediaRequest, cancellationToken);

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
