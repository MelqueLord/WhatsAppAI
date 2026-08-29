# Especificação: Sistema de Planos e Gestão de Empresas

**Status:** Implementado  
**Versão:** 2.0.0
**Data:** 2026-08-16
**Spec relacionada:** `spec.md` (plataforma base)
**Implementado em:** Fase 9 (T090-T112)

## 1. Problema

Atualmente, a plataforma provisiona tenants manualmente pelo PlatformAdmin. É necessário um modelo de planos que permita:

1. Diferenciar funcionalidades disponíveis por tipo de plano
2. Automatizar o cadastro de empresas com plano selecionado
3. Controlar acesso baseado no plano contratado

## 2. Atores

- **PlatformAdmin:** gerencia planos, aprova cadastros, monitora adesão
- **TenantOwner:** cadastra empresa, seleciona plano, configura ambiente
- **Operator:** atende conversas dentro das funcionalidades do plano

## 3. Tipos de Plano

| Plano | Linhas oficiais | Operators | IA/mês padrão | Recursos implementados |
|---|---:|---:|---:|---|
| STAR | 1 | 2 | 1.500 | Inbox, dashboard, histórico, atendimento compartilhado, IA e conhecimento |
| FLOW | 2 | 4 | 5.000 | Tudo do STAR, BOT, tags e filas/distribuição |
| SCALA | 3 | 8 | 12.000 | Todos os recursos implementados do FLOW e maior capacidade operacional |

Os valores de IA são sugestões iniciais e podem ser personalizados pelo PlatformAdmin em cada tenant. Pipeline, relatório avançado e respostas rápidas anunciados comercialmente ainda não possuem módulos implementados e não devem aparecer como permissão ativa até entrega própria especificada.

`BOT` e `IA_BOT` permanecem somente para compatibilidade de tenants existentes e não aparecem como opção para novos cadastros.

## 4. Histórias de Usuário

### US-P001 — Cadastrar empresa com plano (P1)

Como PlatformAdmin, quero cadastrar uma empresa selecionando um plano para provisionar o tenant com as funcionalidades corretas.

**Aceite:**

1. PlatformAdmin seleciona STAR, FLOW ou SCALA ao criar tenant
2. Plano é persistido no tenant e controla funcionalidades disponíveis
3. Tenant criado recebe status `Pending` até ativação pelo TenantOwner
4. Link de ativação é gerado para envio manual

### US-P002 — Ativar empresa e configurar ambiente (P1)

Como TenantOwner, quero ativar minha empresa e configurar o ambiente conforme o plano contratado.

**Aceite:**

1. Ativação define senha e ativa TenantOwner
2. Interface mostra apenas funcionalidades do plano contratado
3. Configurações obrigatórias são guiadas conforme as permissões efetivas retornadas no login
4. Recursos não contratados não aparecem na navegação e são bloqueados no backend

### US-P003 — Gerenciar operadores (P1)

Como TenantOwner, quero convidar e gerenciar operadores para atendimento humano.

**Aceite:**

1. Todos os planos comerciais permitem gestão de operadores
2. Operadores têm acesso à inbox e modos de conversa
3. STAR permite até 2, FLOW até 4 e SCALA até 8 Operators
4. A IA responde até a franquia personalizada do tenant

### US-P004 — Configurar atendimento (P1)

Como TenantOwner, quero configurar o comportamento do atendimento conforme meu plano.

**Aceite:**

1. Ambos os planos: configuração de WhatsApp, mensagens de boas-vindas e fallback
2. Plano IA+BOT: configuração adicional de OpenAI, conhecimento para IA, behavior policy
3. Configurações são persistidas e afetam comportamento imediato

### US-P005 — Visualizar funcionalidades do plano (P1)

Como TenantOwner, quero ver claramente quais funcionalidades meu plano inclui.

**Aceite:**

1. Dashboard mostra plano atual e funcionalidades incluídas
2. Funcionalidades não disponíveis estão desabilitadas com indicação do plano necessário
3. Upgrade de plano é possível (futuro)

## 5. Requisitos Funcionais

### Plano e Tenant

- **FR-P001:** PlatformAdmin pode criar tenant selecionando STAR, FLOW ou SCALA
- **FR-P002:** Plano é persistido em `Tenant` e controla acesso a funcionalidades
- **FR-P003:** STAR habilita IA e desabilita BOT, tags e filas/distribuição
- **FR-P004:** FLOW e SCALA habilitam IA, BOT, tags e filas/distribuição implementadas
- **FR-P005:** Plano pode ser alterado por PlatformAdmin (upgrade/downgrade com validação)

### Cadastro de Empresa

- **FR-P006:** Cadastro de empresa cria Tenant + TenantOwner + Invitation atomicamente
- **FR-P007:** Status inicial do tenant é `Pending` até ativação
- **FR-P008:** Link de ativação é retornado uma única vez para envio manual
- **FR-P009:** Ativação define senha e ativa User/Membership/Tenant

### Configuração por Plano

- **FR-P010:** Interface filtra funcionalidades com permissões efetivas retornadas pelo backend
- **FR-P011:** Endpoints de IA validam plano antes de executar
- **FR-P012:** Configuração de WhatsApp é obrigatória para ambos os planos
- **FR-P013:** Configuração de provedor de IA é obrigatória nos três planos comerciais quando a IA estiver ativa

### Operadores

- **FR-P014:** Gestão de operadores disponível para ambos os planos
- **FR-P015:** Ambos os planos permitem criação de memberships Operator
- **FR-P016:** Limites de linhas e Operators são provisionados automaticamente pelo plano
- **FR-P017:** Franquia mensal de respostas de IA é inicializada pelo plano e personalizável por tenant

## 6. Regras de Negócio

- **BR-P001:** Um tenant possui exatamente um plano ativo
- **BR-P002:** Planos comerciais habilitam IA; BOT legado não pode usar IA
- **BR-P003:** Plano BOT pode usar todos os modos de conversa (Automatic, Human, Paused) para resposta humana
- **BR-P004:** Mudança de plano preserva dados existentes mas altera funcionalidades disponíveis
- **BR-P005:** Downgrade de IA+BOT para BOT desabilita IA mas preserva histórico de AiInteraction
- **BR-P006:** Cadastro de empresa exige seleção de plano obrigatória
- **BR-P007:** BOT legado não requer configuração de IA
- **BR-P008:** apenas respostas válidas da IA enfileiradas consomem franquia; entradas, simulações, falhas, fallback e handoff não consomem
- **BR-P009:** franquia esgotada bloqueia o provedor e aplica handoff/fallback seguro

## 7. Modelo de Dados

### Entidade: SubscriptionPlan

```
id, name, code, description, features_json, max_operators, max_knowledge_items, 
is_active, created_at, updated_at
```

**Códigos selecionáveis:** `STAR`, `FLOW`, `SCALA`
**Códigos legados:** `BOT`, `IA_BOT`

**features_json:** JSON com funcionalidades habilitadas:
```json
{
  "ai_enabled": false,
  "openai_required": false,
  "ai_metrics": false
}
```

Além dos campos existentes, o plano persiste `is_selectable`, permissões de BOT/tags/distribuição e padrões de linhas, Operators e franquia de IA.

### Alteração em Tenant

Adicionar campos:
- `plan_id` (FK para SubscriptionPlan)
- `plan_activated_at`
- `plan_expires_at` (futuro, para trial/pagamento)
- `monthly_ai_response_limit` (personalizado; `null` apenas para compatibilidade legada)

### Alteração em TenantMembership

Nenhuma alteração necessária - ambos os planos permitem operadores.

## 8. Interface

### Página de Cadastro de Empresa (PlatformAdmin)

1. Nome da empresa
2. Nome do TenantOwner
3. E-mail do TenantOwner
4. Seleção de STAR, FLOW ou SCALA; linhas e Operators são preenchidos automaticamente
5. Franquia mensal de respostas da IA preenchida pelo plano e editável
6. Botão "Criar Empresa"
7. Senha temporária exibida uma única vez

### Dashboard do TenantOwner

- Indicação do plano atual
- Cards de funcionalidades habilitadas/desabilitadas
- Guia de configuração obrigatória

### Menu Lateral

**STAR:** Dashboard, Inbox, Operadores, WhatsApp, IA, Conhecimento e Uso.
**FLOW/SCALA:** recursos do STAR mais BOT, Tags e Filas.

## 9. Dependências

- Plataforma base (spec.md) implementada
- US-001 (Provisionar cliente) estendido
- US-009 (Gerenciar Operators) condicionado ao plano

## 10. Fora do Escopo (MVP)

- Pagamento/recorrência de planos
- Trial period
- Upgrade/downgrade self-service
- Criação arbitrária de novos planos pela interface
- Cobrança automática de excedentes
- Marketplace de planos

## 11. Capacidade da instalação

Os limites comerciais de cada tenant continuam separados da capacidade técnica do servidor. O PlatformAdmin acompanha globalmente clientes não encerrados, linhas WhatsApp ativas e Operators ativos conforme **US-011/FR-040/FR-041** da especificação principal. Os limites padrão da instalação são 25 clientes, 40 linhas e 90 operadores e não alteram o plano contratado.
