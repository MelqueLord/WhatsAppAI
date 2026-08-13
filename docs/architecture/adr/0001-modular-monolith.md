# ADR-0001: monólito modular

**Status:** Aceito — 2026-08-03

## Contexto

O MVP precisa de consistência entre mensagens, modo da conversa, IA e outbox, com equipe pequena e baixo custo operacional.

## Decisão

Usar um monólito modular .NET, uma base PostgreSQL e um frontend React separado. Módulos têm limites de código, mas compartilham transação quando necessário.

## Consequências

Deploy, debugging e transações são simples. Escala independente não existe inicialmente. Extração exige métrica, caso de uso e novo ADR.
