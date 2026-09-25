# ADR-0015 — Isolamento do serviço da ponte QR

**Status:** Aceito
**Data:** 2026-09-25
**Relacionados:** [CR-006](../specs/producao/seguranca-e-prontidao.md), [design](../design/seguranca-e-prontidao.md), [ADR-0011](0011-postgresql-qr-session-leases.md)

## Contexto

A ponte QR mantém estado de sessão e expõe comandos de sessão/configuração. A revisão A-003 identificou autenticação por segredo global, rota pública ampla de webhook e operações que não aplicam o mesmo controle de lease. Um segredo comprometido não pode conferir capacidade administrativa sobre todas as sessões, e comandos internos não devem competir com callbacks externos na mesma superfície pública.

## Decisão

Adotar a **opção 1: rede Docker privada para a ponte, bloqueio explícito de comandos internos no Nginx e identidade de serviço rotacionável**. A decisão preserva o monólito modular e não introduz um broker, microsserviço ou infraestrutura externa sem necessidade demonstrada.

### Rede e superfície pública

- A ponte QR não publica porta no host e integra uma rede Compose marcada como `internal`; somente `api`, `worker` e as instâncias `whatsapp-web` participam dessa rede.
- O Nginx não encaminha nenhuma rota da ponte. Na superfície pública, somente os callbacks externos previstos da Meta continuam em `/api/webhooks/whatsapp`.
- As rotas de estado de sessão, lease e comandos da ponte deixam de usar o prefixo público de webhook. Elas serão disponibilizadas exclusivamente pela rede interna em `/internal/whatsapp-web/...` no T263.
- Health check da ponte é utilizado apenas pela rede interna/orquestrador e não é publicado pelo Nginx.

### Identidade, rotação e revogação

- Cada chamada ponte→WebApi e WebApi/worker→ponte usa `X-WhatsApp-Web-Service-Id` e `X-WhatsApp-Web-Service-Token` por TLS da rede interna. Os valores são montados como segredos de arquivo somente leitura, nunca registrados, devolvidos ao navegador ou persistidos pelo produto.
- A WebApi aceita apenas o identificador de serviço aprovado e um token atual; durante uma rotação, aceita também o token anterior por no máximo 24 horas. A revogação remove o token comprometido da janela de aceitação e reinicia as instâncias afetadas.
- O segredo de callback externo não autentica comandos internos. A chave atual compartilhada por compatibilidade será descontinuada no T263, depois da migração dos dois lados.

### Autorização de comandos e lease

- Toda mutação de estado QR recebe o `sessionId` no formato `{tenantId}-qr-{lineNumber}` e extrai tenant e linha desse identificador validado; nenhum tenant é confiado de payload ou cabeçalho.
- Salvar, restaurar, apagar credenciais, criar/renovar/liberar lease, iniciar sessão, obter QR, consultar estado, logout e enviar mensagem exigem identidade interna aprovada. As mutações confirmam que o `instanceId` autenticado é o proprietário do lease para a sessão/linha.
- Callbacks de mensagem encaminhados pela ponte continuam idempotentes, mas também passam pela identidade interna e validam a sessão proprietária antes de persistir o evento.

### Migração e rollback

1. T263 adiciona a rede interna, os novos caminhos e a validação de token duplo, mantendo temporariamente o caminho legado acessível somente dentro da rede durante o rollout.
2. Cada instância da ponte é atualizada uma por vez; ela renova o lease e recupera as credenciais existentes antes de assumir novas sessões. Não há migração de conteúdo de conversa.
3. Depois de todos os consumidores usarem a rota interna, o Nginx bloqueia definitivamente o caminho legado e a validação do segredo externo nele é removida.
4. Rollback dentro da janela de 24 horas usa o token anterior e a imagem anterior, preservando os volumes de credenciais e os leases. Encerrada a janela, rollback exige nova rotação planejada; não se reabre rota pública.

### Evidências obrigatórias

T263 deve incluir testes de contrato para token ausente, inválido, revogado e anterior expirado; testes de lease para cada mutação; e um teste Compose/Nginx provando que a Internet não alcança comandos internos. Logs e métricas registram somente `serviceId` permitido, hash/correlação da sessão, resultado e categoria de falha — nunca token, telefone, QR, credencial ou conteúdo de mensagem.

## Consequências

O T263 implementará Compose, Nginx, WebApi e ponte conforme este contrato. T264 só poderá remover o snapshot de Inbox depois desses controles, preservando no máximo credenciais de sessão protegidas e estado operacional com finalidade/retenção explícita. CR-006 continua bloqueador de produção até a evidência de T263; CR-007 continua bloqueador até T264.
