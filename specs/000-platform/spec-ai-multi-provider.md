# Spec: Multi-provedor de IA e configuração separada por plano

**Status:** Implementado (Fase 10 - T150-T165)
**Depende de:** Plataforma base (T001-T075), Sistema de Planos (T090-T116)
**Refs:** US-004, FR-013, FR-014, FR-021, BR-008

> **Correção de decisão (2026-08-27):** BOT e IA são configurações separadas. O plano BOT deve operar sem provedor de IA; o plano BOT + IA adiciona a configuração de provedor, credencial, diretrizes, confiança e handoff da IA. A separação deve ser preservada na UI, nos contratos e nas permissões, sem misturar capacidades entre os pacotes.

## 1. Problema

Atualmente a plataforma suporta apenas OpenAI como provedor de IA. A tela de configuração de IA (`AiConfigPage`) é limitada a model ID e API key. As configurações de comportamento do bot (mensagens automáticas, fluxo de respostas) ficam em uma tela separada (`BotConfigPage`), fragmentando a experiência do TenantOwner.

O TenantOwner precisa de uma tela própria para configurar o atendimento automatizado por IA quando o tenant possui o plano BOT + IA. O fluxo do BOT continua em tela própria e deve funcionar também no plano BOT, sem exigir provedor ou credencial de IA.

## 2. Provedores suportados

| Provedor | Identificador | Exemplos de modelos |
|----------|--------------|---------------------|
| OpenAI | `openai` | gpt-4o, gpt-4o-mini, gpt-4.1-mini |
| Google Gemini | `gemini` | gemini-3.1-pro-preview, gemini-3.6-flash |
| Anthropic | `anthropic` | claude-sonnet-4, claude-haiku-3.5 |
| Xiaomi MiMo | `xiaomi` | mimo-v2.5-pro, mimo-v2.5 |

## 3. Histórias de usuário

### US-AI-001 — Escolher provedor de IA

Como TenantOwner, quero escolher entre OpenAI, Gemini, Anthropic ou Xiaomi como provedor de IA do meu atendimento, para usar o modelo que melhor atende meu caso e orçamento.

**Aceite:**
1. A tela exibe um seletor de provedor com as 4 opções.
2. Ao selecionar um provedor, os campos de modelo sugerem modelos compatíveis.
3. Cada provedor tem sua própria credencial (API key) armazenada.
4. Apenas um provedor pode estar ativo por vez.
5. A troca de provedor preserva o histórico de interações anteriores.

### US-AI-002 — Configurar atendimento IA na tela própria do pacote IA

Como TenantOwner do plano BOT + IA, quero configurar provedor, modelo, credenciais, diretrizes, confiança e handoff na tela de "Atendimento com IA", mantendo o fluxo e as mensagens do BOT na tela própria do BOT.

**Aceite:**
1. A tela de IA contém: provedor, modelo, API key, teste de conexão, diretrizes, limiar de confiança e handoff.
2. A tela de "Fluxo do bot" permanece disponível nos planos BOT e BOT + IA para modo, mensagens e fluxo do BOT.
3. Os contratos e salvamentos de BOT e IA permanecem separados, sem sobrescrever configurações da outra capacidade.
4. O BOT funciona sem provedor ou credencial de IA.

### US-AI-003 — Testar conexão por provedor

Como TenantOwner, quero testar a conexão com o provedor de IA escolhido para verificar se a credencial está correta antes de ativar o atendimento automático.

**Aceite:**
1. O teste envia uma requisição mínima ao provedor selecionado.
2. Sucesso/falha identifica a etapa sem revelar credenciais.
3. Cada provedor tem validação específica (endpoint, formato de resposta).

## 4. Requisitos funcionais

- **FR-AI-001:** a entidade `AiProviderCredential` já suporta campo `Provider` (string). O sistema deve aceitar os valores `openai`, `gemini`, `anthropic`, `xiaomi`, `grok`.
- **FR-AI-002:** implementar adaptadores `IAiProvider` para Gemini, Anthropic e Xiaomi, além do OpenAI existente.
- **FR-AI-003:** `AiConfigPage` e `BotConfigPage` permanecem distintas. A primeira exige a capacidade `aiEnabled`; a segunda está disponível nos planos BOT e BOT + IA.
- **FR-AI-004:** credenciais são armazenadas por provedor no `ISecretStore` com chave `ai:{tenantId}:{provider}:apikey`.
- **FR-AI-005:** o `BotConfiguration` (modo, mensagens e fluxo) permanece como entidade e tela separadas; diretrizes, confiança e credenciais pertencem à configuração de IA.
- **FR-AI-006:** o seletor de provedor deve exibir nome amigável e ícone/distintivo de cada provedor.
- **FR-AI-007:** modelos sugeridos devem ser carregados por provedor (lista estática ou configuração).
- **FR-AI-008:** as diretrizes da IA devem permitir selecionar filas ativas do tenant para encaminhamento automático conforme a escolha ou intenção expressa pelo cliente.
- **FR-AI-009:** as diretrizes da IA devem permitir selecionar tags ativas do tenant para categorização automática do contato conforme o conteúdo da conversa.
- **FR-AI-010:** o consumo real de tokens deve ser registrado por tenant, provedor e modelo, separando entrada e saída para permitir estimativa de custo sem expor prompts ou credenciais.

## 5. Regras de negócio

- **BR-AI-001:** apenas um provedor de IA pode estar ativo por tenant por vez.
- **BR-AI-002:** trocar de provedor não apaga credenciais de provedores anteriores (permite alternância).
- **BR-AI-003:** em modo Manual, as configurações de IA ficam salvas mas inativas.
- **BR-AI-004:** provedor com credencial inválida ou ausente não bloqueia o modo Manual ou SimpleAutoReply.
- **BR-AI-005:** o teste de conexão deve usar o adaptador correto conforme o provedor selecionado.
- **BR-AI-006:** o PlatformAdmin controla a franquia de respostas por empresa; tokens servem para medir e distribuir custo, sem substituir o limite comercial de respostas.

## 6. Adaptadores por provedor

Cada provedor implementa `IAiProvider` com sua API específica:

| Provedor | Endpoint base | Autenticação | Formato |
|----------|--------------|--------------|---------|
| OpenAI | `api.openai.com/v1/responses` | Bearer token | Responses API |
| Gemini | `generativelanguage.googleapis.com/v1beta` | API key query param | GenerateContent |
| Anthropic | `api.anthropic.com/v1/messages` | `x-api-key` header | Messages API |
| Xiaomi | `api.xiaomi.com/v1/chat/completions` (ou endpoint MiMo) | Bearer token | Chat Completions compatível |
| Grok | `api.x.ai/v1/chat/completions` | Bearer token | Chat Completions compatível |

Todos os adaptadores convertem sua resposta nativa para o formato comum `AiResponse` com `AiDecision`.

## 7. Frontend — Configuração separada por plano

### Estrutura das telas

`/integrations/ai` (somente BOT + IA) concentra provedor, modelo, credencial, diretrizes, limiar de confiança e handoff. `/bot-config` (BOT e BOT + IA) concentra modo, mensagens automáticas e fluxo do BOT. Nenhuma tela deve exigir a configuração da outra.

```
BOT + IA — /integrations/ai
  Provedor, modelo, credencial, diretrizes, confiança e handoff

BOT / BOT + IA — /bot-config
  Modo, mensagens automáticas, fluxo e fallback do BOT
```

### Rota e menu

- Rota IA: `/integrations/ai`, protegida por `aiEnabled`
- Rota BOT: `/bot-config`, disponível nos dois planos
- Menu lateral: exibe cada entrada conforme a capacidade do plano
- Guard: `OwnerRoute` + verificação de plano `aiEnabled`

## 8. Impacto na implementação atual

### Alterações necessárias

| Camada | Arquivo/Área | Mudança |
|--------|-------------|---------|
| **Infrastructure/OpenAI/** | `OpenAiProvider.cs` | Já existe. Renomear namespace se necessário. |
| **Infrastructure/Gemini/** | Novo | Implementar `GeminiProvider : IAiProvider` |
| **Infrastructure/Anthropic/** | Novo | Implementar `AnthropicProvider : IAiProvider` |
| **Infrastructure/Xiaomi/** | Novo | Implementar `XiaomiProvider : IAiProvider` |
| **Infrastructure/OpenAI/** | DI extensions | Registrar todos os provedores; resolver por nome |
| **WebApi/Integrations/** | `AiProviderEndpoints.cs` | Aceitar `provider` no request; listar modelos por provedor |
| **WebApi/Bot/** | `BotConfigurationEndpoints.cs` | Permanece separado e disponível para os dois planos |
| **Frontend** | `AiConfigPage.tsx` | Reescrever com seletor de provedor e seções próprias de IA |
| **Frontend** | `BotConfigPage.tsx` | Permanece como tela do BOT |
| **Frontend** | `App.tsx` + `Sidebar.tsx` | Manter rotas e aplicar visibilidade por capacidade |

### Entidades não afetadas

- `AiProviderCredential`: já tem campo `Provider`, não precisa de migration.
- `BotConfiguration`: permanece como entidade separada no banco.
- `AiInteraction`: registra provider usado, já compatível.

## 9. Critérios de sucesso

1. TenantOwner vê 4 provedores na tela e consegue configurar qualquer um.
2. Teste de conexão funciona para cada provedor.
3. IA responde corretamente usando qualquer provedor configurado.
4. A tela de IA não aparece no plano BOT.
5. A tela de BOT aparece nos dois planos e funciona sem provedor configurado.
6. Alterar provedor/diretrizes não altera modo ou mensagens do BOT.
7. Handoff e fallback da IA usam apenas configurações do plano BOT + IA.
