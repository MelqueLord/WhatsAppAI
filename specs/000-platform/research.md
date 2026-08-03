# Pesquisa e decisões técnicas

## Resumo

As decisões abaixo privilegiam suporte de longo prazo, baixo custo operacional e independência entre o domínio e provedores externos.

## R-001 — .NET 10 LTS

O .NET 10 é a linha LTS ativa e tem horizonte de suporte maior que versões STS anteriores. Um produto novo não deve começar em .NET 9 perto do fim de manutenção. Fixar SDK por `global.json` no bootstrap.

## R-002 — React 19.2 com TypeScript e Vite

React 19.2 é a linha estável atual. O produto precisa de uma SPA operacional, não de renderização pública orientada a SEO; Vite reduz configuração e evita adotar um framework full-stack desnecessário.

## R-003 — PostgreSQL 18

PostgreSQL cobre transações, JSONB, índices, auditoria e a carga inicial de Inbox/Outbox. Redis e broker só serão avaliados quando métricas demonstrarem contenção, latência ou volume incompatível.

## R-004 — OpenAI Responses API e modelo configurável

O adaptador utiliza Responses API e saída estruturada. O nome do modelo não aparece no domínio e é configurável por tenant/ambiente. Para atendimento textual de baixo custo, a configuração inicial proposta é `gpt-5.6-luna`, sujeita a disponibilidade, preço e avaliação antes do piloto.

## R-005 — Conhecimento sem vetor no MVP

Itens curtos e ativos são selecionados por categoria/limite e inseridos no contexto. Isso é suficiente para validar o produto. Busca vetorial exige ingestão, chunking, avaliação e operação adicionais; fica condicionada a volume e qualidade observados.

## R-006 — Sem n8n no núcleo

Webhooks, filas, política e estado são regras centrais e precisam de testes, versionamento e atomicidade. n8n pode ser usado futuramente em automações periféricas, como notificação interna, desde que a aplicação continue funcionando sem ele.

## R-007 — Spec Kit + skills nativas do Codex

GitHub Spec Kit é o framework SDD recomendado porque organiza o fluxo em constituição, especificação, plano, tarefas e implementação, e oferece integração baseada em skills para o Codex. `AGENTS.md` mantém regras permanentes do repositório; skills focadas entram apenas quando um fluxo repetido justificar automação.

Superpowers é uma alternativa útil para disciplina geral de brainstorming, planos e TDD. Não deve ser combinado no início com o fluxo inteiro do Spec Kit, pois comandos e gates sobrepostos aumentariam contexto e ambiguidade. Reavaliar depois do primeiro ciclo.

## R-008 — Credenciais e cobrança pertencem ao cliente

BYOK separa consumo do SaaS e torna as faturas oficiais verificáveis. A plataforma guarda referências/segredos de forma protegida e exibe estimativas; não faz markup nem garante equivalência com a fatura.

## R-009 — Janela de serviço e marketing

O MVP responde somente a mensagens iniciadas pelo consumidor e bloqueia texto livre após a janela de 24 horas. Templates proativos, campanhas e classificação comercial ficam fora do produto inicial, reduzindo risco de política e complexidade de cobrança.

## Critérios para reavaliar arquitetura

Abrir ADR, com métricas, antes de introduzir:

- Redis: fan-out/estado em tempo real não atendido pela implantação atual.
- Broker: backlog ou vazão não suportados com segurança pelo worker/PostgreSQL.
- Microsserviço: necessidade independente de escala, segurança, equipe ou deploy.
- pgvector/RAG: conhecimento excede contexto controlado ou avaliação mostra ganho material.
- n8n: integração periférica solicitada por múltiplos clientes e sem regra transacional central.

## Fontes oficiais consultadas

- Política de suporte .NET: <https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core>
- Versões do React: <https://react.dev/versions>
- Política de versões PostgreSQL: <https://www.postgresql.org/support/versioning/>
- GitHub Spec Kit: <https://github.com/github/spec-kit>
- Skills no Codex: <https://learn.chatgpt.com/docs/build-skills>
