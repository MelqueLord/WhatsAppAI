# Contexto: produção

Produção usa PostgreSQL e sessões QR persistentes. O deploy aplica migration antes da aplicação, preserva volumes e valida health checks, logs sanitizados, HTTPS e canais críticos. Rollback, restore e smoke real continuam gates de saída.

Fonte: [produção e observabilidade](../design/producao-e-observabilidade.md), [checklist](../tasks/producao.md) e [roadmap](../../ROADMAP.md).
