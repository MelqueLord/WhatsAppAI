# Design: uso e limites

Ledger e reservas de quota controlam consumo mensal UTC por tenant. Reserva protege concorrência antes da chamada; commit ocorre em resposta válida enfileirada; release ocorre nas falhas; reconciliador encerra reservas pendentes. Custos técnicos são administrativos.

Fonte: [ADR de franquia](../decisoes/0010-platform-managed-ai-allowances.md) e [plano de quota](plano-franquia-ia.md).
