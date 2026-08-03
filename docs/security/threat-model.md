# Modelo de ameaças e privacidade

## Ativos críticos

Credenciais Meta/OpenAI, sessões, números de telefone, mensagens, conhecimento do negócio, identificação de tenant, eventos de auditoria e intenção de envio.

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

## Classificação e retenção

- **Segredo:** tokens/chaves; nunca em logs, exportações ou browser.
- **Pessoal:** telefone, nome e mensagens; cifrar quando aplicável, acesso por finalidade e retenção limitada.
- **Operacional:** IDs, status, latências e unidades; preferir pseudonimização.
- **Público/interno:** conhecimento comercial conforme classificação do tenant.

Definir retenção padrão antes da Fase 7 e validar bases legais, termos, direitos dos titulares e contratos com aconselhamento jurídico. Este documento é engenharia de segurança, não parecer jurídico.

## Resposta a incidente

Revogar/rotacionar segredo, suspender integração afetada, preservar auditoria, delimitar tenants/eventos, comunicar conforme política aplicável e registrar post-mortem/ADR. Nunca apagar evidência para “limpar” o incidente.
