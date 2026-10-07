using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppAI.Application.Integrations;

namespace WhatsAppAI.Infrastructure.Meta;

internal sealed class WhatsAppClient : IWhatsAppClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<WhatsAppClient> logger;
    private readonly string baseUrl;

    public WhatsAppClient(HttpClient httpClient, ILogger<WhatsAppClient> logger, IOptions<MetaGraphOptions> options)
    {
        this.httpClient = httpClient;
        this.logger = logger;
        baseUrl = options.Value.BaseUrl;
    }

    internal WhatsAppClient(HttpClient httpClient, ILogger<WhatsAppClient> logger)
        : this(httpClient, logger, Options.Create(new MetaGraphOptions())) { }

    public async Task<WhatsAppConnectionResult> TestConnectionAsync(
        string phoneNumberId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/{phoneNumberId}");
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
                $"{baseUrl}/{phoneNumberId}/messages")
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
        Stream mediaStream, string contentType, long contentLength, string contentSha256,
        string? caption, string? fileName, string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (contentType is not ("image/jpeg" or "image/png") || contentLength is <= 0 or > 5 * 1024 * 1024 ||
            !IsSha256(contentSha256))
            return new SendMessageResult { IsSuccess = false, IsRetryable = false, ErrorMessage = "Unsupported image content." };

        try
        {
            using var upload = new MultipartFormDataContent();
            var fileContent = new StreamContent(mediaStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            upload.Add(fileContent, "file", fileName ?? "image");
            upload.Add(new StringContent("whatsapp"), "messaging_product");
            upload.Add(new StringContent(contentType), "type");
            using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{phoneNumberId}/media") { Content = upload };
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
            using var sendRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{phoneNumberId}/messages") { Content = JsonContent.Create(payload) };
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

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    public async Task<SendMessageResult> SendTemplateMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        string templateName,
        string templateLanguage,
        IReadOnlyList<WhatsAppTemplateParameter> parameters,
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
                                Text = value.Text,
                                ParameterName = value.Name
                            }).ToList()
                        }]
                }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/{phoneNumberId}/messages")
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
            string? nextPageUrl = $"{baseUrl}/{wabaId}/message_templates?fields=id,name,language,status,category,parameter_format,components&limit=250";

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
                        ErrorMessage = GetSanitizedTemplateListError(response.StatusCode)
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
                        HasOnlySupportedTemplateComponents(template.Components) &&
                        HasSupportedParameters(template.Components, template.ParameterFormat))
                    {
                        MetaTemplateId = template.Id,
                        BodyText = GetComponentText(template.Components, "BODY") ?? string.Empty,
                        FooterText = GetComponentText(template.Components, "FOOTER"),
                        ComponentsJson = JsonSerializer.Serialize(template.Components ?? []),
                        ParameterFormat = NormalizeParameterFormat(template.ParameterFormat),
                        BodyParameterNames = GetBodyParameterNames(template.Components, template.ParameterFormat)
                    }));

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

    public async Task<WhatsAppTemplateCreateResult> CreateTemplateAsync(
        string wabaId,
        string accessToken,
        WhatsAppTemplateCreateRequest template,
        CancellationToken cancellationToken = default)
    {
        var validation = WhatsAppTemplateValidator.Validate(template);
        if (validation.Count > 0)
            return new WhatsAppTemplateCreateResult { FailureKind = WhatsAppTemplateFailureKind.Validation, ErrorMessage = "Template inválido." };

        var components = new List<CreateTemplateComponent>
        {
            new()
            {
                Type = "BODY",
                Text = template.BodyText,
                Example = template.BodyExamples.Count == 0 ? null : new CreateTemplateExample { BodyText = [template.BodyExamples.ToArray()] }
            }
        };
        if (!string.IsNullOrWhiteSpace(template.FooterText))
            components.Add(new CreateTemplateComponent { Type = "FOOTER", Text = template.FooterText });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{wabaId}/message_templates")
        {
            Content = JsonContent.Create(new CreateTemplatePayload
            {
                Name = template.Name,
                Language = template.Language,
                Category = template.Category,
                Components = components
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var success = JsonSerializer.Deserialize<CreateTemplateResponse>(raw);
                return new WhatsAppTemplateCreateResult
                {
                    IsSuccess = true,
                    MetaTemplateId = success?.Id,
                    Status = string.IsNullOrWhiteSpace(success?.Status) ? "PENDING" : success.Status.ToUpperInvariant(),
                    Category = string.IsNullOrWhiteSpace(success?.Category) ? template.Category : success.Category.ToUpperInvariant()
                };
            }

            var graphError = TryReadGraphError(raw);
            var failure = ClassifyTemplateFailure(response.StatusCode, graphError?.Code, graphError?.ErrorSubcode);
            logger.LogWarning("Meta template creation failed with HTTP {StatusCode}, Graph code {GraphCode}, subcode {Subcode}",
                (int)response.StatusCode, graphError?.Code, graphError?.ErrorSubcode);
            return new WhatsAppTemplateCreateResult
            {
                FailureKind = failure,
                ErrorCode = graphError?.ErrorSubcode?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    ?? graphError?.Code?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ErrorMessage = SanitizedTemplateError(failure)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WhatsAppTemplateCreateResult { FailureKind = WhatsAppTemplateFailureKind.OutcomeUnknown, ErrorMessage = "O resultado da submissão é incerto e será reconciliado." };
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Meta template creation ended without a confirmed response");
            return new WhatsAppTemplateCreateResult { FailureKind = WhatsAppTemplateFailureKind.OutcomeUnknown, ErrorMessage = "O resultado da submissão é incerto e será reconciliado." };
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

    private static string GetSanitizedTemplateListError(System.Net.HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "A credencial da linha não é válida para consultar templates.",
        HttpStatusCode.Forbidden => "A credencial não possui permissão para consultar templates. Configure whatsapp_business_management na Meta.",
        HttpStatusCode.TooManyRequests => "A Meta limitou temporariamente a consulta de templates. Tente novamente em alguns instantes.",
        _ => "A Meta não conseguiu carregar os templates desta linha. Verifique a configuração da API Oficial."
    };

    private static int CountBodyParameters(IReadOnlyList<TemplateListComponent>? components) =>
        GetBodyParameterNames(components, "POSITIONAL").Length;

    private static string[] GetBodyParameterNames(IReadOnlyList<TemplateListComponent>? components, string? parameterFormat)
    {
        var body = components?.FirstOrDefault(component =>
            string.Equals(component.Type, "BODY", StringComparison.OrdinalIgnoreCase));
        if (body?.Text is null)
            return [];

        var names = System.Text.RegularExpressions.Regex.Matches(body.Text, @"\{\{\s*([A-Za-z][A-Za-z0-9_]*|\d+)\s*\}\}")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return string.Equals(NormalizeParameterFormat(parameterFormat), "POSITIONAL", StringComparison.Ordinal)
            ? names.OrderBy(name => int.TryParse(name, out var position) ? position : int.MaxValue).ToArray()
            : names;
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

    private static bool HasSupportedParameters(IReadOnlyList<TemplateListComponent>? components, string? parameterFormat)
    {
        var names = GetBodyParameterNames(components, parameterFormat);
        if (names.Length > 10)
            return false;

        var format = NormalizeParameterFormat(parameterFormat);
        return format switch
        {
            "POSITIONAL" => Array.TrueForAll(names, name => int.TryParse(name, out _)),
            "NAMED" => Array.TrueForAll(names, name => !int.TryParse(name, out _)),
            _ => false
        };
    }

    private static string NormalizeParameterFormat(string? parameterFormat) =>
        string.IsNullOrWhiteSpace(parameterFormat) ? "POSITIONAL" : parameterFormat.Trim().ToUpperInvariant();

    private static bool IsTrustedMetaPageUrl(string? pageUrl) =>
        Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, "graph.facebook.com", StringComparison.OrdinalIgnoreCase);

    private static string? GetComponentText(IReadOnlyList<TemplateListComponent>? components, string type) =>
        components?.FirstOrDefault(component => string.Equals(component.Type, type, StringComparison.OrdinalIgnoreCase))?.Text;

    private static GraphError? TryReadGraphError(string raw)
    {
        try { return JsonSerializer.Deserialize<GraphErrorEnvelope>(raw)?.Error; }
        catch (JsonException) { return null; }
    }

    private static WhatsAppTemplateFailureKind ClassifyTemplateFailure(HttpStatusCode status, int? code, int? subcode)
    {
        if (subcode == 2388024) return WhatsAppTemplateFailureKind.Duplicate;
        if (status == HttpStatusCode.Forbidden || code == 200) return WhatsAppTemplateFailureKind.Authorization;
        if (status == HttpStatusCode.TooManyRequests) return WhatsAppTemplateFailureKind.RateLimit;
        if ((int)status >= 500 || status == HttpStatusCode.RequestTimeout) return WhatsAppTemplateFailureKind.Transient;
        return WhatsAppTemplateFailureKind.Validation;
    }

    private static string SanitizedTemplateError(WhatsAppTemplateFailureKind kind) => kind switch
    {
        WhatsAppTemplateFailureKind.Duplicate => "Já existe um template com esse nome e idioma; o catálogo será reconciliado.",
        WhatsAppTemplateFailureKind.Authorization => "A credencial não possui permissão para gerenciar templates.",
        WhatsAppTemplateFailureKind.RateLimit => "O limite de criação da Meta foi atingido. Tente novamente mais tarde.",
        WhatsAppTemplateFailureKind.Transient => "A Meta está temporariamente indisponível.",
        _ => "A Meta rejeitou o template. Revise os dados informados."
    };
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
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("parameter_format")]
    public string? ParameterFormat { get; init; }

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

    [JsonPropertyName("parameter_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParameterName { get; init; }
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

internal sealed class CreateTemplatePayload
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("language")] public string Language { get; init; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; init; } = string.Empty;
    [JsonPropertyName("parameter_format")] public string ParameterFormat { get; init; } = "POSITIONAL";
    [JsonPropertyName("components")] public List<CreateTemplateComponent> Components { get; init; } = [];
}

internal sealed class CreateTemplateComponent
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("text")] public string Text { get; init; } = string.Empty;
    [JsonPropertyName("example")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CreateTemplateExample? Example { get; init; }
}

internal sealed class CreateTemplateExample
{
    [JsonPropertyName("body_text")] public List<string[]> BodyText { get; init; } = [];
}

internal sealed class CreateTemplateResponse
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("category")] public string? Category { get; init; }
}

internal sealed class GraphErrorEnvelope
{
    [JsonPropertyName("error")] public GraphError? Error { get; init; }
}

internal sealed class GraphError
{
    [JsonPropertyName("code")] public int? Code { get; init; }
    [JsonPropertyName("error_subcode")] public int? ErrorSubcode { get; init; }
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
