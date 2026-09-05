# Pesquisa e decisões técnicas

## Resumo

As decisões abaixo privilegiam suporte de longo prazo, baixo custo operacional e independência entre o domínio e provedores externos.

## R-001 — .NET 10 LTS

O .NET 10 é a linha LTS ativa e tem horizonte de suporte maior que versões STS anteriores. Um produto novo não deve começar em .NET 9 perto do fim de manutenção. Fixar SDK por `global.json` no bootstrap.

## R-002 — React 19.2 com TypeScript e Vite

React 19.2 é a linha estável atual. O produto precisa de uma SPA operacional, não de renderização pública orientada a SEO; Vite reduz configuração e evita adotar um framework full-stack desnecessário.

## R-003 — PostgreSQL 17

PostgreSQL cobre transações, JSONB, índices, auditoria e a carga inicial de Inbox/Outbox. Supabase atende aos ambientes gerenciados e a imagem oficial PostgreSQL atende à produção própria. Redis e broker só serão avaliados quando métricas demonstrarem contenção, latência ou volume incompatível.

## R-004 — OpenAI Responses API e modelo configurável

O adaptador utiliza Responses API e saída estruturada. O nome do modelo não aparece no domínio e é configurável por tenant/ambiente. Nenhum modelo é promovido por suposição: a escolha inicial e toda alteração passam pelo conjunto de avaliações de `docs/ai/behavior-policy.md`, comparando qualidade, handoff, segurança, p95 e custo. O resultado, a versão escolhida e os critérios de rollback ficam registrados antes do piloto.

## R-005 — Conhecimento sem vetor no MVP

Itens curtos e ativos são selecionados por categoria/limite e inseridos no contexto. Isso é suficiente para validar o produto. Busca vetorial exige ingestão, chunking, avaliação e operação adicionais; fica condicionada a volume e qualidade observados.

A seleção local pode combinar correspondência lexical, conceitos equivalentes, intenção, categoria e tolerância a pequenas variações de escrita. Esse aprimoramento não cria embeddings, índice vetorial, nova persistência ou dependência operacional e preserva a decisão de manter RAG vetorial fora do MVP.

## R-006 — Sem n8n no núcleo

Webhooks, filas, política e estado são regras centrais e precisam de testes, versionamento e atomicidade. n8n pode ser usado futuramente em automações periféricas, como notificação interna, desde que a aplicação continue funcionando sem ele.

## R-007 — Spec Kit + skills nativas do Codex

GitHub Spec Kit é o framework SDD recomendado porque organiza o fluxo em constituição, especificação, plano, tarefas e implementação, e oferece integração baseada em skills para o Codex. `AGENTS.md` mantém regras permanentes do repositório; skills focadas entram apenas quando um fluxo repetido justificar automação.

Superpowers é uma alternativa útil para disciplina geral de brainstorming, planos e TDD. Não deve ser combinado no início com o fluxo inteiro do Spec Kit, pois comandos e gates sobrepostos aumentariam contexto e ambiguidade. Reavaliar depois do primeiro ciclo.

## R-008 — Credenciais e cobrança pertencem ao cliente

BYOK separa consumo do SaaS e torna as faturas oficiais verificáveis. A plataforma guarda referências/segredos de forma protegida e exibe estimativas; não faz markup nem garante equivalência com a fatura.

## R-009 — Janela de serviço e marketing

O MVP responde somente a mensagens iniciadas pelo consumidor e bloqueia texto livre após a janela de 24 horas. Templates transacionais aprovados pela Meta podem ser enviados pelo operador somente na API Oficial; templates proativos, campanhas e classificação comercial ficam fora do produto inicial.

## R-010 — Meta App compartilhado e contas dos tenants

A plataforma utiliza um único Meta App para linhas Cloud. Seu `app_secret` e verify token são segredos globais da plataforma no `ISecretStore`; o primeiro valida `X-Hub-Signature-256` antes de qualquer resolução de tenant e o segundo valida o challenge GET. Após a autenticidade do POST ser comprovada, `phone_number_id` resolve a `WhatsAppAccount`. Linhas QR usam sessões Baileys isoladas por tenant/linha e segredo próprio para a ponte. WABA, `phone_number_id`, token de acesso e faturamento Cloud continuam pertencendo a cada tenant. A decisão especializa ADR-0002, ADR-0003 e ADR-0009.

## ADRs aceitos reutilizados

- `docs/architecture/adr/0001-modular-monolith.md` — monólito modular.
- `docs/architecture/adr/0002-official-whatsapp-cloud-api.md` — canal Cloud oficial da Meta.
- `docs/architecture/adr/0009-baileys-production-qr.md` — ponte Baileys para conexões QR em produção.
- `docs/architecture/adr/0003-customer-owned-provider-billing.md` — contas e faturamento dos tenants.
- `docs/architecture/adr/0004-no-n8n-core.md` — n8n fora do núcleo.
- `docs/architecture/adr/0005-postgres-inbox-outbox.md` — filas duráveis em PostgreSQL.
- `docs/architecture/adr/0008-postgresql-only.md` — PostgreSQL único, Supabase e Docker.

## Critérios para reavaliar arquitetura

Abrir ADR, com métricas, antes de introduzir:

- Redis: fan-out/estado em tempo real não atendido pela implantação atual.
- Broker: backlog ou vazão não suportados com segurança pelo worker/PostgreSQL.
- Microsserviço: necessidade independente de escala, segurança, equipe ou deploy.
- RAG vetorial: conhecimento excede contexto controlado ou avaliação mostra ganho material.
- n8n: integração periférica solicitada por múltiplos clientes e sem regra transacional central.

## Fontes oficiais consultadas

- Política de suporte .NET: <https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core>
- Versões do React: <https://react.dev/versions>
- Documentação PostgreSQL: <https://www.postgresql.org/docs/>
- GitHub Spec Kit: <https://github.com/github/spec-kit>
- Skills no Codex: <https://learn.chatgpt.com/docs/build-skills>
