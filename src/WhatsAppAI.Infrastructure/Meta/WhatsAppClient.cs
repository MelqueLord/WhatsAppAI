using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Integrations;

namespace WhatsAppAI.Infrastructure.Meta;

internal sealed class WhatsAppClient(
    HttpClient httpClient,
    ILogger<WhatsAppClient> logger) : IWhatsAppClient
{
    private const string BaseUrl = "https://graph.facebook.com/v21.0";

    public async Task<WhatsAppConnectionResult> TestConnectionAsync(
        string phoneNumberId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/{phoneNumberId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<PhoneNumberResponse>(
                    cancellationToken: cancellationToken);

                return new WhatsAppConnectionResult
                {
                    IsSuccess = true,
                    PhoneNumber = content?.DisplayPhoneNumber,
                    QualityRating = content?.QualityRating
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("WhatsApp API returned {StatusCode}: {Error}",
                response.StatusCode, SanitizeError(errorContent));

            return new WhatsAppConnectionResult
            {
                IsSuccess = false,
                ErrorMessage = GetSanitizedErrorMessage(response.StatusCode)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test WhatsApp connection");
            return new WhatsAppConnectionResult
            {
                IsSuccess = false,
                ErrorMessage = "Connection failed. Please check your credentials."
            };
        }
    }

    public async Task<SendMessageResult> SendTextMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new SendMessageRequest
            {
                MessagingProduct = "whatsapp",
                To = recipientPhone,
                Type = "text",
                Text = new TextBody { Body = text }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/{phoneNumberId}/messages")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<SendMessageResponse>(
                    cancellationToken: cancellationToken);

                return new SendMessageResult
                {
                    IsSuccess = true,
                    MessageId = content?.Messages?.FirstOrDefault()?.Id
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("WhatsApp API returned {StatusCode}: {Error}",
                response.StatusCode, SanitizeError(errorContent));

            return new SendMessageResult
            {
                IsSuccess = false,
                ErrorMessage = "Failed to send message."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp message");
            return new SendMessageResult
            {
                IsSuccess = false,
                ErrorMessage = "Failed to send message."
            };
        }
    }

    public async Task<SendMessageResult> SendMediaMessageAsync(
        string phoneNumberId, string accessToken, string recipientPhone,
        string mediaType, string mediaContent, string? caption, string? fileName,
        CancellationToken cancellationToken = default)
    {
        if (mediaType != "image" || !TryReadImageDataUrl(mediaContent, out var contentType, out var bytes))
            return new SendMessageResult { IsSuccess = false, IsRetryable = false, ErrorMessage = "Unsupported image content." };

        try
        {
            using var upload = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            upload.Add(fileContent, "file", fileName ?? "image");
            upload.Add(new StringContent("whatsapp"), "messaging_product");
            upload.Add(new StringContent(contentType), "type");
            using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{phoneNumberId}/media") { Content = upload };
            uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var uploadResponse = await httpClient.SendAsync(uploadRequest, cancellationToken);
            if (!uploadResponse.IsSuccessStatusCode)
                return new SendMessageResult { IsSuccess = false, IsRetryable = uploadResponse.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)uploadResponse.StatusCode >= 500, ErrorMessage = "Failed to upload image." };

            var media = await uploadResponse.Content.ReadFromJsonAsync<UploadMediaResponse>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(media?.Id))
                return new SendMessageResult { IsSuccess = false, IsRetryable = true, ErrorMessage = "Image upload did not return an identifier." };

            var payload = new SendImageMessageRequest
            {
                To = recipientPhone,
                Image = new ImageBody { Id = media.Id, Caption = string.IsNullOrWhiteSpace(caption) ? null : caption }
            };
            using var sendRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{phoneNumberId}/messages") { Content = JsonContent.Create(payload) };
            sendRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var sendResponse = await httpClient.SendAsync(sendRequest, cancellationToken);
            if (!sendResponse.IsSuccessStatusCode)
                return new SendMessageResult { IsSuccess = false, IsRetryable = sendResponse.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)sendResponse.StatusCode >= 500, ErrorMessage = "Failed to send image." };

            var sent = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
            return new SendMessageResult { IsSuccess = true, MessageId = sent?.Messages?.FirstOrDefault()?.Id };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp image");
            return new SendMessageResult { IsSuccess = false, ErrorMessage = "Failed to send image." };
        }
    }

    private static bool TryReadImageDataUrl(string value, out string contentType, out byte[] bytes)
    {
        contentType = string.Empty;
        bytes = [];
        var separator = value.IndexOf(',');
        if (separator <= 5 || !value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            !value[..separator].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return false;
        contentType = value[5..(separator - ";base64".Length)];
        if (contentType is not ("image/jpeg" or "image/png")) return false;
        try { bytes = Convert.FromBase64String(value[(separator + 1)..]); return bytes.Length is > 0 and <= 5 * 1024 * 1024; }
        catch (FormatException) { return false; }
    }

    public async Task<SendMessageResult> SendTemplateMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        string templateName,
        string templateLanguage,
        IReadOnlyList<string> parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new SendTemplateMessageRequest
            {
                MessagingProduct = "whatsapp",
                To = recipientPhone,
                Type = "template",
                Template = new TemplateBody
                {
                    Name = templateName,
                    Language = new TemplateLanguage { Code = templateLanguage },
                    Components = parameters.Count == 0
                        ? null
                        :
                        [new TemplateComponent
                        {
                            Type = "body",
                            Parameters = parameters.Select(value => new TemplateParameter
                            {
                                Type = "text",
                                Text = value
                            }).ToList()
                        }]
                }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/{phoneNumberId}/messages")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<SendMessageResponse>(
                    cancellationToken: cancellationToken);
                return new SendMessageResult
                {
                    IsSuccess = true,
                    MessageId = content?.Messages?.FirstOrDefault()?.Id
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("WhatsApp template API returned {StatusCode}: {Error}",
                response.StatusCode, SanitizeError(errorContent));
            return new SendMessageResult { IsSuccess = false, ErrorMessage = "Failed to send template message." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp template message");
            return new SendMessageResult { IsSuccess = false, ErrorMessage = "Failed to send template message." };
        }
    }

    public async Task<WhatsAppTemplateListResult> ListTemplatesAsync(
        string wabaId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var templates = new List<WhatsAppTemplateSummary>();
            var visitedPages = new HashSet<string>(StringComparer.Ordinal);
            string? nextPageUrl = $"{BaseUrl}/{wabaId}/message_templates?fields=name,language,status,category,components&limit=250";

            while (!string.IsNullOrWhiteSpace(nextPageUrl) && visitedPages.Add(nextPageUrl))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, nextPageUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("WhatsApp template API returned {StatusCode}", response.StatusCode);
                    return new WhatsAppTemplateListResult
                    {
                        ErrorMessage = GetSanitizedErrorMessage(response.StatusCode)
                    };
                }

                var content = await response.Content.ReadFromJsonAsync<TemplateListResponse>(
                    cancellationToken: cancellationToken);
                templates.AddRange((content?.Data ?? [])
                    .Where(template =>
                        !string.IsNullOrWhiteSpace(template.Name) &&
                        !string.IsNullOrWhiteSpace(template.Language))
                    .Select(template => new WhatsAppTemplateSummary(
                        template.Name!,
                        template.Language!,
                        CountBodyParameters(template.Components),
                        string.IsNullOrWhiteSpace(template.Category) ? "UNKNOWN" : template.Category.Trim().ToUpperInvariant(),
                        string.IsNullOrWhiteSpace(template.Status) ? "UNKNOWN" : template.Status.Trim().ToUpperInvariant(),
                        HasOnlySupportedTemplateComponents(template.Components))));

                nextPageUrl = IsTrustedMetaPageUrl(content?.Paging?.Next)
                    ? content?.Paging?.Next
                    : null;
            }

            return new WhatsAppTemplateListResult
            {
                IsSuccess = true,
                Templates = templates
                    .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(template => template.Language, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list WhatsApp templates");
            return new WhatsAppTemplateListResult { ErrorMessage = "Unable to load templates." };
        }
    }

    public Task<WhatsAppQrCodeResult> GetQrCodeAsync(
        Guid tenantId,
        int lineNumber = 1,
        CancellationToken cancellationToken = default)
    {
        // Official API doesn't support QR code connection
        return Task.FromResult(new WhatsAppQrCodeResult
        {
            IsSuccess = false,
            ErrorMessage = "QR code connection is not available with the official API. Please use the API configuration."
        });
    }

    public Task<WhatsAppSessionStatus> GetSessionStatusAsync(
        Guid tenantId,
        int lineNumber = 1,
        CancellationToken cancellationToken = default)
    {
        // Official API doesn't have session concept
        return Task.FromResult(new WhatsAppSessionStatus
        {
            IsConnected = false,
            Status = "not_applicable"
        });
    }

    public Task DisconnectSessionAsync(
        Guid tenantId,
        int lineNumber = 1,
        CancellationToken cancellationToken = default)
    {
        // Official API doesn't have session concept
        return Task.CompletedTask;
    }

    private static string SanitizeError(string error)
    {
        // Remove any potential tokens or sensitive data from error messages
        if (error.Contains("access_token"))
            return "Authentication error";
        if (error.Contains("OAuthException"))
            return "Authentication error";

        return "API error";
    }

    private static string GetSanitizedErrorMessage(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Invalid credentials. Please check your access token.",
            System.Net.HttpStatusCode.Forbidden => "Access denied. Please check your permissions.",
            System.Net.HttpStatusCode.NotFound => "Phone number not found. Please check your Phone Number ID.",
            System.Net.HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please try again later.",
            _ => "Connection failed. Please check your configuration."
        };
    }

    private static int CountBodyParameters(IReadOnlyList<TemplateListComponent>? components)
    {
        var body = components?.FirstOrDefault(component =>
            string.Equals(component.Type, "BODY", StringComparison.OrdinalIgnoreCase));
        return body?.Text is null ? 0 : System.Text.RegularExpressions.Regex.Count(body.Text, @"\{\{\d+\}\}");
    }

    private static bool HasOnlySupportedTemplateComponents(IReadOnlyList<TemplateListComponent>? components) =>
        components is { Count: > 0 } &&
        components.Any(component =>
            string.Equals(component.Type, "BODY", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(component.Text)) &&
        components.All(component =>
            string.Equals(component.Type, "BODY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(component.Type, "FOOTER", StringComparison.OrdinalIgnoreCase)) &&
        CountBodyParameters(components) <= 10;

    private static bool IsTrustedMetaPageUrl(string? pageUrl) =>
        Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, "graph.facebook.com", StringComparison.OrdinalIgnoreCase);
}

internal sealed class PhoneNumberResponse
{
    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; init; }

    [JsonPropertyName("quality_rating")]
    public string? QualityRating { get; init; }
}

internal sealed class SendMessageRequest
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; init; } = "whatsapp";

    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public TextBody Text { get; init; } = new();
}

internal sealed class TemplateListResponse
{
    [JsonPropertyName("data")]
    public List<TemplateListItem> Data { get; init; } = [];

    [JsonPropertyName("paging")]
    public TemplateListPaging? Paging { get; init; }
}

internal sealed class TemplateListPaging
{
    [JsonPropertyName("next")]
    public string? Next { get; init; }
}

internal sealed class TemplateListItem
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("components")]
    public List<TemplateListComponent>? Components { get; init; }
}

internal sealed class TemplateListComponent
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

internal sealed class SendTemplateMessageRequest
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; init; } = "whatsapp";

    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "template";

    [JsonPropertyName("template")]
    public TemplateBody Template { get; init; } = new();
}

internal sealed class TemplateBody
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("language")]
    public TemplateLanguage Language { get; init; } = new();

    [JsonPropertyName("components")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TemplateComponent>? Components { get; init; }
}

internal sealed class TemplateLanguage
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;
}

internal sealed class TemplateComponent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<TemplateParameter> Parameters { get; init; } = [];
}

internal sealed class TemplateParameter
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

internal sealed class TextBody
{
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;
}

internal sealed class SendMessageResponse
{
    [JsonPropertyName("messages")]
    public List<MessageId>? Messages { get; init; }
}

internal sealed class UploadMediaResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

internal sealed class SendImageMessageRequest
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; init; } = "whatsapp";
    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;
    [JsonPropertyName("type")]
    public string Type { get; init; } = "image";
    [JsonPropertyName("image")]
    public ImageBody Image { get; init; } = new();
}

internal sealed class ImageBody
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    [JsonPropertyName("caption")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Caption { get; init; }
}

internal sealed class MessageId
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}
