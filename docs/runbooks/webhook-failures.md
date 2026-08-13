# Runbook: falhas de webhook e processamento

## Sinais

- aumento de HTTP 4xx/5xx em `/webhooks/whatsapp`;
- idade/profundidade de `WebhookEvent` Pending crescendo;
- eventos em `Dead` ou diferença entre dashboard Meta e mensagens persistidas;
- status de saída não avançam.

## Triagem

1. Identifique janela, correlação e tenant/conta pseudonimizados.
2. Separe falha de recebimento, assinatura, persistência, worker ou payload desconhecido.
3. Verifique health do banco, latência, locks expirados e versão/configuração do app Meta.
4. Confirme se houve rotação/expiração de segredo sem expô-lo.
5. Pause automação do tenant se existir risco de resposta incorreta ou duplicada.

## Recuperação segura

- Assinatura inválida: não reprocessar; corrigir segredo/configuração e validar novo evento.
- Banco indisponível: retornar erro para permitir retry do provedor; restaurar serviço e observar replay.
- Worker parado: recuperar lease expirado e retomar; não editar status manualmente.
- Payload desconhecido: marcar `Ignored` somente se comprovadamente irrelevante; caso contrário adicionar parser/teste e reprocessar pela chave original.
- Evento `Dead`: corrigir causa, registrar auditoria e usar comando administrativo idempotente de reprocessamento.

## Validação

Fila retorna à idade normal, não surgem duplicatas, mensagens/status batem com amostra do provedor e alerta permanece verde por pelo menos duas janelas de polling.

## Escalonamento

Se houver perda, vazamento entre tenants, segredo exposto ou envio incorreto, tratar como incidente de segurança, suspender a integração afetada e seguir `docs/security/threat-model.md`.

## Pós-incidente

Registrar linha do tempo, impacto, causa, lacuna de teste/alerta e ação com responsável/prazo. Mudança arquitetural exige ADR.
