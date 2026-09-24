# Regras: WhatsApp

- Validar challenge e assinatura da Cloud API antes de identificar tenant por linha.
- Persistir webhook antes de processamento assíncrono e usar chaves idempotentes.
- Todas as mensagens de saída passam pela Outbox e registram estado de entrega.
- Bloquear texto livre após 24 horas; templates transacionais são exclusivos de linhas oficiais.

Fonte: [ADR Cloud API](../decisoes/0002-official-whatsapp-cloud-api.md), [ADR QR](../decisoes/0009-baileys-production-qr.md) e [specs WhatsApp](../specs/whatsapp/).
