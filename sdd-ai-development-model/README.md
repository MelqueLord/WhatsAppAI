# Modelo de Desenvolvimento com IA e SDD

Este kit inicia um projeto com desenvolvimento orientado por especificação (SDD) e uso disciplinado de agentes de IA. Ele não contém regras de negócio, fornecedores, tecnologias ou dados de um produto específico.

## Como aplicar em outro projeto

1. Copie o conteúdo desta pasta para a raiz do novo repositório.
2. Antes do primeiro desenvolvimento, adapte `AGENTS.md` e `.specify/memory/constitution.md` às decisões reais do projeto.
3. Preencha `ROADMAP.md` com os marcos do produto.
4. Registre o contexto existente em `docs/contexto/` e as regras permanentes em `docs/regras/`.
5. Para cada capacidade, crie a especificação, o plano e as tarefas antes de alterar código.

Os diretórios `.specify/` e `.agents/skills/` trazem os fluxos e as skills reutilizáveis para assistentes compatíveis com Spec Kit. Eles podem ser mantidos, adaptados ou removidos conforme a ferramenta de IA escolhida.

## Fluxo de uma capacidade

1. **Especificar**: descreva o problema, usuários, regras, critérios de aceitação e medidas de sucesso em `docs/specs/`.
2. **Esclarecer**: elimine ambiguidades que bloqueiam decisões de produto, segurança ou arquitetura.
3. **Planejar**: defina o desenho técnico em `docs/design/` e crie ADRs para decisões estruturais.
4. **Quebrar em tarefas**: registre tarefas pequenas, ordenadas e rastreáveis em `docs/tasks/`.
5. **Implementar**: execute uma tarefa por vez, com testes proporcionais ao risco.
6. **Verificar**: formate, compile, execute os testes relevantes e revise o diff.
7. **Convergir**: compare código, especificação, plano e tarefas; registre ou complete o que faltar.

## Uso com IA

Ao iniciar uma solicitação, o agente deve primeiro ler a constituição, a especificação, o plano, as tarefas e os ADRs relacionados. Antes de editar, deve informar a intenção, os arquivos previstos e como verificará a mudança. Ao encerrar, deve apresentar o que mudou, a validação feita e riscos restantes.

Use as skills incluídas nesta ordem quando estiverem disponíveis: `speckit-specify`, `speckit-clarify`, `speckit-plan`, `speckit-tasks`, `speckit-analyze`, `speckit-implement` e `speckit-converge`.

## Estrutura

```text
AGENTS.md                       Regras operacionais para pessoas e agentes
ROADMAP.md                      Marcos e direção do produto
.specify/                       Templates, scripts e fluxos SDD
.agents/skills/                 Skills reutilizáveis do fluxo Spec Kit
docs/contexto/                  Contexto atual por domínio
docs/regras/                    Regras permanentes de arquitetura e qualidade
docs/specs/                     Especificações de capacidades
docs/design/                    Planos técnicos
docs/tasks/                     Tarefas executáveis
docs/decisoes/                  ADRs
docs/pesquisa/                  Evidências e pesquisas
```

O kit começa propositalmente neutro. A constituição e os documentos de regras devem se tornar a fonte de verdade do novo projeto antes que o código seja escrito.
