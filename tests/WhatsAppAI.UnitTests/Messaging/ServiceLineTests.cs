using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.UnitTests.Messaging;

public sealed class ServiceLineTests
{
    [Theory]
    [InlineData("Preciso de duvida sobre a plataforma")]
    [InlineData("Tenho DÚVIDA sobre a plataforma")]
    public void MatchesKeywords_IgnoresAccents(string message)
    {
        var queue = ServiceLine.Create(Guid.NewGuid(), "Suporte");
        queue.SetKeywords("DÚVIDA, suporte");

        Assert.True(queue.MatchesKeywords(message));
    }

    [Fact]
    public void MatchesKeywords_RequiresTheStartOfAWord()
    {
        var queue = ServiceLine.Create(Guid.NewGuid(), "Comercial");
        queue.SetKeywords("IA");

        Assert.False(queue.MatchesKeywords("Bom dia, tudo bem?"));
        Assert.True(queue.MatchesKeywords("Quero falar sobre IA"));
    }

    [Fact]
    public void SetTransferNotice_NormalizesBlankTextAndRejectsLongMessages()
    {
        var queue = ServiceLine.Create(Guid.NewGuid(), "Suporte");

        queue.SetTransferNotice("  Aguarde o suporte.  ");
        Assert.Equal("Aguarde o suporte.", queue.TransferNotice);

        queue.SetTransferNotice(" ");
        Assert.Null(queue.TransferNotice);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            queue.SetTransferNotice(new string('a', ServiceLine.TransferNoticeMaxLength + 1)));
    }
}
