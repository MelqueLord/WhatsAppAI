# Especificação: Sistema de Planos e Gestão de Empresas

**Status:** Implementado  
**Versão:** 1.1.0
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

### Plano 1: BOT (Sem IA)

- **Todos os recursos da plataforma**, exceto IA para atendimento
- Inbox em tempo real
- Resposta humana manual
- Modos `Automatic`, `Human` e `Paused`
- Gestão de operadores
- Base de conhecimento (para consulta manual, não alimenta IA)
- Mídia, tags, auditoria, uso
- WhatsApp Cloud API integrado
- Ideal para atendimento 100% humano sem automação por IA

### Plano 2: IA + BOT (Completo com IA)

- **Todos os recursos do plano BOT**
- Resposta automática por IA
- IA utiliza conhecimento para gerar respostas
- Behavior policy e circuit breaker
- AiInteraction e métricas de IA
- Configuração de OpenAI obrigatória
- Ideal para atendimento automatizado com supervisão humana

## 4. Histórias de Usuário

### US-P001 — Cadastrar empresa com plano (P1)

Como PlatformAdmin, quero cadastrar uma empresa selecionando um plano para provisionar o tenant com as funcionalidades corretas.

**Aceite:**

1. PlatformAdmin seleciona plano (BOT ou IA+BOT) ao criar tenant
2. Plano é persistido no tenant e controla funcionalidades disponíveis
3. Tenant criado recebe status `Pending` até ativação pelo TenantOwner
4. Link de ativação é gerado para envio manual

### US-P002 — Ativar empresa e configurar ambiente (P1)

Como TenantOwner, quero ativar minha empresa e configurar o ambiente conforme o plano contratado.

**Aceite:**

1. Ativação define senha e ativa TenantOwner
2. Interface mostra apenas funcionalidades do plano contratado
3. Configurações obrigatórias são guiadas (WhatsApp para ambos, OpenAI apenas para IA+BOT)
4. Plano BOT não mostra configurações de IA nem métricas de IA

### US-P003 — Gerenciar operadores (P1)

Como TenantOwner, quero convidar e gerenciar operadores para atendimento humano.

**Aceite:**

1. Ambos os planos permitem gestão de operadores
2. Operadores têm acesso à inbox e modos de conversa
3. Plano BOT: operadores atendem manualmente sem IA
4. Plano IA+BOT: operadores atendem manualmente e IA responde automaticamente
5. Limite de operadores pode ser configurável por plano

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

- **FR-P001:** PlatformAdmin pode criar tenant selecionando plano (BOT ou IA+BOT)
- **FR-P002:** Plano é persistido em `Tenant` e controla acesso a funcionalidades
- **FR-P003:** Plano BOT desabilita: IA para atendimento, configuração de OpenAI, métricas de IA, AiInteraction
- **FR-P004:** Plano IA+BOT habilita todas as funcionalidades incluindo IA
- **FR-P005:** Plano pode ser alterado por PlatformAdmin (upgrade/downgrade com validação)

### Cadastro de Empresa

- **FR-P006:** Cadastro de empresa cria Tenant + TenantOwner + Invitation atomicamente
- **FR-P007:** Status inicial do tenant é `Pending` até ativação
- **FR-P008:** Link de ativação é retornado uma única vez para envio manual
- **FR-P009:** Ativação define senha e ativa User/Membership/Tenant

### Configuração por Plano

- **FR-P010:** Interface filtra funcionalidades de IA baseado no plano
- **FR-P011:** Endpoints de IA validam plano antes de executar
- **FR-P012:** Configuração de WhatsApp é obrigatória para ambos os planos
- **FR-P013:** Configuração de OpenAI é obrigatória apenas para plano IA+BOT

### Operadores

- **FR-P014:** Gestão de operadores disponível para ambos os planos
- **FR-P015:** Ambos os planos permitem criação de memberships Operator
- **FR-P016:** Limite de operadores pode ser configurado por plano (futuro)

## 6. Regras de Negócio

- **BR-P001:** Um tenant possui exatamente um plano ativo
- **BR-P002:** Plano BOT não pode usar IA para atendimento (AiOrchestrationWorker não processa)
- **BR-P003:** Plano BOT pode usar todos os modos de conversa (Automatic, Human, Paused) para resposta humana
- **BR-P004:** Mudança de plano preserva dados existentes mas altera funcionalidades disponíveis
- **BR-P005:** Downgrade de IA+BOT para BOT desabilita IA mas preserva histórico de AiInteraction
- **BR-P006:** Cadastro de empresa exige seleção de plano obrigatória
- **BR-P007:** Plano BOT não requer configuração de OpenAI

## 7. Modelo de Dados

### Entidade: SubscriptionPlan

```
id, name, code, description, features_json, max_operators, max_knowledge_items, 
is_active, created_at, updated_at
```

**Códigos:** `BOT`, `IA_BOT`

**features_json:** JSON com funcionalidades habilitadas:
```json
{
  "ai_enabled": false,
  "openai_required": false,
  "ai_metrics": false
}
```

**Plano BOT:** `ai_enabled: false`  
**Plano IA+BOT:** `ai_enabled: true, openai_required: true, ai_metrics: true`

### Alteração em Tenant

Adicionar campos:
- `plan_id` (FK para SubscriptionPlan)
- `plan_activated_at`
- `plan_expires_at` (futuro, para trial/pagamento)

### Alteração em TenantMembership

Nenhuma alteração necessária - ambos os planos permitem operadores.

## 8. Interface

### Página de Cadastro de Empresa (PlatformAdmin)

1. Nome da empresa
2. Nome do TenantOwner
3. E-mail do TenantOwner
4. Seleção de plano (BOT ou IA+BOT) com descrição de funcionalidades
5. Botão "Criar Empresa"
6. Link de ativação exibido uma única vez

### Dashboard do TenantOwner

- Indicação do plano atual
- Cards de funcionalidades habilitadas/desabilitadas
- Guia de configuração obrigatória

### Menu Lateral

**Ambos os planos:** Dashboard, Inbox, Operadores, Configurações, Conhecimento, Tags, Uso

**Plano IA+BOT (adicional):** Configurações de IA, Métricas de IA

## 9. Dependências

- Plataforma base (spec.md) implementada
- US-001 (Provisionar cliente) estendido
- US-009 (Gerenciar Operators) condicionado ao plano

## 10. Fora do Escopo (MVP)

- Pagamento/recorrência de planos
- Trial period
- Upgrade/downgrade self-service
- Planos customizados
- Limites de uso por plano (mensagens, tokens)
- Marketplace de planos

## 11. Capacidade da instalação

Os limites comerciais de cada tenant continuam separados da capacidade técnica do servidor. O PlatformAdmin acompanha globalmente clientes não encerrados, linhas WhatsApp ativas e Operators ativos conforme **US-011/FR-040/FR-041** da especificação principal. Os limites padrão da instalação são 25 clientes, 40 linhas e 90 operadores e não alteram o plano contratado.
