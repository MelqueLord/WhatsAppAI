# Quickstart de desenvolvimento

Este documento registra a sequencia local confirmada pelo bootstrap **T001-T003**.

## Pré-requisitos

- Git
- .NET SDK 10 compatível com `global.json`
- Node.js LTS e gerenciador de pacotes fixado no repositório
- Docker com Compose
- HTTPS local confiável para testes de webhook, quando necessário

## Preparacao local

Copie `.env.example` para um `.env` local ignorado, substitua a senha de exemplo e execute:

```bash
docker compose up -d postgres
docker compose ps postgres
dotnet user-secrets --project src/WhatsAppAI.WebApi set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=whatsapp_ai;Username=whatsapp_ai;Password=<senha-local>"
dotnet restore
dotnet run --project src/WhatsAppAI.WebApi
```

Nenhuma migration existe no bootstrap. O primeiro `dotnet ef database update` somente sera executado depois da migration de Tenant/User prevista na Fase 1.

Em outro terminal:

```bash
cd apps/web
npm ci
npm run dev
```

## Segredos locais

Não versionar credenciais. Usar `dotnet user-secrets` no backend e um `.env.local` ignorado para valores públicos do frontend. Tokens Meta/OpenAI nunca devem estar no bundle da SPA.

## Verificacao antes de commit

```bash
dotnet format --verify-no-changes
dotnet build --no-restore
dotnet test --no-build
cd apps/web && npm run lint && npm run test && npm run build
```

Enquanto `T004` e `T005` nao forem concluidas, estes comandos sao a verificacao manual esperada. O CI reproduzivel sera documentado e automatizado em `T005`.
