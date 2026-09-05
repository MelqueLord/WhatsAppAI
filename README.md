# WhatsApp AI Manager

Nome provisório de um SaaS multiempresa para centralizar atendimentos do WhatsApp Business e automatizar respostas com IA.

## Estado do projeto

O pacote SDD inicial foi implementado. O backlog em `specs/000-platform/tasks.md` esta marcado como concluido ate `T144`, cobrindo bootstrap, identidade/tenancy, WhatsApp, inbox, resposta humana, IA segura, conhecimento, uso/auditoria, producao/piloto e sistema de planos.

Implementado:

- Backend .NET 10 com WebApi, workers, EF Core, autenticacao, tenant isolation, SignalR, Meta/OpenAI, Inbox/Outbox, auditoria e uso.
- Frontend React 19.2 + TypeScript + Vite com telas de auth, admin, operadores, inbox, integracoes, conhecimento, uso, bot e planos.
- Persistencia PostgreSQL via Npgsql, com Supabase gerenciado ou Docker em producao propria.
- Docker, Nginx, scripts de backup/restore, observabilidade, runbooks e testes unitarios/integracao/arquitetura.

Atualizacao de readiness (2026-08-21):

- P0 implementado em codigo/configuracao: cookies e CSRF em producao, remocao de `cookies.txt`, compose com segredos obrigatorios, nginx por template e migration bundle com servico `migrate`.
- Validacao local executada: `dotnet restore` e `dotnet build -c Release` ok; `npm run build` ok.
- Pendencias atuais: 3 testes .NET falhando, 23 erros de lint frontend, 1 teste frontend falhando e 8 testes de webhook ainda ignorados.
- Validacao operacional de deploy continua pendente em host com Docker/TLS (`docker compose --profile production config`, `nginx -t` e smoke HTTPS/SignalR).

## Premissas fechadas

- Cada cliente é dono da conta Meta, do número, do método de pagamento e do projeto/chave da OpenAI.
- O produto usa a WhatsApp Cloud API e, para linhas QR, a ponte WhatsApp Web via Baileys em produção.
- O MVP atende conversas iniciadas pelo consumidor; não inclui campanhas nem disparos de marketing.
- O núcleo não depende de n8n.
- A arquitetura inicial é um monólito modular, sem microsserviços, RabbitMQ ou Redis.
- Stack de referência: .NET 10 LTS, React 19.2 + TypeScript, PostgreSQL e SignalR.

## Mapa da documentação

| Documento | Finalidade |
|---|---|
| `AGENTS.md` | Regras operacionais para agentes do Codex |
| `.specify/memory/constitution.md` | Princípios que governam todas as decisões |
| `specs/000-platform/spec.md` | Escopo, histórias, requisitos e critérios de sucesso |
| `specs/000-platform/plan.md` | Plano técnico e estrutura do código |
| `specs/000-platform/research.md` | Decisões e justificativas |
| `specs/000-platform/data-model.md` | Modelo de dados e invariantes |
| `specs/000-platform/contracts/openapi.yaml` | Contrato HTTP inicial |
| `specs/000-platform/tasks.md` | Backlog de implementação rastreável |
| `specs/000-platform/quickstart.md` | Sequência de preparação e execução local |
| `docs/architecture/architecture.md` | Visão de componentes e fluxos |
| `docs/architecture/adr/` | Registros de decisões arquiteturais |
| `docs/security/threat-model.md` | Ameaças, controles e privacidade |
| `docs/ai/behavior-policy.md` | Limites e comportamento da automação |
| `docs/runbooks/implemented-flows.md` | Guia consolidado do funcionamento implementado |
| `docs/implementation-history.md` | Histórico consolidado de implementação e publicação |
| `docs/testing/strategy.md` | Estratégia de testes e gates |
| `docs/runbooks/webhook-failures.md` | Operação de falhas de webhook |
| `docs/sdd-framework.md` | Framework SDD e skills recomendadas |

## Como rodar localmente

### Requisitos

- Windows 10/11
- .NET SDK 10
- Node.js LTS, que inclui npm
- Git

Docker e necessario para o PostgreSQL local. A conexão do Supabase deve ser fornecida somente por segredo de ambiente.

### Depois de baixar pelo GitHub

```powershell
git clone <URL_DO_REPOSITORIO>
Set-Location WhatsAppAI
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\setup.ps1
.\run.bat
```

O `Set-ExecutionPolicy` vale somente para o terminal atual e nao exige administrador. Se o PowerShell ja permitir scripts, essa linha pode ser omitida.

O setup executa restore, inicia PostgreSQL, compila backend/frontend e roda os testes do frontend. Para preparar mais rapidamente sem os testes:

```powershell
.\setup.ps1 -SkipTests
```

### Enderecos locais

- Aplicacao web: http://localhost:5173
- API: http://localhost:5000
- WhatsApp Web bridge/QR: http://localhost:3020

O bridge do WhatsApp e opcional. O `setup.ps1` instala suas dependencias automaticamente; sem ele, o restante da aplicacao continua funcionando, mas a conexao por QR fica indisponivel.

### Primeiro acesso

Em ambiente de desenvolvimento, o administrador inicial e:

```text
E-mail: admin@platform.com
Senha: Admin@123
```

Entre como `PlatformAdmin`, crie uma empresa e use a senha temporaria exibida no cadastro para acessar o `TenantOwner`. O `TenantOwner` pode convidar ou criar `Operators`.

### Parar a aplicacao

Feche as janelas abertas pelo `run.bat` ou pressione `Ctrl+C` nelas.

### Docker/PostgreSQL

Para um ambiente parecido com producao, use o Compose separadamente:

```powershell
docker compose up -d postgres
```

Esse caminho e opcional e exige Docker Desktop configurado. O desenvolvimento usa a mesma configuração PostgreSQL da produção.

### Supabase (PostgreSQL)

Para conectar a um banco PostgreSQL gerenciado (ex. Supabase, Railway, Render):

#### Setup com Admin Automático (Recomendado)

```powershell
# Rodar setup completo com usuário admin criado
.\setup-supabase-with-admin.ps1

# Ou com parametros
.\setup-supabase-with-admin.ps1 `
  -ProjectUrl "https://xxxx.supabase.co" `
  -ApiKey "your-api-key" `
  -DbPassword "your-db-password"
```

Isso:
1. Testa conexao PostgreSQL
2. Roda EF Core migrations
3. Seedeia admin@platform.com / Admin@123
4. Gera `appsettings.Supabase.json`

#### Start na Aplicacao

```powershell
$env:ASPNETCORE_ENVIRONMENT='Supabase'
dotnet run --project src/WhatsAppAI.WebApi
```

Acesse: http://localhost:5000  
Login: `admin@platform.com` / `Admin@123`

#### Setup Manual (sem Admin)

Se preferir executar passo a passo:

```powershell
# 1. Base setup
.\setup-supabase.ps1

# 2. Admin seed manual (opcional)
psql -h db.xxx.supabase.co -U postgres -d postgres -f setup-supabase-admin.sql
```
