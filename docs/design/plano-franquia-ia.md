# Plano de implementação — franquia de respostas e recargas de IA

**Escopo:** enforcement real da franquia mensal de respostas por tenant, suspensão automática da IA e recarga de 500 respostas.

**Referências:** US-006, US-012, US-013, FR-044, FR-045, FR-047, FR-061, BR-AI-006, T196 e T210.

## Objetivo

Garantir que um tenant nunca consiga iniciar uma chamada de IA quando sua franquia efetiva estiver esgotada. A franquia será composta pelo limite-base do plano mais recargas válidas no mês UTC. O débito deverá ser reservado atomicamente antes da chamada ao provedor e confirmado somente quando uma resposta válida for criada para envio.

As diretrizes, o perfil do negócio, a base de conhecimento, a confiança, as filas, as tags e o handoff permanecem dados do tenant e continuam isolados por `TenantId`. Credenciais, provedor, modelo, tokens e custo permanecem sob controle do `PlatformAdmin`.

## Decisão de produto

- O limite-base é definido pelo plano e pode ser personalizado pelo `PlatformAdmin`.
- Cada recarga adiciona exatamente 500 respostas ao mês UTC corrente e nunca altera o limite-base.
- O TenantOwner pode visualizar saldo e enviar uma solicitação de recarga de 500 respostas.
- Somente o `PlatformAdmin` pode aprovar/liberar o crédito, registrar a recarga manual ou rejeitar a solicitação. Isso evita que o tenant aumente o próprio custo da plataforma sem autorização.
- A recarga aprovada libera a IA imediatamente; não reativa o tenant quando ele estiver suspenso por inadimplência.
- Respostas humanas, handoff, fallback, simulações, entradas e falhas do provedor não consomem respostas.
- O novo mês UTC inicia uma nova franquia. Recargas não utilizadas não transitam para o mês seguinte.

## Estado atual e lacunas

Já existem limite no `Tenant`, métricas no `UsageLedger`, política de cálculo, endpoint administrativo, dashboard e uma verificação no `AiOrchestrationWorker`. A lacuna principal é que a verificação de respostas ocorre antes da chamada, mas não reserva de forma atômica uma unidade de franquia. Dois workers concorrentes podem observar o mesmo saldo e ambos criar respostas.

Também é necessário separar claramente:

1. solicitação de recarga feita pelo TenantOwner;
2. aprovação e crédito efetivo feito pelo PlatformAdmin;
3. reserva pendente durante a chamada da IA;
4. consumo confirmado quando a resposta entra na Outbox.

## Desenho técnico

### Modelo de dados

Adicionar uma entidade `AiResponseQuotaReservation` ou equivalente persistente, sempre com `TenantId`:

- `Id`;
- `TenantId`;
- `PeriodStartUtc`;
- `SourceMessageId`;
- `IdempotencyKey` única por tenant;
- `Status`: `Pending`, `Committed` ou `Released`;
- `ReservedAt`, `CommittedAt`, `ReleasedAt`;
- `ReleaseReason` sanitizado;
- `RowVersion` para concorrência otimista.

Adicionar uma entidade de solicitação de recarga somente se a solicitação do TenantOwner for habilitada nesta entrega:

- `AiResponseTopUpRequestId`;
- `TenantId`;
- `Quantity`, sempre 500;
- `PeriodStartUtc`;
- `Status`: `Pending`, `Approved` ou `Rejected`;
- ator solicitante, ator aprovador e datas;
- chave idempotente;
- justificativa sanitizada, sem conteúdo de conversa.

O `UsageLedger` continuará sendo a fonte auditável do consumo confirmado e das recargas aprovadas. A reserva não deve ser contada como resposta consumida no dashboard; ela deve ser incluída no cálculo de saldo disponível enquanto estiver pendente.

### Reserva concorrente

Criar um serviço de aplicação, por exemplo `IAiResponseQuotaService`, com operações:

- `TryReserveAsync(tenantId, sourceMessageId, period, cancellationToken)`;
- `CommitAsync(reservationId, sourceMessageId, cancellationToken)`;
- `ReleaseAsync(reservationId, reason, cancellationToken)`;
- `GetSnapshotAsync(tenantId, period, cancellationToken)`.

`TryReserveAsync` deve executar uma transação PostgreSQL, obter lock transacional por tenant/período ou usar uma linha de controle versionada, somar consumo confirmado e reservas pendentes, e somente criar a reserva quando houver saldo. A operação deve ser idempotente para a mesma mensagem.

Fluxo obrigatório do worker:

```text
inbound elegível
  -> reserva de 1 resposta
  -> estima/reserva orçamento de tokens e custo
  -> chama provedor
  -> valida resposta e janela/mode/version
  -> cria mensagem Outbox + UsageLedger de resposta
  -> confirma reserva
```

Se a chamada falhar, produzir handoff ou perder a corrida de versão, a reserva deve ser liberada. Se a aplicação cair depois de reservar, um job de reconciliação deve liberar reservas antigas sem resposta válida. A confirmação da resposta, o `UsageLedger` e a Outbox devem estar na mesma transação.

### Regra de suspensão

Quando não houver saldo, o worker não chama nenhum provedor. Ele registra uma auditoria idempotente de quota esgotada, marca o inbound como processado e aplica o fallback/handoff seguro já existente, sem retry infinito.

O status calculado deve distinguir:

- `Normal`;
- `Warning` a partir de 80% do limite;
- `Exhausted` quando saldo confirmado menos reservas pendentes for zero;
- `Unlimited` quando o limite-base for nulo, caso permitido pela política comercial.

### Recarga de 500 respostas

Endpoints previstos:

- `GET /api/usage`: saldo e alertas para TenantOwner/Operator, sem tokens, provedor, modelo ou custo;
- `POST /api/tenant/ai-response-top-up-requests`: solicitação do TenantOwner, sempre 500;
- `GET /api/admin/tenants/{tenantId}/ai-response-top-up-requests`: fila administrativa;
- `POST /api/admin/tenants/{tenantId}/ai-response-topups`: aprovação/liberação idempotente pelo PlatformAdmin;
- `POST /api/admin/tenants/{tenantId}/ai-response-top-up-requests/{requestId}/reject`: rejeição auditada;
- `GET /api/admin/tenants/{tenantId}/ai-usage`: consumo técnico detalhado para PlatformAdmin.

Se a solicitação de recarga ficar fora do escopo inicial, o endpoint administrativo direto de recarga deve permanecer, mas a UI do tenant não pode apresentar uma ação que pareça liberar crédito automaticamente.

### Painel administrativo

Criar um menu dedicado de **Uso de IA e Franquias**, com:

- filtro por tenant e mês UTC;
- limite-base, recargas aprovadas, limite efetivo, respostas usadas, reservas pendentes e saldo;
- tokens de entrada/saída, total, provedor, modelo e custo estimado somente para PlatformAdmin;
- status da IA e motivo do bloqueio;
- histórico de recargas e solicitações;
- ação para aprovar uma solicitação ou adicionar exatamente 500 respostas;
- auditoria do ator, data, período e chave idempotente.

O TenantOwner verá apenas saldo, percentual, aviso de 80%, estado suspenso por franquia e botão de solicitação, se essa etapa for habilitada.

## Segurança e isolamento

- Toda entidade nova terá `TenantId` e filtro global quando aplicável.
- A reserva sempre será criada para o tenant da mensagem, nunca para o tenant inferido do usuário do painel.
- Endpoints administrativos validarão `PlatformAdmin`; solicitação de recarga validará `TenantOwner` do tenant corrente.
- A chave idempotente será armazenada como identificador operacional, sem segredo.
- Logs e auditoria não conterão prompt, resposta completa, telefone ou dados pessoais.
- A credencial do provedor será resolvida somente depois de a reserva de resposta e o orçamento técnico serem aprovados.

## Ordem de implementação

1. Especificar estados, período UTC, política de saldo e contratos de resposta.
2. Criar migration e configurações das entidades de reserva e solicitação, com índices e unicidades.
3. Implementar `IAiResponseQuotaService` e a operação transacional/idempotente de reserva.
4. Integrar reserva, confirmação e liberação ao `AiOrchestrationWorker`, preservando a reserva de tokens/custo existente.
5. Implementar solicitação, aprovação, rejeição e histórico de recarga.
6. Ajustar endpoints de uso e autenticação para expor somente os campos permitidos por papel.
7. Criar o menu administrativo e os estados de saldo/suspensão no painel do tenant.
8. Adicionar reconciliação de reservas pendentes e métricas operacionais.
9. Executar testes de concorrência, idempotência, isolamento e fluxo completo.
10. Atualizar documentação operacional, migration runbook e critérios de implantação.

## Tarefas propostas

- **T212** — Fechar contrato da quota, estados da reserva, período UTC e regras de consumo. **Refs:** FR-044, FR-045, FR-047.
- **T213** — Criar entidades, configurações, índices, unicidades e migration reversível para reservas e solicitações de recarga. **Refs:** FR-044, FR-045, FR-061.
- **T214** — Implementar `IAiResponseQuotaService` com reserva atômica por tenant/período e idempotência por mensagem. **Refs:** FR-045, BR-AI-006.
- **T215** — Integrar reserva/commit/release ao worker, incluindo falhas, handoff, resposta inválida, timeout e concorrência de versão. **Refs:** FR-045, FR-061.
- **T216** — Implementar reconciliação de reservas pendentes e métricas/alertas operacionais. **Refs:** FR-045, NFR-009.
- **T217** — Implementar solicitação do TenantOwner e aprovação/rejeição administrativa de recarga de exatamente 500 respostas. **Refs:** US-012, US-013, FR-044, FR-019.
- **T218** — Ajustar contratos de uso, `/auth/me` e endpoints administrativos por papel, sem expor dados técnicos ao tenant. **Refs:** US-006, FR-047, FR-054.
- **T219** — Criar o menu administrativo de Uso de IA e o painel de saldo/solicitação do tenant. **Refs:** US-006, US-013, FR-047.
- **T220** — Cobrir unidade, PostgreSQL, concorrência, idempotência, isolamento, autorização, falhas e migration. **Refs:** SC-004, SC-005, FR-044, FR-045.
- **T221** — Atualizar runbook de operação, recarga, suspensão, virada mensal e diagnóstico. **Refs:** BR-AI-006, NFR-009.

## Critérios de aceite

- Com limite 1 e duas mensagens concorrentes, no máximo uma resposta de IA é criada/enfileirada.
- Uma falha do provedor não consome a resposta reservada.
- Handoff, fallback e resposta inválida liberam a reserva.
- Ao esgotar o saldo, nenhuma chamada externa ao provedor é iniciada.
- Uma recarga repetida com a mesma chave não duplica 500 respostas.
- Uma recarga aprovada aumenta o saldo em exatamente 500 no mês corrente.
- A virada do mês não carrega recargas antigas.
- Nenhum tenant acessa saldo, consumo ou solicitação de outro tenant.
- TenantOwner não visualiza tokens, custo, provedor ou modelo.
- PlatformAdmin consegue auditar quem aprovou, quando, quanto e para qual período.

## Testes planejados

- unitários da política de saldo e transições da reserva;
- integração PostgreSQL para unicidade e isolamento por tenant;
- concorrência com múltiplos workers para provar o limite;
- idempotência de reserva, commit, release e recarga;
- falha do provedor, timeout, handoff e perda de versão;
- contrato dos endpoints por papel;
- teste frontend do menu administrativo e aviso de suspensão;
- teste de reconciliação de reservas antigas;
- teste de migration `Up`/`Down` e execução em banco candidato de produção.

## Riscos e decisões operacionais

- A aprovação manual do PlatformAdmin é necessária para manter o controle financeiro da plataforma. Se o produto futuramente vender recargas automaticamente, deverá ser criado um fluxo de pagamento separado; não se deve liberar crédito com base apenas em um botão do tenant.
- O bloqueio é somente da IA. O tenant continua podendo receber mensagens, consultar histórico e operar manualmente, respeitando a suspensão financeira geral do tenant.
- O uso de PostgreSQL é obrigatório para a garantia de concorrência; o teste SQLite não é suficiente para validar locks e isolamento do runtime.
- O job de reconciliação deve ter alerta quando houver reservas pendentes acima do tempo normal de uma chamada ao provedor.
