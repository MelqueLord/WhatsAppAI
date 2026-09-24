# Spec: controlar status de conexão

O status de cada linha é monitorado e diagnosticável pelo tenant autorizado ou PlatformAdmin. Falhas de Cloud e QR mantêm histórico e rastreabilidade, sem expor token ou sessão.

Para uma linha Cloud, a interface apresenta `Conectado` apenas depois de validar as credenciais da própria linha contra a Meta. Ausência de configuração, token ou validação faz a interface apresentar `Desconectado`; o diagnóstico é sanitizado e isolado ao tenant corrente.

Fonte: [runbook de disponibilidade](../../specs/producao/health-checks.md) e [runbook de webhook](processar-webhook.md).
