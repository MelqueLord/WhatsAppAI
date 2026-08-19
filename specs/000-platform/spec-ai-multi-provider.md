# Spec: Multi-provedor de IA e tela unificada de configuração

**Status:** Implementado (Fase 10 - T150-T165)
**Depende de:** Plataforma base (T001-T075), Sistema de Planos (T090-T116)
**Refs:** US-004, FR-013, FR-014, FR-021, BR-008

## 1. Problema

Atualmente a plataforma suporta apenas OpenAI como provedor de IA. A tela de configuração de IA (`AiConfigPage`) é limitada a model ID e API key. As configurações de comportamento do bot (mensagens automáticas, fluxo de respostas) ficam em uma tela separada (`BotConfigPage`), fragmentando a experiência do TenantOwner.

O TenantOwner precisa de uma tela única e completa para configurar todo o atendimento automatizado por IA, incluindo a escolha do provedor de IA.

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

### US-AI-002 — Configurar atendimento IA em um só lugar

Como TenantOwner, quero configurar provedor, modelo, credenciais, modo de operação, mensagens automáticas e limites em uma única tela de "Atendimento com IA", para não navegar entre múltiplas páginas.

**Aceite:**
1. A tela unificada contém: provedor, modelo, API key, teste de conexão, modo (Manual/SimpleAutoReply/AiPowered), mensagens (boas-vindas, fallback, handoff, mídia), limites de tokens.
2. A tela de "Fluxo do bot" separada é removida do menu.
3. Configurações são salvas atomicamente.
4. O modo Manual desabilita campos específicos de IA sem ocultá-los.

### US-AI-003 — Testar conexão por provedor

Como TenantOwner, quero testar a conexão com o provedor de IA escolhido para verificar se a credencial está correta antes de ativar o atendimento automático.

**Aceite:**
1. O teste envia uma requisição mínima ao provedor selecionado.
2. Sucesso/falha identifica a etapa sem revelar credenciais.
3. Cada provedor tem validação específica (endpoint, formato de resposta).

## 4. Requisitos funcionais

- **FR-AI-001:** a entidade `AiProviderCredential` já suporta campo `Provider` (string). O sistema deve aceitar os valores `openai`, `gemini`, `anthropic`, `xiaomi`.
- **FR-AI-002:** implementar adaptadores `IAiProvider` para Gemini, Anthropic e Xiaomi, além do OpenAI existente.
- **FR-AI-003:** a tela unificada substitui `AiConfigPage` e `BotConfigPage`. O menu lateral exibe apenas "Atendimento com IA".
- **FR-AI-004:** credenciais são armazenadas por provedor no `ISecretStore` com chave `ai:{tenantId}:{provider}:apikey`.
- **FR-AI-005:** o `BotConfiguration` (modo, mensagens, limites) permanece como entidade separada mas é editado na mesma tela.
- **FR-AI-006:** o seletor de provedor deve exibir nome amigável e ícone/distintivo de cada provedor.
- **FR-AI-007:** modelos sugeridos devem ser carregados por provedor (lista estática ou configuração).

## 5. Regras de negócio

- **BR-AI-001:** apenas um provedor de IA pode estar ativo por tenant por vez.
- **BR-AI-002:** trocar de provedor não apaga credenciais de provedores anteriores (permite alternância).
- **BR-AI-003:** em modo Manual, as configurações de IA ficam salvas mas inativas.
- **BR-AI-004:** provedor com credencial inválida ou ausente não bloqueia o modo Manual ou SimpleAutoReply.
- **BR-AI-005:** o teste de conexão deve usar o adaptador correto conforme o provedor selecionado.

## 6. Adaptadores por provedor

Cada provedor implementa `IAiProvider` com sua API específica:

| Provedor | Endpoint base | Autenticação | Formato |
|----------|--------------|--------------|---------|
| OpenAI | `api.openai.com/v1/responses` | Bearer token | Responses API |
| Gemini | `generativelanguage.googleapis.com/v1beta` | API key query param | GenerateContent |
| Anthropic | `api.anthropic.com/v1/messages` | `x-api-key` header | Messages API |
| Xiaomi | `api.xiaomi.com/v1/chat/completions` (ou endpoint MiMo) | Bearer token | Chat Completions compatível |

Todos os adaptadores convertem sua resposta nativa para o formato comum `AiResponse` com `AiDecision`.

## 7. Frontend — Tela unificada

### Estrutura da tela

```
┌─────────────────────────────────────────────────┐
│  Atendimento com IA                             │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌─ Provedor de IA ─────────────────────────┐   │
│  │  [OpenAI] [Gemini] [Anthropic] [Xiaomi]  │   │
│  │  Modelo: [dropdown por provedor]         │   │
│  │  API Key: [••••••••]                     │   │
│  │  [Salvar] [Testar Conexão]              │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌─ Modo de operação ──────────────────────┐   │
│  │  ○ Manual  ○ SimpleAutoReply  ○ IA      │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌─ Mensagens automáticas ─────────────────┐   │
│  │  Boas-vindas: [textarea]                 │   │
│  │  Fallback: [textarea]                    │   │
│  │  Handoff: [textarea]                     │   │
│  │  Mídia: [textarea]                       │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌─ Limites ───────────────────────────────┐   │
│  │  Max tokens/resposta: [number]           │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Rota e menu

- Rota: `/integrations/ai` (substitui a atual)
- Menu lateral: "Atendimento com IA" (substitui "Configuração de IA" e "Fluxo do bot")
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
| **WebApi/Bot/** | `BotConfigurationEndpoints.cs` | Pode ser removido ou mesclado nos endpoints de IA |
| **Frontend** | `AiConfigPage.tsx` | Reescrever com seletor de provedor e seções unificadas |
| **Frontend** | `BotConfigPage.tsx` | Remover; conteúdo mesclado em AiConfigPage |
| **Frontend** | `App.tsx` + `Sidebar.tsx` | Remover rota `/bot-config`, atualizar menu |

### Entidades não afetadas

- `AiProviderCredential`: já tem campo `Provider`, não precisa de migration.
- `BotConfiguration`: permanece como entidade separada no banco.
- `AiInteraction`: registra provider usado, já compatível.

## 9. Critérios de sucesso

1. TenantOwner vê 4 provedores na tela e consegue configurar qualquer um.
2. Teste de conexão funciona para cada provedor.
3. IA responde corretamente usando qualquer provedor configurado.
4. Tela de "Fluxo do bot" não existe mais no menu.
5. Configurações de modo, mensagens e limites estão na tela de IA.
6. Trocar de provedor não perde configurações de mensagens.
7. Modo Manual funciona sem provedor configurado.
