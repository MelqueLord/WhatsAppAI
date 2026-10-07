# ADR-0017: Remover consentimento por contato como bloqueio do produto

**Status:** Aceito — 2026-10-06

## Contexto

O produto solicitava e persistia consentimento de Marketing por contato, bloqueava destinatários sem evidência no dispatch/worker e condicionava memória individual à finalidade padrão de atendimento por IA. Isso criava confirmações dentro do aplicativo e impedia o operador de usar essas funcionalidades quando a evidência não estava cadastrada.

## Decisão

- Remover do aplicativo a coleta e a exigência de evidência por contato para disparo de Marketing e uso de memória individual.
- Incluir os contatos selecionados em disparos de Marketing sem consultar `ConsentEvidence`; a memória individual é gerenciada manualmente, com validade e escopo de tenant/contato.
- Não criar uma finalidade padrão de IA com base legal `Consent` durante o provisionamento. Cada tenant configura finalidades e bases legais pertinentes nos controles gerais de privacidade.
- Preservar os controles genéricos de finalidade, evidência, revogação e solicitações de titulares. Evidências históricas permanecem auditáveis, mas não condicionam esses dois fluxos.
- O tenant continua responsável por obter a aceitação explícita para mensagens no WhatsApp e respeitar solicitações para bloquear, interromper ou cancelar comunicações, conforme a [Política de Mensagens do WhatsApp Business](https://business.whatsapp.com/policy/preview?lang=pt_BR).

## Consequências

- Disparos de Marketing não são filtrados pela evidência de consentimento armazenada pelo aplicativo.
- Memórias individuais podem ser registradas sem evidência de consentimento, mas continuam manuais, sanitizadas, limitadas a 365 dias, auditadas e removidas do contexto ao expirar, desativar ou anonimizar o contato.
- A migration torna opcional a coluna histórica de evidência de memória e remove suas relações de execução sem apagar os valores antigos.
- Os estados de privacidade genéricos continuam disponíveis para finalidades cuja base legal configurada seja consentimento.
