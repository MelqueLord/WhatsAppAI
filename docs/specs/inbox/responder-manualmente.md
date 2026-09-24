# Spec: responder manualmente

Operator assume a conversa antes de responder. O comando usa concorrência otimista e atualiza para `Human`; mensagem humana é enfileirada com estado rastreável e respeita a janela de 24 horas.

Fonte: US-003 e FR-010 a FR-012 em [especificação-base](../plataforma/especificacao-plataforma.md).
