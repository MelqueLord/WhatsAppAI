# Design: segurança e prontidão de produção

**Status:** planejado
**Data:** 2026-09-13
**Especificação:** [segurança e prontidão](../specs/producao/seguranca-e-prontidao.md)

## Contexto e princípio de execução

As correções preservam o monólito modular, PostgreSQL, ASP.NET Core, React e a ponte QR existente. Cada tarefa deve começar pelo teste que demonstra a falha ou pelo inventário que delimita a alteração; não há mudança de código enquanto o item dependente não estiver decidido e rastreado.

## Autenticação de navegador

O contrato do navegador passa a ser cookie de sessão e token antiforgery. A API não devolve JWT para clientes de navegador, o frontend elimina o armazenamento, o cabeçalho Bearer e qualquer `console` que exponha a resposta de autenticação. SignalR autentica pela mesma sessão de cookie, sem token em query string.

O validador de sessão consulta o estado atual de usuário, membership, tenant e security stamp antes de autorizar a requisição ou o hub. Caso a plataforma preserve algum tipo de credential não destinada a navegador no futuro, ele requer especificação de cliente e validação de revogação equivalente; esta entrega não presume esse novo contrato.

## Isolamento de tenant

Os filtros globais continuam como defesa padrão. Interfaces e repositórios que leem ou escrevem dados do tenant recebem `TenantId` de modo explícito quando a consulta não pode herdar o filtro. `IgnoreQueryFilters()` fica restrito a uma lista curta de operações de worker/admin, cada uma com predicado de tenant e teste de isolamento.

O interceptor usa o tenant da requisição/execução existente, sem abrir um escopo novo. A revisão inventaria todas as ocorrências de bypass e divide refatorações em tarefas pequenas, para que nenhuma consulta continue dependente apenas da disciplina do chamador.

## Observabilidade e health checks

A sanitização ocorre no pipeline Serilog antes dos sinks, cobrindo chaves de propriedades e valores aninhados, request logging e exceções. Dados que não podem ser transformados com segurança são descartados. Testes capturam o evento final de um sink de memória e verificam as sentinelas proibidas.

`/health/live` não depende de banco ou serviço externo. `/health/ready` registra uma verificação Npgsql/PostgreSQL rotulada `ready` e retorna indisponível se a conexão ou operação mínima de persistência falhar. Métricas e alertas passam a usar essa semântica.

## Ponte QR e dados retidos

O [ADR-0015](../decisoes/0015-isolamento-servico-ponte-qr.md) é pré-requisito para CR-006. Após a decisão, a implementação separa callbacks públicos de comandos internos, aplica identidade de serviço com escopo mínimo e exige lease para toda mutação de sessão. O arquivo de inbox da ponte deve ser removido ou substituído por estado mínimo protegido, com retenção explicitamente justificada; o banco da plataforma permanece a fonte de verdade de conversas.

## Backup e entrega

O processo de backup deve produzir arquivo cifrado, permissões mínimas, cópia fora do host e manifesto de integridade sem vazar segredo. O ensaio restaura banco, Data Protection e estado QR necessário num ambiente isolado, mede tempos e guarda apenas evidências sanitizadas.

Configurações de limite de taxa devem ser carregadas e validadas pelo ambiente. A política CSP é adicionada por Nginx após validação de recursos necessários. CI fixa as referências, verifica dependências e imagens, e publica artefatos de análise; decisões sobre produtos externos de backup permanecem fora deste design até escolha explícita.

## Frontend e validação

A tabela de broadcast deve conter apenas elementos válidos de tabela. Rotas ou módulos pesados são carregados sob demanda até atender o orçamento de bundle definido durante T268. Um teste de navegador cobre login por cookie, abertura da inbox, atualização SignalR e a tela corrigida de broadcast.

## Estratégia de testes e entrega

1. Testes unitários para sanitização, política de tenant e autenticação/revogação.
2. Testes de integração para cookie/CSRF, autorização, SignalR, health readiness e isolamento de dados.
3. Testes de contrato para a identidade da ponte QR e testes de rede no Compose aprovado pelo ADR.
4. Teste de restauração e smoke em staging com evidências sanitizadas.
5. Revisão de diff, formatação, build com SDK .NET fixado, testes relevantes, auditorias e atualização do gate de produção.
