using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace WhatsAppAI.Infrastructure.Observability;

public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public const string CorrelationIdProperty = "CorrelationId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.CorrelationIdProperty]?.ToString()
            ?? "no-context";

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(CorrelationIdProperty, correlationId));
    }
}
