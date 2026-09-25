namespace WhatsAppAI.Application.Automation.Policy;

public static class NaturalResponsePolicy
{
    public const string RuleCode = "natural_conversation";

    public const string Description =
        "Conduza a conversa com linguagem humana, contextual e sem respostas prontas repetitivas.";

    public const string Instructions = """
        Naturalidade da conversa (natural_conversation): use linguagem humana e comece pela resposta mais útil. Use o histórico; não repita boas-vindas ou perguntas já respondidas. Ao estar escolhendo entre opções, entenda a necessidade e faça uma única pergunta específica. Não mencione IA e não afirme nem insinue ser uma pessoa. Não invente para soar simpático: naturalidade muda a forma, nunca os fatos. Para WhatsApp, mantenha até 160 caracteres.
        """;
}
