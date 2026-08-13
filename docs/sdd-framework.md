# Framework SDD e skills para Codex

## Recomendação

Adotar **GitHub Spec Kit** como framework principal de Spec-Driven Development e usar as convenções nativas do Codex:

- `AGENTS.md` para regras permanentes e contexto do repositório;
- `.agents/skills/` para skills locais, pequenas e acionáveis;
- Spec Kit para o ciclo `constitution → specify → plan → tasks → implement`.

O pacote atual já segue os artefatos do Spec Kit. Depois de revisar/commitar estes documentos, a CLI pode ser instalada e inicializada em uma branch de teste. Antes disso, preserve os arquivos existentes e confira o diff para evitar sobrescrita.

Exemplo a confirmar com a versão instalada:

```bash
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git@vX.Y.Z
specify init --here --integration codex
```

Substitua `vX.Y.Z` pela release estável atual e rode `specify integration list` antes da inicialização. A integração `codex` instala as skills `speckit-*` em `.agents/skills`.

## Skills recomendadas no repositório

Não criar uma skill grande que replique toda a documentação. Criar somente quando o fluxo já foi executado manualmente e mostrou repetição.

| Skill proposta | Quando usar | Responsabilidade |
|---|---|---|
| `waai-implement-task` | ao implementar `Txxx` | carregar spec/plan/tarefa, checar dependências, executar uma fatia e validar gates |
| `waai-review-tenant-isolation` | em mudança de dados/auth/SignalR/jobs | checklist e testes negativos de isolamento |
| `waai-test-webhook-fixture` | ao adicionar evento Meta | anonimizar fixture, verificar assinatura/parser/idempotência |
| `waai-evaluate-ai-policy` | ao mudar modelo/prompt/conhecimento | executar dataset de avaliação e comparar qualidade/custo/handoff |
| `waai-create-adr` | diante de decisão estrutural | gerar ADR curto, atualizar documentos afetados e registrar consequências |
| `waai-pilot-readiness` | antes de deploy piloto | reunir segurança, backup, métricas, smoke e rollback |

Cada `SKILL.md` deve ser curto, com instruções essenciais; checklists extensos e exemplos ficam em `references/`, scripts determinísticos em `scripts/`. Isso segue o princípio de divulgação progressiva e reduz contexto desnecessário.

## O que não instalar inicialmente

- Um segundo framework de processo completo em paralelo.
- Skills genéricas de “criar CRUD” que ignoram requisitos e tenancy.
- Skills que armazenem credenciais ou façam deploy sem gate humano.
- n8n como substituto do workflow de desenvolvimento ou da lógica central.

## Alternativa

**Superpowers** é boa opção se a prioridade for um método geral de brainstorming, planos, TDD e revisão. Para este projeto, o Spec Kit é mais alinhado porque o pedido é preparar e manter um SDD rastreável. Avaliar Superpowers após o primeiro marco, escolhendo skills complementares e evitando duplicar gates.

## Fluxo diário sugerido

1. Selecionar uma tarefa desbloqueada em `tasks.md`.
2. Confirmar requisitos/aceites e documentos afetados.
3. Pedir ao Codex um plano curto daquele incremento.
4. Implementar, compilar, testar e revisar o diff.
5. Atualizar contrato/ADR/runbook quando a realidade mudar.
6. Commitar com IDs rastreáveis e somente então seguir à próxima tarefa.

## Referências oficiais

- GitHub Spec Kit: <https://github.com/github/spec-kit>
- Integrações suportadas: <https://github.com/github/spec-kit/blob/main/docs/reference/integrations.md>
- Skills no Codex: <https://learn.chatgpt.com/docs/build-skills>
- Instruções `AGENTS.md`: <https://learn.chatgpt.com/docs/agent-configuration/agents-md>
