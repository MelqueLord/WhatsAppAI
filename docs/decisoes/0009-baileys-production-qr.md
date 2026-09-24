# ADR-0009: Baileys para conexões QR em produção

**Status:** Aceito — 2026-08-27

## Contexto

Além das linhas conectadas pela WhatsApp Cloud API, a plataforma oferece linhas QR. Essas linhas usam a ponte WhatsApp Web baseada em Baileys, já isolada por tenant e linha.

## Decisão

Baileys é permitido em produção exclusivamente para conexões QR. A Cloud API continua sendo o canal oficial para linhas Cloud. A ponte deve manter sessões e segredos isolados por tenant/linha, autenticar chamadas ao backend e registrar falhas operacionais. O tenant aceita os riscos próprios desse canal.

## Consequências

- A sessão QR pode exigir nova autenticação e ficar indisponível por ação do WhatsApp ou mudança do protocolo.
- Operação, alertas e suporte distinguem o canal Cloud do canal QR.
- A IA continua sem acesso direto a Meta ou Baileys; o backend decide qualquer envio.
