# Quickstart de validação

1. Configurar linha Cloud API ativa, token no cofre e template `UTILITY` aprovado com BODY texto.
2. Criar lista oficial, escolher linha/template/idioma, preencher a quantidade exata de parâmetros e selecionar dois contatos do tenant.
3. Disparar e confirmar revalidação; cada destinatário deve ganhar uma Message e uma Outbox com chave distinta.
4. Rodar workers e verificar SignalR, `Queued`, `Sent`/`Failed` e conclusão.
5. Repetir com template marketing/reprovado, contagem inválida, linha desconectada, reinício concorrente, cancelamento, Operator sem linha/fila e lista QR existente. Validar as rejeições e ausência de duplicidade/vazamento.

Executar testes de domínio, endpoints, integração PostgreSQL, Vitest da tela, build/lint frontend e aplicar/reverter a migration antes da aprovação.
