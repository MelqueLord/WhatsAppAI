# Retomada — controle de franquia de IA

**Estado em:** 2026-08-29  
**Branch:** `master`  
**Último commit de código:** `840a5dc`

## O que já está implementado

- Cada tenant possui `MonthlyAiResponseLimit`; `null` mantém compatibilidade ilimitada.
- O `UsageLedger` é a fonte única de consumo, com métrica `ai_responses`.
- Apenas respostas válidas da IA criadas na outbox consomem uma unidade.
- A reserva final usa lock transacional por tenant e revalida conversa, janela, modo e limite antes da criação.
- Limite atingido finaliza o inbound e aplica o handoff/fallback seguro, sem loop de reprocessamento.
- STAR, FLOW e SCALA aplicam padrões comerciais; franquia personalizada é preservada ao trocar de plano.
- API de uso retorna limite, consumo, restante, percentual e status (`normal`, `warning`, `exhausted`, `unlimited`).
- Tela do tenant mostra franquia e histórico recente de alertas.
- Tela administrativa mostra resumo, filtro por status e histórico por empresa.
- Alertas de 80% e esgotamento são registrados no `AuditLog`, idempotentes por tenant/mês.
- O backend é a fonte única do status; as telas não recalculam o limiar.
- Alterações de plano/franquia registram auditoria com versão e valores, sem segredos.

## Commits do incremento

`97bc2c7`, `433e6cb`, `696f533`, `e2440d8`, `5f50d62`, `6931960`, `cca92ad`, `df67033`, `dc8e7db`, `78642ad`, `840a5dc`.

Todos foram enviados para `origin/master`.

## Validação executada

- Build da solução .NET: aprovado, sem warnings novos.
- Build web: aprovado; permanecem apenas avisos de tamanho de bundle e anotação externa do SignalR.
- Testes web: 15 aprovados.
- Testes de quota/políticas: aprovados.
- Teste Docker/Testcontainers de isolamento do `UsageLedger`: aprovado.
- Nenhuma migration foi necessária depois da migration comercial já existente.

## Como continuar em outro chat

Leia este arquivo, `docs/runbooks/implemented-flows.md`, `docs/architecture/adr/0010-platform-managed-ai-allowances.md` e as seções US-012/FR-044/FR-045 de `specs/000-platform/spec.md` antes de alterar código.

Não reimplemente a contagem. Use `IUsageLedgerRepository`, `UsageMetricNames.AiResponses` e `AiQuotaAlertPolicy`.

Próximas melhorias possíveis, ainda fora deste incremento:

- tornar o toggle de IA atomicamente concorrente com `If-Match`;
- unificar gravação de modo e `HandoffEvent` na mesma transação;
- adicionar faturamento/overage, se o produto sair do escopo MVP;
- evoluir o RAG lexical para recuperação vetorial somente com nova decisão de arquitetura.

Prompt sugerido para retomada: “Leia `docs/runbooks/ai-quota-continuation.md` e revise somente as lacunas ainda abertas, sem duplicar a regra de franquia nem alterar o que já está validado.”
