# Quickstart de desenvolvimento

Este documento define a sequência esperada; os comandos executáveis serão confirmados na **T001–T003**.

## Pré-requisitos

- Git
- .NET SDK 10 compatível com `global.json`
- Node.js LTS e gerenciador de pacotes fixado no repositório
- Docker com Compose
- HTTPS local confiável para testes de webhook, quando necessário

## Preparação planejada

```bash
docker compose up -d postgres
dotnet restore
dotnet ef database update --project src/WhatsAppAI.Infrastructure --startup-project src/WhatsAppAI.WebApi
dotnet run --project src/WhatsAppAI.WebApi
```

Em outro terminal:

```bash
cd apps/web
npm ci
npm run dev
```

## Segredos locais

Não versionar credenciais. Usar `dotnet user-secrets` no backend e um `.env.local` ignorado para valores públicos do frontend. Tokens Meta/OpenAI nunca devem estar no bundle da SPA.

## Verificação antes de commit

```bash
dotnet format --verify-no-changes
dotnet build --no-restore
dotnet test --no-build
cd apps/web && npm run lint && npm run test && npm run build
```

O bootstrap pode ajustar comandos, mas deve manter um único comando de CI reproduzível e documentado.
