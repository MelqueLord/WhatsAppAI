# Quickstart de desenvolvimento

Este documento define a sequência esperada; os comandos executáveis serão confirmados na **T001–T003**.

## Pré-requisitos

- Git
- .NET SDK 10 compatível com `global.json`
- Node.js LTS e gerenciador de pacotes fixado no repositório
- Docker com Compose
- HTTPS local confiável para testes de webhook, quando necessário

## Execucao local sem administrador

Para executar em uma maquina Windows sem permissao de administrador, instale o .NET SDK 10 e o Node.js LTS no escopo do usuario e execute:

```powershell
.\setup.ps1
.\run.bat
```

Esse caminho inicia PostgreSQL em Docker e sobe a API em `http://localhost:5000` e o frontend em `http://localhost:5173`.

## Preparação com Docker/PostgreSQL

Copie `.env.example` para um `.env` local ignorado, substitua a senha de exemplo e execute:

```bash
docker compose up -d postgres
docker compose ps postgres
dotnet user-secrets --project src/WhatsAppAI.WebApi set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=whatsappai;Username=whatsappai;Password=<senha-local>"
dotnet user-secrets --project src/WhatsAppAI.WebApi set "BootstrapAdmin:Email" "admin@seu-dominio.com"
dotnet user-secrets --project src/WhatsAppAI.WebApi set "BootstrapAdmin:Password" "<senha-forte-com-12-ou-mais-caracteres>"
dotnet restore
dotnet run --project src/WhatsAppAI.WebApi
```

Supabase e PostgreSQL Docker usam o mesmo provider Npgsql e a mesma cadeia de migrations.

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

## Provedores de IA

A plataforma suporta os provedores registrados no catálogo de IA (atualmente OpenAI, Gemini, Anthropic, Xiaomi MiMo, xAI Grok e Groq). Para usar IA, o tenant precisa ter plano `IA+BOT` e configurar pelo menos um provedor na tela "Atendimento com IA":

| Provedor | Identificador | Exemplo de modelo |
|----------|--------------|-------------------|
| OpenAI | `openai` | gpt-4o-mini |
| Google Gemini | `gemini` | gemini-3.6-flash |
| Anthropic | `anthropic` | claude-sonnet-4-20250514 |
| Xiaomi MiMo | `xiaomi` | mimo-v2.5 |

A API key é criptografada no banco via `ISecretStore`. O endpoint `GET /api/integrations/ai/providers` lista provedores disponíveis com modelos sugeridos.

## Segredos locais

Não versionar credenciais. Usar `dotnet user-secrets` no backend e um `.env.local` ignorado para valores públicos do frontend. Tokens Meta/OpenAI nunca devem estar no bundle da SPA.

## Memória do cliente com consentimento

1. O contato deve responder exatamente `SIM` quando receber a solicitação de consentimento para atendimento automatizado por IA.
2. Em **Contatos > Editar**, use **Memória do cliente** para registrar somente um fato curto confirmado pelo cliente, como preferência de atendimento.
3. A memória fica vinculada à empresa e ao contato, expira conforme a finalidade de IA (365 dias por padrão) e pode ser removida pelo Operator ou TenantOwner.
4. Revogar o consentimento bloqueia a memória no próximo atendimento; a IA não cria memórias automaticamente e a anonimização redige todas as entradas do contato.

## Aprendizado supervisionado por empresa

1. No Inbox, o operador pode marcar uma resposta da IA como útil ou informar uma correção.
2. Uma resposta útil ensina o agente daquela empresa sobre estilo e fluxo. Uma correção só é aprendida quando a resposta corrigida é informada; uma observação sem resposta não vira exemplo.
3. Os exemplos aprendidos aparecem em **Exemplos de atendimento** com a etiqueta **Aprendido com operador**. O TenantOwner pode editar, desativar ou reativar cada um.
4. O aprendizado é isolado por empresa e não altera o modelo global. Preços, horários, políticas e demais fatos continuam dependendo da Base de Conhecimento.

## Decisão de encaminhamento

1. Cadastre somente filas ativas que a IA pode usar e selecione-as na configuração do provedor; palavras-chave e escolhas de fila atribuem a conversa sem desligar a IA.
2. Enquanto a conversa estiver em uma fila no modo automático, a IA continua respondendo com as diretrizes, a base de conhecimento e o histórico recente.
3. O modo humano só é ativado por pedido explícito do cliente, risco/lacuna específica que não possa ser respondida com segurança ou ação manual do operador.
4. Fila inexistente, inativa ou não selecionada é ignorada pelo backend e não altera a conversa.

## Respostas naturais

O agente responde como parte da equipe da empresa: começa pela resposta mais útil, usa o histórico para continuar o assunto, não repete saudações ou perguntas já respondidas e faz no máximo uma pergunta curta de continuidade. O tom selecionado no perfil orienta a forma da conversa, mas não autoriza inventar fatos. Todas as respostas automáticas permanecem limitadas a 160 caracteres.

## Segurança contra informação inventada

Antes de enviar, o backend verifica valores concretos da resposta — preço, horário, prazo, percentual, data, link e contato — contra as informações autorizadas da empresa. Se um valor não estiver cadastrado, ele não é enviado: a conversa segue o handoff seguro. A exceção é uma pergunta genérica com pesquisa pública explicitamente permitida, sem transformar informação pública em fato da empresa.

## Verificação antes de commit

```bash
dotnet format --verify-no-changes
dotnet build --no-restore
dotnet test --no-build
cd apps/web && npm run lint && npm run test && npm run build
```

O bootstrap pode ajustar comandos, mas deve manter um único comando de CI reproduzível e documentado.
