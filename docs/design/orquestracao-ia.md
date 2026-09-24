# Design: orquestração de IA

Worker aplica gates de tenant, modo, janela, finalidade, quota e configuração; monta contexto autorizado; chama provedor; valida resposta; revalida versão e takeover; e só então cria Outbox. Fallback e handoff evitam loops e decisões inseguras.

Fonte: [política de IA](../regras/inteligencia-artificial.md) e [spec de resposta por IA](../specs/ia/responder-com-ia.md).
