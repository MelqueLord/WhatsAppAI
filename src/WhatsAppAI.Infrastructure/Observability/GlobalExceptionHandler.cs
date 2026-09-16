using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Domain;

namespace WhatsAppAI.Infrastructure.Observability;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
            return false;

        var (statusCode, code, message) = Map(exception);
        var correlationId = httpContext.Items[CorrelationIdMiddleware.CorrelationIdProperty]?.ToString()
            ?? httpContext.TraceIdentifier;

        logger.LogError(
            "Request failed with {ErrorCode}; exception type {ExceptionType}; correlation {CorrelationId}",
            code,
            exception.GetType().Name,
            correlationId);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(
            new GlobalErrorResponse(code, message, correlationId),
            cancellationToken);

        return true;
    }

    private static (int StatusCode, string Code, string Message) Map(Exception exception) => exception switch
    {
        ConcurrencyException or DbUpdateConcurrencyException => (
            StatusCodes.Status409Conflict,
            "CONFLICT",
            "Esta informação foi alterada por outra pessoa. Atualize a página e tente novamente."),
        ArgumentException or FormatException or BadHttpRequestException => (
            StatusCodes.Status400BadRequest,
            "INVALID_REQUEST",
            "Confira os dados informados e tente novamente."),
        _ => (
            StatusCodes.Status500InternalServerError,
            "UNEXPECTED_ERROR",
            "Não foi possível concluir agora. Tente novamente em alguns instantes.")
    };

    private sealed record GlobalErrorResponse(string Code, string Message, string CorrelationId);
}
