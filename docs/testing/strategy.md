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

## Ambientes

Testcontainers fornece PostgreSQL real por suíte de integração. SDKs externos ficam atrás de servidores HTTP fake; sandbox/contas de teste só entram em smoke tests opt-in. Nunca executar testes destrutivos em conta de cliente.

## Gates

PR exige lint/build, unitários, arquitetura e integrações afetadas. Merge para piloto exige E2E crítico e avaliações da IA. Deploy exige migration ensaiada, smoke test e plano de rollback.

Cobertura numérica não substitui cenários. Regras críticas precisam de branch coverage útil; código gerado e DTOs não elevam meta artificialmente.
