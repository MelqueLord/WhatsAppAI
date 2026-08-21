# Validação da candidata a produção

## Pré-requisitos

- Checkout limpo da tag candidata.
- Docker/Compose, .NET 10 e Node 22.
- Arquivo de ambiente de staging preenchido por cofre, sem segredos no repositório.

## Situação já concluída em código

- Endurecimento de cookies/CSRF (produção).
- `cookies.txt` removido e sessão antiga invalidada por rotação de nome de cookie.
- Compose com variáveis obrigatórias e sem defaults inseguros para segredos.
- Nginx por template com `DOMAIN` obrigatório.
- Migration bundle + serviço `migrate` no Compose.

## Sequência

1. Validar configuração do Compose e do Nginx.
2. Construir imagens sem cache e iniciar MySQL 8.4.
3. Executar serviço `migrate` em banco vazio e em cópia da versão anterior.
4. Executar build, lint e todos os testes backend/frontend.
5. Subir API, frontend e proxy; validar liveness, readiness, HTTPS e SignalR.
6. Executar smoke test dos três papéis e teste negativo entre dois tenants.
7. Testar webhook assinado, duplicidade, mensagem humana, IA, fila e tag.
8. Executar backup, restauração e rollback da aplicação/migration.

## Resultado esperado

Todos os itens de `contracts/production-readiness-gates.md` estão aprovados e suas evidências estão anexadas à release. Qualquer falha interrompe a promoção.

## Estado da validação local (2026-08-21)

- `dotnet restore` e `dotnet build -c Release`: ok.
- `dotnet test -c Release --no-build`: falhou (3 testes).
- `npm run build`: ok.
- `npm run lint`: falhou (23 erros).
- `npm test`: falhou (1 teste em `AiConfigPage.test.tsx`).
- `docker compose --profile production config`: pendente (Docker indisponível no ambiente validado).
