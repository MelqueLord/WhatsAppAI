# Regras do repositório

## Fonte de verdade

Antes de alterar código, leia nesta ordem:

1. `.specify/memory/constitution.md`;
2. contexto e regras do domínio afetado em `docs/`;
3. especificação da capacidade em `docs/specs/`;
4. plano técnico correspondente em `docs/design/`;
5. tarefas em `docs/tasks/`;
6. ADRs e documentos especializados relacionados.

Se código e especificação divergirem, interrompa a implementação e proponha a correção explícita de um deles. Não altere silenciosamente a intenção do produto.

## Estrutura de documentação SDD

- `ROADMAP.md` organiza os marcos do produto.
- `docs/contexto/` explica o estado e os limites de cada domínio.
- `docs/regras/` consolida regras duráveis de arquitetura, qualidade, segurança e operação.
- `docs/specs/`, `docs/design/` e `docs/tasks/` mantêm a cadeia requisito → desenho → execução.
- `docs/decisoes/` guarda ADRs para decisões estruturais.
- `docs/pesquisa/` preserva evidências que sustentam decisões.
- Os templates e fluxos reutilizáveis permanecem em `.specify/`.

Ao alterar comportamento, atualize o documento que ajuda a explicar a mudança; ele não substitui a especificação, o plano, a tarefa ou o ADR canônico.

## Modo de trabalho

- Execute uma tarefa identificada por vez, respeitando dependências.
- Antes de editar, descreva a intenção, os arquivos afetados e os testes esperados.
- Preserve rastreabilidade: requisitos, decisões, tarefas, commits e revisões devem usar identificadores estáveis.
- Inspecione o código real antes de citar classes, endpoints, tabelas ou configurações.
- Não introduza dependência, serviço externo ou abstração sem necessidade demonstrada pela especificação.
- Decisões estruturais novas exigem ADR.
- Ao terminar um incremento: formate, compile, execute os testes relevantes, revise o diff e registre riscos restantes.

## Segurança e qualidade

- Defina e aplique isolamento de dados, autorização e auditoria adequados ao domínio.
- Nunca registre ou versione segredos, tokens, chaves ou dados sensíveis sem uma política explícita.
- Valide entradas, limites, autorização e idempotência onde forem relevantes.
- Código de domínio não deve depender diretamente de SDKs de fornecedores.
- Regras críticas exigem testes automatizados; integrações exigem testes de contrato ou integração quando aplicável.
- Mudanças de banco devem incluir migração reversível e testes dos limites de dados definidos pelo produto.
- Trate warnings novos como falhas até que haja justificativa documentada.
- Mantenha o idioma do código consistente; a documentação pode seguir o idioma adotado pela equipe.
