# Especificação: correção de segurança e prontidão de produção

**Status:** planejada
**Data:** 2026-09-13
**Origem:** [avaliação de padrões de indústria](../../pesquisa/avaliacao-padroes-industria.md)

## 1. Objetivo

Restabelecer a conformidade da implementação com os requisitos já aprovados de autenticação, isolamento de tenant, sigilo, disponibilidade e recuperação antes do piloto. Este incremento não adiciona uma capacidade de produto; ele corrige divergências entre a implementação e FR-001, FR-002, FR-004, FR-016, FR-019, FR-028, NFR-004, NFR-005, NFR-006 e NFR-008, além dos aceites de US-007 e US-009.

## 2. Escopo de correção

- **CR-001 — Sessão de navegador protegida:** o navegador usa exclusivamente o cookie de sessão `HttpOnly`, `Secure` e `SameSite=Lax`. Login, respostas de autenticação, chamadas API e SignalR não expõem nem persistem JWT no JavaScript, `localStorage`, `sessionStorage`, URL ou logs do navegador.
- **CR-002 — Revogação imediata:** uma credential aceita pela plataforma deve ser rejeitada antes de endpoint ou hub quando o usuário, sua membership, seu tenant ou seu security stamp não forem válidos. Desativação encerra o acesso imediatamente e reativação exige nova autenticação.
- **CR-003 — Isolamento obrigatório nas consultas:** todo acesso autenticado a dados de negócio é limitado pelo tenant corrente no predicado executado. Repositórios tenant-scoped exigem o tenant no contrato; remoção de filtro global só é permitida em worker ou operação administrativa explicitamente documentada, com predicado de tenant e teste de isolamento.
- **CR-004 — Logs efetivamente sanitizados:** a infraestrutura sanitiza propriedades estruturadas e exceções antes do destino de log. Tokens, chaves, cookies, telefones, e-mails, conteúdo de mensagens, prompts completos e dados pessoais não mascarados não podem aparecer na telemetria.
- **CR-005 — Health checks úteis:** liveness mede somente a capacidade do processo responder. Readiness falha quando PostgreSQL ou a persistência essencial não estão disponíveis e só declara pronto após as dependências marcadas como `ready` responderem.
- **CR-006 — Isolamento da ponte QR:** comandos de sessão, lease e configuração da ponte QR não ficam acessíveis pela superfície pública de webhook e usam uma identidade de serviço delimitada. A topologia e a autenticação só serão implementadas após decisão registrada no ADR-0015.
- **CR-007 — Retenção mínima da ponte QR:** a ponte não mantém uma cópia independente e não protegida do conteúdo de conversas. Qualquer estado operacional retido tem finalidade, prazo, controle de acesso e proteção compatíveis com as regras de dados sensíveis.
- **CR-008 — Backup recuperável:** backups de banco e estado necessário à recuperação são cifrados, com permissões restritas, cópia fora do host e evidência periódica de restauração que demonstre RPO de 24 horas e RTO de 4 horas.
- **CR-009 — Configuração de borda verificável:** limites de taxa vêm de configuração validada por ambiente e a borda aplica uma política CSP compatível com a aplicação, cookies e SignalR.
- **CR-010 — Cadeia de entrega reproduzível:** ações CI, imagens e pacotes possuem versões imutáveis ou atualização controlada; a entrega verifica dependências, segredos, vulnerabilidades e imagens, produzindo evidência rastreável.
- **CR-011 — Interface válida e acessível:** a tela de broadcast produz HTML semanticamente válido em tabelas e o bundle inicial respeita o orçamento de desempenho definido no plano técnico, com teste de fluxo crítico no navegador.
- **CR-012 — Gate operacional honesto:** roadmap, checklist e gate de produção refletem estes bloqueios até que cada aceite tenha evidência anexada.

## 3. Critérios de aceite do incremento

1. Testes de frontend e integração demonstram que nenhuma credencial de sessão é lida, gravada ou registrada pelo JavaScript do navegador; uma sessão por cookie continua funcionando em API e SignalR.
2. Testes de autenticação rejeitam imediatamente cookie e qualquer credential suportada depois de desativação, troca de security stamp ou remoção de membership, inclusive na conexão SignalR.
3. Testes de integração provam que uma operação de tenant não encontra, altera nem transmite dados de outro tenant; o conjunto de exceções a filtros possui inventário e cobertura.
4. Testes de logging usam valores sentinela para segredo, token, telefone, e-mail e conteúdo, e comprovam que o destino observado contém somente versões mascaradas.
5. Testes de health check distinguem processo vivo de PostgreSQL indisponível; a readiness falha no segundo caso.
6. O ADR-0015 está aceito antes de alterar a topologia da ponte; seus testes provam que a rede pública não executa comandos internos e que o estado QR não retém conteúdo de conversa indevidamente.
7. Um exercício documentado restaura dados e estado protegidos em ambiente isolado dentro do RPO/RTO acordado.
8. A pipeline aprovada falha para versões não permitidas, segredos, vulnerabilidades acima do limiar definido ou imagem sem verificação, e registra seus artefatos.
9. O checklist de produção permanece NO-GO até todos os itens críticos e altos possuírem evidência de aceite.

## 4. Fora de escopo

Este incremento não introduz login social, MFA, novos provedores, microsserviços, broker, cache ou uma plataforma externa de backup. A escolha de provedor de backup ou de mecanismo de identidade entre serviços só pode ocorrer quando necessária à decisão ADR e aprovada separadamente.

## 5. Rastreabilidade

| Requisitos | Design | Tarefas |
|---|---|---|
| CR-001 a CR-012 | [segurança e prontidão](../../design/seguranca-e-prontidao.md) | [segurança e prontidão](../../tasks/seguranca-e-prontidao.md) |
