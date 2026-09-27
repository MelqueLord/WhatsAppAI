# Quickstart de validação: Cadastro de templates na Meta

Este guia define as evidências necessárias depois da implementação. Ele não deve ser executado contra produção nem com conteúdo real de clientes.

## Pré-requisitos

- SDK .NET definido em `global.json` e Node/npm do projeto.
- PostgreSQL de teste disponível.
- Dois tenants ativos, cada um com TenantOwner.
- Tenant A com duas linhas oficiais na mesma WABA; Tenant B com outra WABA.
- Token de staging do Tenant A com `whatsapp_business_management` e `whatsapp_business_messaging`.
- App Meta de staging inscrito em `message_template_status_update`, `template_category_update` e `message_template_components_update`.
- Exemplos das variáveis devem ser fictícios e não conter dados pessoais reais.

## 1. Gates locais

```powershell
dotnet format WhatsAppAI.sln --verify-no-changes
dotnet build WhatsAppAI.sln -c Release
dotnet test tests/WhatsAppAI.UnitTests/WhatsAppAI.UnitTests.csproj -c Release
dotnet test tests/WhatsAppAI.ArchitectureTests/WhatsAppAI.ArchitectureTests.csproj -c Release
dotnet test tests/WhatsAppAI.IntegrationTests/WhatsAppAI.IntegrationTests.csproj -c Release
npm --prefix apps/web run lint
npm --prefix apps/web test
npm --prefix apps/web run build
git diff --check
```

Esperado: zero falhas e zero warnings novos do projeto.

## Evidência da implementação local (2026-09-27)

- `dotnet build WhatsAppAI.sln -c Debug --no-restore`: concluído. Há conflitos de versão OpenTelemetry já existentes no projeto de testes de arquitetura.
- `dotnet test tests/WhatsAppAI.UnitTests/WhatsAppAI.UnitTests.csproj -c Release --no-build`: 559 aprovados.
- `dotnet test tests/WhatsAppAI.ArchitectureTests/WhatsAppAI.ArchitectureTests.csproj -c Debug --no-build`: 7 aprovados.
- `dotnet ef migrations has-pending-model-changes`: nenhum modelo pendente, com connection string local de design-time.
- testes focais de página, rota e navegação de templates: 6 aprovados; build Vite aprovado.
- lint do frontend aprovado após separar a publicação de notificações do componente React.
- os testes de persistência, endpoint e webhook de templates foram adicionados e compilam; dependem do PostgreSQL iniciado pelo Testcontainers.
- a suíte de integração exige Docker/Testcontainers e não pôde iniciar nesta máquina, pois o endpoint `npipe://./pipe/docker_engine` não estava disponível.

Risco residual: os cenários de integração contra PostgreSQL, as chamadas reais da Meta em staging e os webhooks assinados ainda precisam ser executados no ambiente com Docker e credenciais de staging. As tarefas de cobertura de integração permanecem abertas em `tasks.md`.

## 2. Migration e isolamento da WABA

1. Aplicar migrations em banco vazio.
2. Aplicar em snapshot com duas linhas do Tenant A usando a mesma WABA.
3. Confirmar uma única `WhatsAppBusinessAccount` e duas linhas relacionadas.
4. Tentar associar a mesma WABA ao Tenant B.
5. Executar `Down` em banco descartável e reaplicar `Up`.

Esperado: agrupamento correto no Tenant A; conflito entre tenants falha explicitamente; reversão e reaplicação preservam consistência.

## 3. Cadastro válido

Como TenantOwner A, submeter pela linha oficial 1:

```json
{
  "name": "atualizacao_atendimento_staging",
  "language": "pt_BR",
  "category": "UTILITY",
  "bodyText": "Olá, {{1}}. Seu protocolo {{2}} foi atualizado.",
  "bodyExamples": ["Maria", "ATD-123"],
  "footerText": "Equipe de atendimento"
}
```

Esperado:

- endpoint retorna `202` com IDs internos e `QUEUED`;
- consulta pela linha 2 mostra a mesma variante, pois o catálogo pertence à WABA;
- worker envia um único POST à Meta;
- estado passa a `ACCEPTED` e revisão a `PENDING` ou ao estado retornado;
- nenhum token, body completo ou exemplo aparece nos logs.

## 4. Validação e autorização

Repetir com estes casos:

- Operator em vez de TenantOwner;
- linha QR Code;
- linha de outro tenant ou acima da quota;
- nome com maiúsculas/espaços;
- placeholders `{{1}}` e `{{3}}` sem `{{2}}`;
- dois placeholders e um exemplo;
- body com mais de 1.024 caracteres;
- categoria `AUTHENTICATION`.

Esperado: nenhuma submissão durável nem chamada externa é criada.

## 5. Idempotência

1. Enviar duas vezes o mesmo payload com a mesma `Idempotency-Key`.
2. Reusar a chave com outro body.
3. Enviar o mesmo nome/idioma com uma chave diferente.

Esperado: o primeiro caso retorna a mesma operação; o segundo retorna `409`; o terceiro reconcilia ou informa conflito sem POST duplicado.

## 6. Timeout após possível aceite

Configurar o contrato Meta de teste para aceitar a requisição e encerrar a conexão antes da resposta.

Esperado:

1. submissão entra em `OUTCOME_UNKNOWN`;
2. worker consulta por nome e idioma;
3. item remoto compatível é adotado e vinculado ao ID externo;
4. nenhum segundo POST ocorre;
5. conteúdo divergente resulta em `NEEDS_ATTENTION`.

## 7. Webhook por WABA

Enviar payloads assinados contendo:

- aprovação e rejeição;
- categoria alterada;
- atualização de componentes;
- dois `entry/change` no mesmo POST;
- o mesmo fragmento duas vezes;
- WABA desconhecida;
- evento antigo após evento novo.

Esperado: cada fragmento é persistido uma vez, somente o tenant proprietário é alterado, WABA desconhecida fica em quarentena e possível regressão sem ordem confiável provoca sincronização autoritativa.

## 8. Sincronização completa

Simular catálogo com mais de uma página e itens criados no WhatsApp Manager.

Esperado: todas as páginas são aplicadas; variantes de idioma permanecem separadas; falha na página intermediária não marca itens ausentes; uma nova execução converge sem duplicar.

## 9. Regressão de envio

Validar:

- Inbox permite individualmente apenas Utilidade/Marketing aprovados e compatíveis;
- broadcast permite apenas Utilidade aprovada e compatível;
- status pendente/rejeitado/pausado permanece bloqueado;
- backend continua consultando a Meta antes de enfileirar envio;
- QR Code continua rejeitando templates.

## 10. Evidência de staging

Anexar ao incremento:

- execução dos testes e migration;
- print sanitizado do catálogo com ciclo `PENDING -> APPROVED` ou `REJECTED`;
- confirmação das inscrições de webhook;
- evidência de que o token possui as permissões necessárias, sem revelar o token;
- correlação dos logs sanitizados da submissão e do webhook;
- riscos residuais e rollback da migration.
