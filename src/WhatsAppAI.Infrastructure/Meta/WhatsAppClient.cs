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
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync(
                $"{BaseUrl}/{phoneNumberId}",
                cancellationToken);

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
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var request = new SendMessageRequest
            {
                MessagingProduct = "whatsapp",
                To = recipientPhone,
                Type = "text",
                Text = new TextBody { Body = text }
            };

            var response = await httpClient.PostAsJsonAsync(
                $"{BaseUrl}/{phoneNumberId}/messages",
                request,
                cancellationToken);

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

internal sealed class MessageId
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}
