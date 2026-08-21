# Contrato de gates para publicação

Uma release recebe `GO` somente quando todos os resultados abaixo forem verdadeiros:

| Gate | Resultado esperado |
|---|---|
| Código | Checkout limpo, tag imutável e diff revisado. |
| Backend | Build `Release`, testes unitários/arquitetura/integração aprovados e zero warning novo. |
| Frontend | Lint, testes e build aprovados. |
| Segurança | Antiforgery obrigatório, cookies seguros, scanner limpo e isolamento de tenant aprovado. |
| Banco | MySQL 8.4 migrado do zero e da versão anterior; rollback/restauração aprovados. |
| Deploy | Compose validado, `nginx -t` aprovado, HTTPS, API, SignalR e health checks operantes. |
| Integrações | Meta/WhatsApp/IA testados sem segredo ou PII em logs. |
| Operação | Monitoramento, alertas, backup, restauração e rollback com responsáveis definidos. |

Qualquer falha gera `NO-GO`. Exceção exige registro formal com risco, responsável, prazo e aprovação conforme a constituição.
