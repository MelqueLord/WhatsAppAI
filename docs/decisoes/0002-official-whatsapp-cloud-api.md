# ADR-0002: WhatsApp Cloud API como canal oficial

**Status:** Aceito, complementado por ADR-0009 — 2026-08-27

## Contexto

A Cloud API é o canal oficial e suportado da Meta. O produto também opera conexões QR via Baileys conforme ADR-0009, com riscos operacionais aceitos pelo tenant.

## Decisão

Integrar diretamente com a WhatsApp Cloud API da Meta para linhas Cloud. Esta decisão não exclui a ponte WhatsApp Web/Baileys aprovada no ADR-0009 para linhas QR.

## Consequências

O cliente precisa de ambiente Meta aprovado e aceita custos/políticas do provedor. A integração deve acompanhar versões oficiais e tratar webhooks idempotentemente.
