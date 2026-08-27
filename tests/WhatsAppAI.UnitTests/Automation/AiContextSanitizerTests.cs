using WhatsAppAI.Application.Automation.Context;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiContextSanitizerTests
{
    [Fact]
    public void RedactPersonalData_RemovesCommonContactIdentifiers()
    {
        var result = AiContextSanitizer.RedactPersonalData(
            "Email ana@example.com, telefone +55 (11) 99999-9999, CPF 123.456.789-01 e CNPJ 12.345.678/0001-99.");

        Assert.DoesNotContain("ana@example.com", result);
        Assert.DoesNotContain("99999-9999", result);
        Assert.DoesNotContain("123.456.789-01", result);
        Assert.DoesNotContain("12.345.678/0001-99", result);
    }
}
