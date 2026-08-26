using System.Text.Json.Serialization;

namespace WhatsAppAI.Infrastructure.Meta.Models;

public sealed class WebhookPayload
{
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("entry")]
    public List<WebhookEntry>? Entry { get; set; }
}

public sealed class WebhookEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("changes")]
    public List<WebhookChange>? Changes { get; set; }
}

public sealed class WebhookChange
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("value")]
    public WebhookValue? Value { get; set; }
}

public sealed class WebhookValue
{
    [JsonPropertyName("messaging_product")]
    public string? MessagingProduct { get; set; }

    [JsonPropertyName("metadata")]
    public WebhookMetadata? Metadata { get; set; }

    [JsonPropertyName("contacts")]
    public List<WebhookContact>? Contacts { get; set; }

    [JsonPropertyName("messages")]
    public List<WebhookMessage>? Messages { get; set; }

    [JsonPropertyName("statuses")]
    public List<WebhookStatus>? Statuses { get; set; }
}

public sealed class WebhookMetadata
{
    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; set; }

    [JsonPropertyName("phone_number_id")]
    public string? PhoneNumberId { get; set; }
}

public sealed class WebhookContact
{
    [JsonPropertyName("wa_id")]
    public string? WaId { get; set; }

    [JsonPropertyName("profile")]
    public WebhookProfile? Profile { get; set; }
}

public sealed class WebhookProfile
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class WebhookMessage
{
    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? PushName { get; set; }

    [JsonPropertyName("text")]
    public WebhookText? Text { get; set; }

    [JsonPropertyName("image")]
    public WebhookImage? Image { get; set; }

    [JsonPropertyName("document")]
    public WebhookDocument? Document { get; set; }

    [JsonPropertyName("audio")]
    public WebhookAudio? Audio { get; set; }
}

public sealed class WebhookText
{
    [JsonPropertyName("body")]
    public string? Body { get; set; }
}

public sealed class WebhookImage
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("mime")]
    public string? Mime { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }
}

public sealed class WebhookDocument
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("mime")]
    public string? Mime { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }
}

public sealed class WebhookAudio
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("mime")]
    public string? Mime { get; set; }

    [JsonPropertyName("voice")]
    public bool? Voice { get; set; }
}

public sealed class WebhookStatus
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("recipient_id")]
    public string? RecipientId { get; set; }
}
