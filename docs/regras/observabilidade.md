# Regras: observabilidade

- Propagar correlation ID em chamadas internas e externas.
- Registrar somente metadados sanitizados, incluindo tenant, operação, latência e resultado.
- Monitorar saúde, filas, erro, latência de IA e capacidade.
- Atualizar runbooks e alertas quando a mudança alterar operação ou falhas possíveis.
- Liveness mede somente o processo; readiness precisa verificar as dependências essenciais rotuladas como `ready`, incluindo PostgreSQL.
- A sanitização é uma barreira antes dos sinks de log e deve ser provada com testes que inspecionam o evento final; não basta declarar um enricher no contêiner de DI.

Correções abertas: [CR-004 e CR-005](../specs/producao/seguranca-e-prontidao.md).

Fonte: [runbook de observabilidade](../specs/producao/observabilidade.md) e [resposta a incidentes](../tasks/resposta-incidentes.md).
