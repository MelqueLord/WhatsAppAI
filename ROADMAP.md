# Roadmap do WhatsApp AI Manager

**Atualizado em:** 2026-10-18
**Papel:** roadmap canônico do repositório. Especificações, planos, tarefas e ADRs continuam sendo fontes de verdade para cada incremento.

## Estado atual

O [backlog principal](docs/tasks/plataforma.md) registra como concluídas as capacidades até T284, incluindo o envio de templates aprovados de Utilidade ou Marketing na Inbox e no disparo em massa da API Oficial (**BR-006**, ADR-0017). T285, T286 e T287 têm implementação parcial e continuam abertos até a validação indicada em cada tarefa. Permanecem abertas as validações T008 e T012 do [incremento de avisos de fila](docs/tasks/avisos-transferencia-fila.md) e T013 do [incremento LGPD](docs/tasks/lgpd-prontidao-producao.md). O [backlog de segurança e prontidão](docs/tasks/seguranca-e-prontidao.md) mantém correções e evidências de produção pendentes; nele, T262 e T264 estão marcadas como concluídas e T263 como parcial.

## Próximos marcos

1. **Correção de segurança e prontidão (bloqueador):** concluir as tarefas ainda abertas no [backlog especializado](docs/tasks/seguranca-e-prontidao.md), incluindo sessão por cookie, revogação, isolamento de tenant, logs, readiness, ponte QR, backup e cadeia de entrega. O piloto não avança enquanto houver pendência P0 ou P1 sem risco aprovado.
2. **Fechamento de qualidade:** validar T285–T287, migrations, builds, testes, Outbox idempotente, rollback, restauração e smoke real de Cloud API, QR, IA, HTTPS e SignalR.
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
