using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Domain;
using WhatsAppAI.Infrastructure.Observability;

namespace WhatsAppAI.UnitTests.Observability;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsFriendlyConflictWithoutExceptionDetails()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdProperty] = "correlation-test";
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new ConcurrencyException("Internal version details must not be returned."),
            CancellationToken.None);

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Contains("CONFLICT", payload, StringComparison.Ordinal);
        Assert.Contains("Atualize a página", payload, StringComparison.Ordinal);
        Assert.Contains("correlation-test", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Internal version details", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsGenericMessageForUnexpectedErrors()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("Sensitive implementation detail"),
            CancellationToken.None);

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("UNEXPECTED_ERROR", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive implementation detail", payload, StringComparison.Ordinal);
    }
}
