# Design: integração WhatsApp

Cloud API usa app secret global para validar assinatura e resolve tenant por `phone_number_id` somente depois. QR Code usa ponte Baileys com segredo, sessão e lease por linha. Adaptadores protegem domínio e aplicação dos SDKs externos.

Fonte: [ADR Cloud](../decisoes/0002-official-whatsapp-cloud-api.md), [ADR QR](../decisoes/0009-baileys-production-qr.md) e [ADR leases](../decisoes/0011-postgresql-qr-session-leases.md).
