# Regras: arquitetura

- Preservar o monólito modular e as dependências `WebApi → Application → Domain`, com Infrastructure como adaptador.
- Não permitir SDKs da Meta ou IA nas camadas Domain e Application.
- Não introduzir componente operacional ou abstração genérica sem necessidade demonstrada.
- Criar ADR antes de decisão estrutural nova.

Fonte: [constituição](../../.specify/memory/constitution.md) e [ADR-0001](../decisoes/0001-modular-monolith.md).
