# Estratégia de testes

## Camadas

| Camada | Foco | Exemplos |
|---|---|---|
| unitário | regras puras e casos de uso | janela, modo, versão, retry, política de IA |
| arquitetura | dependências e convenções | Domain sem Infrastructure; endpoints finos |
| integração | PostgreSQL/EF/API/adaptadores fake | migrations, tenant, Inbox/Outbox, auth |
| contrato | payloads Meta/OpenAI e OpenAPI | webhooks conhecidos/desconhecidos, schema da IA |
| frontend | componentes e estado | inbox, bloqueios, erro/loading, conhecimento |
| E2E | jornadas críticas | login → receber → assumir → responder |
| avaliação IA | qualidade e segurança | factualidade, handoff, injection, custo |
| carga/resiliência | SLO e falhas parciais | replay, timeout, fila, reconexão SignalR |

## Matriz mínima por requisito crítico

- **FR-005–007:** assinatura inválida, payload grande, duplicado e ordem diferente.
- **FR-009/NFR-006:** conexão de A não recebe evento de B.
- **FR-011/BR-004/010:** humano assume durante geração e vence sempre.
- **FR-012:** bordas exatas antes/no/depois de 24 horas com relógio controlado.
- **FR-014/015:** JSON inválido, texto longo, decisão desconhecida e mudança de versão.
- **FR-004/NFR-008:** segredos ausentes de banco em claro, logs e respostas.
- **NFR-009:** timeout após envio, retry e webhook tardio sem duplicar.
- **FR-001:** login público exige antiforgery; mutação sem `X-CSRF-TOKEN`, com token inválido ou de outra sessão falha.
- **FR-003/US-001:** PlatformAdmin cria, suspende e reativa tenant; suspensão preserva histórico e bloqueia operações.
- **FR-005/FR-022/BR-011:** assinatura usa app secret global antes de resolver `phone_number_id`; desconhecido fica cifrado/quarentenado e pode ser consultado/reprocessado com auditoria.
- **FR-017:** update/desativação exige `If-Match`; versão obsoleta falha e não há delete físico.
- **FR-018/NFR-006:** tenants distintos aceitam o mesmo `(provider, metric, source_id)` sem colisão; duplicata no mesmo tenant falha.
- **FR-019:** identidade da aplicação não consegue atualizar ou apagar `AuditLog`.
- **FR-023/NFR-006:** download de mídia exige sessão/tenant correto e nunca retorna token ou URL privada da Meta.
- **NFR-003:** no mínimo 100 decisões elegíveis, com p95 abaixo de 10 s e tempos de fila/aplicação/provedor separados.
- **NFR-004:** relatório mensal calcula respostas elegíveis concluídas sem 5xx/timeout da plataforma sobre requisições válidas recebidas, sem excluir manutenção ou remover falhas Meta/OpenAI do total.
- **NFR-005:** restore usa ponto de no máximo 24 h e conclui smoke test aprovado em até 4 h da declaração.

## Ambientes

Testcontainers fornece PostgreSQL real por suíte de integração. SDKs externos ficam atrás de servidores HTTP fake; sandbox/contas de teste só entram em smoke tests opt-in. Nunca executar testes destrutivos em conta de cliente.

### Estação local autorizada

Em 2026-08-29 foi confirmado que a estação de desenvolvimento Windows possui Docker Desktop e Docker Compose instalados, e que o responsável possui permissão administrativa para executar os testes locais em containers. Antes da suíte de integração, confirmar que o Docker Engine está em execução. Essa autorização cobre apenas containers locais de teste e não autoriza operações destrutivas em bancos ou ambientes de clientes.

## Gates

PR exige lint/build, unitários, arquitetura e integrações afetadas. Merge para piloto exige E2E crítico e avaliações da IA. Deploy exige migration ensaiada, smoke test e plano de rollback.

Cobertura numérica não substitui cenários. Regras críticas precisam de branch coverage útil; código gerado e DTOs não elevam meta artificialmente.
