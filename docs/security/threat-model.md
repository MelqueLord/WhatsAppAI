# Modelo de ameaças e privacidade

## Ativos críticos

Credenciais Meta/OpenAI, sessões, convites de ativação, números de telefone, mensagens, conhecimento do negócio, identificação de tenant, eventos de auditoria e intenção de envio.

## Ameaças e controles

| Ameaça | Controle mínimo | Verificação |
|---|---|---|
| acesso entre tenants | tenant derivado da sessão, filtros, FKs/índices, grupos SignalR no servidor | testes negativos com dois tenants |
| webhook forjado/replay | assinatura, limite de tempo/tamanho, chave idempotente, rate limit | fixtures inválidas e reentrega |
| segredo exposto | cofre, campos write-only, mascaramento, scanner de segredos | testes de logs/respostas e CI |
| CSRF/sequestro de sessão | cookie Secure/HttpOnly, SameSite, antiforgery, rotação | testes de integração web |
| resposta automática indevida | modo, janela, saída estruturada, versão e revalidação | corrida humano/IA e janela fechada |
| prompt injection do cliente | instruções hierárquicas, conhecimento tratado como dados, ferramentas indisponíveis | suíte adversarial |
| envio duplicado | Inbox/Outbox, unique keys e reconciliação de provider ID | timeout/retry/replay |
| abuso de custo | limites por tenant, orçamento de tokens, rate limit e alerta | teste de quota/circuit breaker |
| PII em telemetria | logging allowlist, hash/máscara, conteúdo fora de logs | inspeção automatizada |
| administrador excessivo | contexto explícito, least privilege e auditoria append-only | revisão de autorização |
| colisão de ledger entre tenants | unicidade `(tenant_id, provider, metric, source_id)` | duas fontes iguais em tenants distintos |
| vazamento de mídia Meta | proxy autenticado tenant-scoped; token/URL privada somente no backend | acesso cruzado e inspeção da resposta |
| alteração de auditoria | identidade da aplicação sem `UPDATE`/`DELETE` em AuditLog | teste de banco com operações negadas |
| roubo/replay de convite | token aleatório armazenado somente como hash, uso único, TTL de 24 h, revogação no reenvio e rate limit | token usado/expirado/revogado não ativa |
| enumeração na ativação | erros públicos equivalentes e sem indicar e-mail/membership | tokens desconhecidos e expirados têm resposta sanitizada |
| elevação na gestão de Operators | tenant derivado da sessão e papel TenantOwner obrigatório; alvo deve ser Operator do mesmo tenant | Owner A não opera B; Operator não administra memberships |
| sessão sobrevivente à desativação | rotação de `security_stamp` e validação do estado da membership | cookie emitido antes da desativação é rejeitado |

## Webhook do Meta App compartilhado

O `app_secret` e o verify token do único Meta App são segredos globais da plataforma e passam pelo `ISecretStore`. O GET valida o challenge com o verify token; o POST valida `X-Hub-Signature-256` sobre os bytes originais antes de interpretar `phone_number_id`. Somente então o tenant é resolvido. Evento desconhecido mantém envelope allowlisted/sanitizado separado do payload original cifrado e restrito; sem tenant resolvido, permanece em quarentena e não cria dados tenant-owned (**FR-004**, **FR-005**, **FR-022**, **BR-011**).

## Sessão, CSRF e mídia

Frontend e backend operam no mesmo site. Em produção, a sessão usa cookie `HttpOnly`, `Secure`, `SameSite=Lax`; o token antiforgery obtido no bootstrap deve acompanhar mutações em `X-CSRF-TOKEN`, inclusive login. Mídia é transmitida exclusivamente por endpoint autenticado e tenant-scoped da WebApi; token e URL privada da Meta nunca chegam ao navegador (**FR-001**, **FR-023**, **NFR-006**).

## Convites, ativação e Operators

PlatformAdmin cria o tenant e o convite do TenantOwner; TenantOwner ativo gerencia somente Operators do tenant corrente. O link de ativação é retornado apenas na criação/reenvio para entrega manual, nunca enviado por serviço de e-mail no MVP e nunca registrado. O banco persiste apenas `token_hash`; convite expira em 24 horas, é de uso único e reenvio revoga o anterior. `POST /auth/activate` recebe erro sanitizado, rate limit e consumo transacional. Desativação de Operator invalida sessões imediatamente; reativação exige novo login. Cada usuário possui no máximo uma membership de tenant (**US-008**, **US-009**, **FR-025–028**, **BR-012–015**, **NFR-006**).

## Classificação e retenção

- **Segredo:** tokens/chaves; nunca em logs, exportações ou browser.
- **Pessoal:** telefone, nome e mensagens; cifrar quando aplicável, acesso por finalidade e retenção limitada.
- **Operacional:** IDs, status, latências e unidades; preferir pseudonimização.
- **Público/interno:** conhecimento comercial conforme classificação do tenant.

Definir retenção padrão antes da Fase 7 e validar bases legais, termos, direitos dos titulares e contratos com aconselhamento jurídico. Este documento é engenharia de segurança, não parecer jurídico.

Prompt completo, raciocínio interno e resposta bruta da IA não são persistidos no MVP. Apenas metadados operacionais allowlisted e sanitizados são permitidos (**FR-016**, **NFR-008**).

## Resposta a incidente

Revogar/rotacionar segredo, suspender integração afetada, preservar auditoria, delimitar tenants/eventos, comunicar conforme política aplicável e registrar post-mortem/ADR. Nunca apagar evidência para “limpar” o incidente.
