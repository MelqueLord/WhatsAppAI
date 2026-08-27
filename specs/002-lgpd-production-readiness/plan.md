# Implementation Plan: LGPD Production Readiness

**Branch**: `master` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-lgpd-production-readiness/spec.md`

## Summary

Adicionar um módulo leve de privacidade ao monólito existente: finalidades/base legal, evidência de consentimento quando aplicável, solicitações de titulares e anonimização transacional. Publicar aviso de privacidade configurável e registrar as evidências de governança em documentação versionada. O desenho reutiliza autenticação, `ICurrentTenant`, EF Core, auditoria e Minimal APIs existentes.

## Technical Context

**Language/Version**: C# / .NET 10 LTS

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 10, Npgsql/Supabase, xUnit

**Storage**: PostgreSQL gerenciado pelo Supabase para homologação/candidato atual; migrations dedicadas Npgsql

**Testing**: xUnit unitário e integração SQLite para fluxo HTTP; validação de migration/modelo Npgsql

**Target Platform**: Linux containerizado; configuração e segredos fornecidos pelo ambiente

**Project Type**: Aplicação web em monólito modular

**Performance Goals**: Exportação de até 10 mil mensagens por contato sem carregar dados de outro tenant; exclusão em transação única

**Constraints**: Menor diff; nenhuma dependência nova; nenhum PII em auditoria; não interromper atendimento por ausência de metadados institucionais

**Scale/Scope**: Até 50 tenants no MVP; operação manual por TenantOwner

## Constitution Check

*GATE: aprovado antes e depois do desenho.*

- Isolamento: todas as novas entidades carregam `TenantId`, têm filtro global e os endpoints reafirmam o tenant.
- Segredos/PII: aviso usa configuração ambiental; auditoria registra IDs/ações, nunca conteúdo exportado ou eliminado.
- Dependências: Domain e Application não recebem SDK externo; nenhuma biblioteca nova.
- Banco: migration PostgreSQL reversível e teste de isolamento fazem parte do incremento.
- Operação: anonimização é transacional e idempotente; falhas não deixam uma solicitação como concluída.
- Estrutura: módulo `Privacy` segue as camadas existentes; ADR-0008 registra responsabilidades e decisões.

## Project Structure

### Documentation (this feature)

```text
specs/002-lgpd-production-readiness/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/privacy-api.yaml
└── tasks.md
```

### Source Code (repository root)

```text
src/WhatsAppAI.Domain/Privacy/
src/WhatsAppAI.Infrastructure/Persistence/Configurations/
src/WhatsAppAI.WebApi/Privacy/
tests/WhatsAppAI.UnitTests/Privacy/
tests/WhatsAppAI.IntegrationTests/Privacy/
docs/security/
docs/architecture/adr/0008-lgpd-operational-controls.md
```

**Structure Decision**: Reutilizar o monólito modular; regras ficam no Domain, persistência no Infrastructure e orquestração HTTP fina na WebApi.

## Complexity Tracking

Nenhuma violação constitucional ou nova infraestrutura.
