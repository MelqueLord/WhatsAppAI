namespace WhatsAppAI.Application.Automation.Policy;

public static class BusinessProfileGuidePolicy
{
    private static readonly IReadOnlyDictionary<string, string> BusinessGuides =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Clínica e saúde"] = "acolha, identifique a necessidade e oriente sobre serviços, preparo e agendamento cadastrados; não forneça diagnóstico ou orientação clínica",
            ["Restaurante e alimentação"] = "entenda se a dúvida é sobre cardápio, pedido, reserva, retirada ou entrega e conduza ao próximo passo cadastrado",
            ["Comércio e varejo"] = "identifique produto, necessidade, compra, pagamento, entrega ou troca e apresente somente condições cadastradas",
            ["E-commerce"] = "ajude a localizar produtos e a entender compra, pagamento, envio, rastreio e troca usando apenas dados cadastrados",
            ["Serviços profissionais"] = "compreenda o problema do cliente, explique os serviços relacionados e conduza para diagnóstico comercial ou próximo passo cadastrado",
            ["Educação"] = "identifique curso, modalidade, matrícula, calendário ou suporte acadêmico e use somente informações oficiais cadastradas",
            ["Imobiliária"] = "entenda se o interesse é compra, venda, locação ou administração e colete a necessidade sem inventar imóveis ou condições",
            ["Hotelaria e turismo"] = "identifique destino, hospedagem, período ou experiência desejada sem afirmar vaga, tarifa ou reserva não confirmada",
            ["Beleza e estética"] = "identifique o serviço desejado e oriente sobre cuidados e agendamento cadastrados sem prometer resultado",
            ["Transportes e logística"] = "entenda origem, destino, tipo de carga ou entrega e informe somente cobertura, prazo e condição cadastrados",
            ["Tecnologia e software"] = "entenda objetivo, funcionalidade, implantação, integração ou suporte e explique capacidades cadastradas sem prometer recurso inexistente",
            ["Contabilidade e finanças"] = "identifique a necessidade contábil, fiscal ou administrativa e forneça orientação geral sem aconselhamento financeiro individual",
            ["Advocacia e serviços jurídicos"] = "acolha e classifique a demanda, explique o processo de atendimento e encaminhe orientação jurídica individual para um profissional",
            ["Oficina e serviços automotivos"] = "identifique veículo, sintoma e serviço desejado sem diagnosticar defeito, prazo ou preço não confirmados",
            ["Construção e arquitetura"] = "entenda tipo de obra, etapa e necessidade e explique serviços e processo cadastrados sem estimar prazo ou custo",
            ["Academia e esportes"] = "identifique objetivo, modalidade, plano ou horário e evite prescrição física ou de saúde não autorizada",
            ["Pet shop e veterinária"] = "identifique o animal e a necessidade de produto, serviço ou atendimento; questões clínicas exigem profissional responsável",
            ["Eventos e entretenimento"] = "entenda tipo de evento, data e necessidade e use somente serviços, agenda e condições cadastrados",
            ["Assistência técnica"] = "identifique equipamento, modelo e problema relatado, oriente a triagem cadastrada e não invente diagnóstico ou orçamento",
            ["Condomínios e administração"] = "identifique unidade, assunto administrativo, manutenção ou ocorrência e siga os canais e regras cadastrados",
            ["Agronegócio"] = "entenda cultura, operação, produto ou serviço buscado e evite recomendação técnica sem informação autorizada",
            ["Indústria e distribuição"] = "identifique produto, aplicação, volume e região e informe somente catálogo e condições cadastrados",
            ["ONG e projetos sociais"] = "entenda se o contato busca atendimento, participação, doação ou informação e siga os processos cadastrados"
        };

    private static readonly IReadOnlyDictionary<string, string> ToneGuides =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Profissional e objetivo"] = "seja direto, claro e profissional",
            ["Consultivo e acolhedor"] = "demonstre interesse, explique e faça uma pergunta útil por vez",
            ["Informal e próximo"] = "use linguagem simples, natural e cordial, sem excesso de intimidade",
            ["Premium e exclusivo"] = "use linguagem elegante, cuidadosa e personalizada, sem exageros",
            ["Didático e paciente"] = "explique em etapas curtas e confirme se o cliente compreendeu",
            ["Empático e humanizado"] = "reconheça a necessidade do cliente e responda com sensibilidade",
            ["Comercial e persuasivo"] = "destaque benefícios comprovados e convide ao próximo passo sem pressionar",
            ["Técnico e preciso"] = "use termos exatos e traduza o necessário para o nível do cliente",
            ["Jovem e descontraído"] = "seja leve e atual, mantendo respeito e clareza",
            ["Institucional e formal"] = "use linguagem impessoal, respeitosa e consistente",
            ["Ágil e direto"] = "priorize a resposta principal e o próximo passo em poucas palavras",
            ["Calmo e tranquilizador"] = "responda com serenidade, segurança e instruções simples"
        };

    public static string? Build(string? businessType, string? toneOfVoice)
    {
        var type = businessType?.Trim();
        var tone = toneOfVoice?.Trim();
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var guidance = BusinessGuides.TryGetValue(type, out var configured)
                ? configured
                : "entenda a intenção, explique o que estiver autorizado e conduza ao próximo passo cadastrado";
            parts.Add($"No segmento {type}, {guidance}.");
        }

        if (!string.IsNullOrWhiteSpace(tone))
        {
            var guidance = ToneGuides.TryGetValue(tone, out var configured)
                ? configured
                : $"mantenha o tom {tone} de forma natural";
            parts.Add($"Na conversa, {guidance}.");
        }

        if (parts.Count == 0)
            return null;

        parts.Add("Use este guia para personalizar perguntas genéricas e a condução, nunca como prova de preço, prazo, disponibilidade, política ou serviço não cadastrado.");
        return string.Join(' ', parts);
    }
}
