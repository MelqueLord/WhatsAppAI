# Quickstart de desenvolvimento

Este documento define a sequência esperada; os comandos executáveis serão confirmados na **T001–T003**.

## Pré-requisitos

- Git
- .NET SDK 10 compatível com `global.json`
- Node.js LTS e gerenciador de pacotes fixado no repositório
- Docker com Compose
- HTTPS local confiável para testes de webhook, quando necessário

## Preparação planejada

Copie `.env.example` para um `.env` local ignorado, substitua a senha de exemplo e execute:

```bash
docker compose up -d mysql
docker compose ps mysql
dotnet user-secrets --project src/WhatsAppAI.WebApi set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=whatsapp_ai;User=whatsapp_ai;Password=<senha-local>"
dotnet restore
dotnet run --project src/WhatsAppAI.WebApi
```

**Nota:** O modo desenvolvimento usa SQLite por conveniência (configurado em `Program.cs`). O MySQL via Docker Compose é usado para testes de integração e produção.

Nenhuma migration existe no bootstrap. O primeiro `dotnet ef database update` somente será executado depois da migration de Tenant/User prevista na Fase 1.

Em outro terminal:

```bash
cd apps/web
npm ci
npm run dev
```

## Planos de assinatura

A aplicação possui dois planos pré-configurados (seed automático):

| Plano | Código | IA | Descrição |
|---|---|---|---|
| BOT | `BOT` | Não | Todos os recursos exceto IA para atendimento |
| IA + BOT | `IA_BOT` | Sim | Completo com IA para atendimento automatizado |

O plano é selecionado ao criar um tenant via `/api/admin/tenants`. Funcionalidades de IA são filtradas automaticamente baseado no plano.

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
