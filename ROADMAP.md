# Roadmap do WhatsApp AI Manager

**Atualizado em:** 2026-09-13
**Papel:** roadmap canônico do repositório. Especificações, planos, tarefas e ADRs continuam sendo fontes de verdade para cada incremento.

## Estado atual

O backlog de capacidades está implementado até T254. Permanecem abertas as validações T008 e T012 para avisos de fila, T013 para LGPD/prontidão de produção e o incremento de correção T255–T269, aberto pela avaliação de segurança e operação de 2026-09-13.

## Próximos marcos

1. **Correção de segurança e prontidão (bloqueador):** concluir T255–T269, incluindo sessão por cookie, revogação, isolamento de tenant, logs, readiness, ponte QR, backup e cadeia de entrega. O piloto não avança enquanto houver pendência P0 ou P1 sem risco aprovado.
2. **Fechamento de qualidade:** validar migrations, builds, testes, Outbox idempotente, rollback, restauração e smoke real de Cloud API, QR, IA, HTTPS e SignalR.
3. **Piloto controlado:** operar dentro da capacidade configurada, com monitoramento de Inbox, Outbox, handoffs, franquia, privacidade e incidentes.
4. **Conciliação do broadcast QR Code:** decidir explicitamente se o módulo passa a fazer parte do escopo comercial, pois o código o contém e a especificação-base ainda exclui mensagens proativas.
5. **Evolução por evidência:** considerar RAG vetorial, cache, broker, automações periféricas ou escala independente somente quando métricas demonstrarem necessidade e houver ADR aprovado.

## Critério de avanço

Nenhum marco avança sem requisitos rastreáveis, testes adequados, isolamento por tenant, revisão de segurança, evidências operacionais e atualização dos documentos afetados.

## Referências

- [Contexto do projeto](docs/contexto/projeto.md)
- [Contextos por domínio](docs/contexto/)
- [Regras do projeto](docs/regras/)
- [Tarefas canônicas](docs/tasks/plataforma.md)
- [Checklist de deploy](docs/tasks/producao.md)
- [Correção de segurança e prontidão](docs/tasks/seguranca-e-prontidao.md)
