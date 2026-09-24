# Regras do repositório

## Fonte de verdade

Antes de alterar código, leia nesta ordem:

1. `.specify/memory/constitution.md`;
2. `docs/specs/plataforma/especificacao-plataforma.md`;
3. `docs/design/plano-plataforma.md`;
4. `docs/tasks/plataforma.md`;
5. ADRs e documentos especializados afetados.

Se código e especificação divergirem, interrompa a implementação e proponha a correção explícita de um deles. Não altere silenciosamente a intenção do produto.

## Estrutura de documentação SDD

- `ROADMAP.md` na raiz organiza os marcos do produto.
- `docs/contexto/` oferece contexto por domínio antes de uma alteração.
- `docs/regras/` consolida regras de arquitetura, qualidade, segurança, tenancy e operação.
- `docs/skills/` documenta fluxos repetíveis de pesquisa, planejamento, revisão, testes e investigação; skills executáveis continuam em `.agents/skills/`.
- `docs/specs/`, `docs/design/`, `docs/tasks/`, `docs/decisoes/` e `docs/pesquisa/` organizam a navegação por capacidade.
- Os templates do Spec Kit permanecem em `.specify/`; os artefatos canônicos e os ADRs permanecem em `docs/`.
- Ao alterar comportamento, atualizar o documento SDD do domínio afetado quando ele ajudar a explicar a mudança; ele nunca substitui requisito, plano, tarefa ou ADR canônico.

## Modo de trabalho

- Execute uma tarefa identificada por vez, respeitando dependências.
- Antes de editar, descreva a intenção, os arquivos afetados e os testes esperados.
- Preserve rastreabilidade: commits e PRs devem mencionar IDs como `US-001`, `FR-012` e `T042`.
- Inspecione o código real antes de citar classes, endpoints ou configurações.
- Ao terminar um incremento: formate, compile, execute os testes relevantes, revise o diff e registre riscos restantes.
- Não introduza dependência, serviço externo ou abstração sem necessidade demonstrada pela especificação.
- Decisões estruturais novas exigem ADR.

## Limites técnicos

- Backend: .NET 10 LTS, ASP.NET Core, EF Core e SignalR.
- Frontend: React 19.2, TypeScript e Vite.
- Persistência: PostgreSQL via Npgsql; Supabase gerenciado ou Docker em produção própria.
- Organização: monólito modular com separação leve entre Domain, Application, Infrastructure e WebApi.
- Integrações: WhatsApp Cloud API oficial e OpenAI Responses API atrás de interfaces próprias.
- Assíncrono: padrões Inbox/Outbox e worker durável apoiado no PostgreSQL.
- Não adicionar ao núcleo do MVP: n8n, microsserviços, RabbitMQ, Redis ou Kubernetes.

## Segurança obrigatória

- Toda entidade de negócio pertencente a cliente deve carregar `TenantId`.
- Toda consulta autenticada deve ser limitada ao tenant corrente.
- Nunca registrar tokens, chaves, conteúdo completo de prompts ou dados pessoais sem mascaramento definido.
- Segredos devem passar por `ISecretStore`; nunca persistir texto puro.
- Validar autenticidade, idempotência e origem dos webhooks.
- O backend decide se uma resposta pode ser enviada; o modelo de IA nunca chama a Meta diretamente.
- Bloquear texto livre fora da janela de atendimento de 24 horas no MVP.

## Qualidade

- Código de domínio e aplicação não depende de SDKs da Meta ou OpenAI.
- Regras críticas exigem testes unitários; integrações exigem testes de contrato/integração.
- Mudanças de banco devem incluir migration reversível e teste de isolamento por tenant.
- Trate warnings novos como falhas no código do projeto.
- O idioma do código é inglês; documentação de produto pode permanecer em português.
