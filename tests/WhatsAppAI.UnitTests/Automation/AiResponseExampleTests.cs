using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiResponseExampleTests
{
    [Fact]
    public void Create_TrimsContentAndActivatesExample()
    {
        var example = AiResponseExample.Create(Guid.NewGuid(), "  Quero agendar  ", "  Posso ajudar com o agendamento.  ");

        Assert.Equal("Quero agendar", example.CustomerMessage);
        Assert.Equal("Posso ajudar com o agendamento.", example.IdealResponse);
        Assert.True(example.IsActive);
        Assert.Equal(1u, example.Version);
    }

    [Fact]
    public void Update_RequiresCurrentVersion()
    {
        var example = AiResponseExample.Create(Guid.NewGuid(), "Mensagem", "Resposta");

        Assert.Throws<ConcurrencyException>(() => example.Update("Outra", "Nova", 99));
    }

    [Fact]
    public void DeactivateAndReactivate_IncrementVersion()
    {
        var example = AiResponseExample.Create(Guid.NewGuid(), "Mensagem", "Resposta");

        example.Deactivate(1);
        example.Reactivate(2);

        Assert.True(example.IsActive);
        Assert.Equal(3u, example.Version);
    }
}
