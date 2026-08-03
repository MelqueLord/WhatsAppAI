# ADR-0002: WhatsApp Cloud API oficial

**Status:** Aceito — 2026-08-03

## Contexto

Conexões não oficiais podem quebrar, causar banimento e dificultar conformidade.

## Decisão

Integrar diretamente com a WhatsApp Cloud API da Meta. Não usar automação de WhatsApp Web nem gateways não oficiais.

## Consequências

O cliente precisa de ambiente Meta aprovado e aceita custos/políticas do provedor. A integração deve acompanhar versões oficiais e tratar webhooks idempotentemente.
