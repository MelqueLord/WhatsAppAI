# Quickstart: validar controles LGPD

**Última validação local:** 2026-08-30
**Status:** Parcialmente validado; T013 permanece aberto até a conclusão dos gates de release.

## Pré-requisitos

- Segredos e conexão Supabase fornecidos localmente/ambiente.
- TenantOwner e dois tenants de teste.

## Validação automatizada

1. Compile a solução em Release: `dotnet build WhatsAppAI.sln --configuration Release`.
2. Execute unitários e arquitetura: `dotnet test WhatsAppAI.sln --configuration Release --no-build`.
3. Execute a suíte de integração em processo separado, com PostgreSQL/Testcontainers e timeout controlado; o comando só é aprovado quando terminar com exit code 0 e relatório completo.
4. Confirme o modelo: `dotnet ef migrations has-pending-model-changes --project src/WhatsAppAI.Infrastructure --startup-project src/WhatsAppAI.WebApi --configuration Release`.
5. Aplique a migration em PostgreSQL vazio e em uma cópia da versão anterior; registre `Up`, rollback e snapshot final.
6. Execute o frontend em `apps/web`: `npm run lint`, `npm test -- --run` e `npm run build`.

### Resultado de 2026-08-30

- Build .NET Release: aprovado, 0 warnings/0 errors.
- Unitários: 340 aprovados.
- Arquitetura: 7 aprovados.
- Frontend: lint, 24 testes e build aprovados.
- Modelo EF/Npgsql: sem alterações pendentes.
- Integração completa: 67/67 aprovados, 0 falhas e 0 ignorados, em 1m40s, após desabilitar hosted workers no fixture HTTP e fornecer um PFX temporário ao host de teste em modo Production.
- Bundle Docker de migrations: imagem Release gerada sem warnings; banco PostgreSQL vazio recebeu as 12 migrations e uma segunda execução terminou sem alterações.
- Startup Docker em Production: API e worker iniciaram com migrations externas; API respondeu 200 em `/health/live` e `/health/ready`.
- O Compose passou a exigir e repassar `BootstrapAdmin__Email` e `BootstrapAdmin__Password`; sem essas variáveis a inicialização falha cedo, conforme esperado.
- Data Protection: PFX temporário montado, key ring gravado no volume compartilhado, leitura confirmada após reinício da API/worker e XML marcado como `encryptedSecret`; os avisos anteriores não reapareceram.
- No smoke anterior, foram observados avisos de binding HTTP explícito e de duas consultas do worker sem `OrderBy`; ambos foram corrigidos depois e exigem repetição do smoke para confirmação operacional.

## Cenário funcional

1. Cadastre uma finalidade não baseada em consentimento e confirme que não há consentimento artificial.
2. Cadastre finalidade baseada em consentimento; registre e revogue evidência.
3. Abra solicitação de portabilidade e obtenha exportação do contato.
4. Abra solicitação de eliminação e execute anonimização.
5. Confirme que nome, telefone e conteúdo não aparecem mais e que tenant diferente permanece intacto.
6. Consulte o aviso público e confirme que somente valores fornecidos pelo ambiente são exibidos.

## Evidências de release

- Resultado de build/testes e migration.
- Captura do aviso com dados institucionais reais (fora do repositório).
- RIPD e checklist revisados por responsável identificado.
- Relatório de isolamento entre tenants e exportação/eliminação LGPD.
- Evidência de backup/restore, `nginx -t`, HTTPS, SignalR e smoke das integrações reais.
