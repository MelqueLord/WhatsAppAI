using WhatsAppAI.Infrastructure.Observability;

namespace WhatsAppAI.UnitTests.Observability;

public class SanitizingEnricherTests
{
    [Fact]
    public void Sanitize_PhoneNumber_ReturnsRedacted()
    {
        var input = "Customer +5511999887766 sent a message";
        var result = SanitizingEnricher.Sanitize(input);
        Assert.DoesNotContain("+5511999887766", result);
        Assert.Contains("***PHONE***", result);
    }

    [Fact]
    public void Sanitize_TokenKeyValue_ReturnsRedacted()
    {
        var input = "token=abc123secret value";
        var result = SanitizingEnricher.Sanitize(input);
        Assert.DoesNotContain("abc123secret", result);
        Assert.Contains("***REDACTED***", result);
    }

    [Fact]
    public void Sanitize_SecretKeyValue_ReturnsRedacted()
    {
        var input = "secret: my-super-secret-value";
        var result = SanitizingEnricher.Sanitize(input);
        Assert.DoesNotContain("my-super-secret-value", result);
        Assert.Contains("***REDACTED***", result);
    }

    [Fact]
    public void Sanitize_Email_ReturnsRedacted()
    {
        var input = "User john.doe@example.com registered";
        var result = SanitizingEnricher.Sanitize(input);
        Assert.DoesNotContain("john.doe@example.com", result);
        Assert.Contains("***EMAIL***", result);
    }

    [Fact]
    public void Sanitize_Null_ReturnsEmpty()
    {
        var result = SanitizingEnricher.Sanitize(null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Sanitize_NormalText_ReturnsUnchanged()
    {
        var input = "This is a normal log message";
        var result = SanitizingEnricher.Sanitize(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Sanitize_MultipleSensitiveData_AllRedacted()
    {
        var input = "User +5511999887766 with token=secret123 and email@test.com";
        var result = SanitizingEnricher.Sanitize(input);
        Assert.DoesNotContain("+5511999887766", result);
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("email@test.com", result);
    }
}
