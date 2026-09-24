# ADR-0004: n8n não participa do núcleo

**Status:** Aceito — 2026-08-03

## Decisão

Webhooks, estado de conversa, filas, política de IA e envios são implementados no produto. n8n não é dependência de disponibilidade nem de consistência.

## Consequências

Regras ficam tipadas, testáveis e versionadas junto ao código. n8n continua elegível para automações periféricas futuras que não controlem o atendimento.
