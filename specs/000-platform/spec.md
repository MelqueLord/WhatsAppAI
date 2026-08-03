# Especificação do produto: plataforma de atendimento WhatsApp com IA

**Status:** Draft para revisão  
**Versão:** 0.1.0  
**Data:** 2026-08-03

## 1. Problema

Pequenas empresas precisam atender clientes no WhatsApp com rapidez, sem implantar CRM ou automações complexas. O proprietário da plataforma configura a conta oficial da Meta e a conta da OpenAI do cliente e entrega uma caixa de entrada pronta, na qual a IA responde dúvidas rotineiras e uma pessoa assume exceções.

## 2. Atores

- **PlatformAdmin:** administra tenants, auxilia onboarding e diagnostica integrações.
- **TenantOwner:** configura negócio, conhecimento e operadores; acompanha uso.
- **Operator:** atende conversas e controla o modo humano/automático.
- **EndCustomer:** envia mensagens ao número comercial do tenant.
- **AI Agent:** redige respostas sob política, contexto e conhecimento aprovados.

## 3. Escopo do MVP

### Incluído

- SaaS multi-tenant com um número de WhatsApp por tenant.
- Autenticação de TenantOwner/Operator e administração mínima da plataforma.
- Conexão guiada à WhatsApp Cloud API e à OpenAI com credenciais do cliente.
- Webhook de mensagens e status com processamento idempotente.
- Inbox em tempo real, histórico, texto e mídia básica.
- Resposta humana e resposta automática textual por IA.
- Modos `Automatic`, `Human` e `Paused` por conversa.
- Base de conhecimento textual simples.
- Indicadores estimados de consumo; faturas oficiais continuam nos provedores.
- Logs de auditoria, métricas operacionais, exclusão e retenção básica.

### Fora do escopo

- Campanhas, listas, disparos, templates de marketing e mensagens proativas.
- CRM, funil comercial, pagamentos, agenda, catálogo e integrações externas.
- Construtor visual de fluxos, n8n no caminho crítico e múltiplos canais.
- IA para áudio/imagem, treinamento de modelos e RAG vetorial no MVP.
- Aplicativos móveis nativos, marketplace e cobrança do SaaS dentro do produto.
- Múltiplos números por tenant e roteamento avançado entre equipes.

## 4. Histórias de usuário

### US-001 — Provisionar um cliente (P1)

Como PlatformAdmin, quero criar um tenant e orientar a conexão das contas do cliente para entregar o ambiente funcionando sem assumir a titularidade delas.

**Aceite:**

1. Tenant, proprietário e configurações são isolados por `TenantId`.
2. O sistema testa Meta e OpenAI sem revelar os segredos armazenados.
3. Uma falha indica qual integração e qual passo precisam de correção.

### US-002 — Receber e visualizar conversa (P1)

Como Operator, quero ver uma mensagem recebida aparecer na inbox com contato, horário e status para acompanhar o atendimento.

**Aceite:**

1. Reentrega do mesmo evento da Meta não duplica mensagem.
2. A conversa aparece em tempo quase real para usuários conectados do tenant correto.
3. Eventos desconhecidos são preservados para diagnóstico sem quebrar o endpoint.

### US-003 — Responder manualmente (P1)

Como Operator, quero assumir uma conversa e enviar texto para resolver casos que exigem uma pessoa.

**Aceite:**

1. Assumir altera o modo para `Human` antes do próximo disparo automático.
2. O envio apresenta estados `Queued`, `Sent`, `Delivered`, `Read` ou `Failed`.
3. Texto livre é bloqueado quando a janela de atendimento de 24 horas está fechada.

### US-004 — Responder automaticamente (P1)

Como TenantOwner, quero que a IA responda mensagens rotineiras usando as instruções e o conhecimento do meu negócio.

**Aceite:**

1. A IA recebe apenas dados do tenant e da conversa corrente.
2. A saída estruturada é validada pelo backend antes do envio.
3. Solicitação humana, baixa confiança, risco ou ausência de resposta segura transfere para `Human`.
4. Falha da IA não perde a mensagem e não produz loop de tentativas.

### US-005 — Manter conhecimento (P2)

Como TenantOwner, quero cadastrar respostas, políticas e informações do negócio para orientar a IA sem alterar código.

**Aceite:**

1. Itens podem ser criados, editados, ativados e desativados.
2. A próxima interação usa somente itens ativos do tenant.
3. Limites de tamanho evitam contexto excessivo e custo imprevisível.

### US-006 — Acompanhar uso (P2)

Como TenantOwner, quero consultar estimativas de mensagens e tokens para antecipar custos cobrados diretamente pela Meta e OpenAI.

**Aceite:**

1. O painel separa consumo por provedor e período.
2. A interface informa que valores são estimativas, não fatura.
3. Falta de preço atualizado não impede registrar unidades de consumo.

### US-007 — Auditar operação (P2)

Como PlatformAdmin, quero identificar falhas e ações relevantes sem ler segredos ou misturar clientes.

**Aceite:**

1. Toda chamada externa tem correlation ID e resultado sanitizado.
2. Alterações de modo, credencial e conhecimento registram ator e horário.
3. O administrador precisa selecionar explicitamente o tenant diagnosticado.

## 5. Requisitos funcionais

- **FR-001:** autenticar usuários com cookie HttpOnly, Secure e SameSite apropriado.
- **FR-002:** autorizar ações por papel e tenant corrente.
- **FR-003:** criar, suspender e reativar tenants sem apagar histórico.
- **FR-004:** armazenar credenciais por abstração de cofre e nunca em texto puro.
- **FR-005:** verificar o desafio e a autenticidade do webhook da Meta.
- **FR-006:** persistir cada evento de webhook antes do processamento assíncrono.
- **FR-007:** deduplicar eventos e mensagens por identificadores do provedor.
- **FR-008:** associar mensagens a contato e conversa do tenant/número corretos.
- **FR-009:** publicar atualizações de inbox via SignalR somente ao grupo do tenant.
- **FR-010:** enviar mensagens humanas por fila durável e registrar status.
- **FR-011:** controlar modo e responsável da conversa com concorrência otimista.
- **FR-012:** impedir texto livre fora da janela de atendimento no MVP.
- **FR-013:** montar contexto de IA com política, conhecimento ativo e histórico limitado.
- **FR-014:** solicitar resposta estruturada contendo ação, texto, confiança e razão de handoff.
- **FR-015:** validar conteúdo, janela e modo novamente antes de enfileirar resposta de IA.
- **FR-016:** registrar modelo, tokens, latência e resultado de cada interação sem persistir segredo.
- **FR-017:** permitir CRUD e ativação de itens de conhecimento.
- **FR-018:** disponibilizar estimativas de unidades e custos por período.
- **FR-019:** registrar auditoria imutável para ações sensíveis.
- **FR-020:** permitir política de retenção e exclusão operacional por tenant.
- **FR-021:** testar as conexões Meta/OpenAI sem expor credenciais ao navegador.
- **FR-022:** preservar payload bruto sanitizado de eventos não reconhecidos para reprocessamento.

## 6. Regras de negócio

- **BR-001:** um tenant possui exatamente um número ativo no MVP.
- **BR-002:** um identificador de telefone de cliente pertence a um contato por tenant.
- **BR-003:** uma conversa em `Human` ou `Paused` nunca gera envio automático.
- **BR-004:** a mudança para `Human` vence uma resposta de IA concorrente ainda não enviada.
- **BR-005:** somente mensagem recebida do cliente abre/renova a janela de 24 horas.
- **BR-006:** marketing, promoção ou template proativo não é enviado pelo MVP.
- **BR-007:** preço exibido é estimativo e versionado; unidades originais são a fonte auditável.
- **BR-008:** credenciais pertencem ao tenant; o PlatformAdmin pode substituí-las, nunca recuperá-las em claro.
- **BR-009:** após limite controlado de tentativas, o job vai para estado morto e gera alerta.
- **BR-010:** resposta da IA é descartada se a versão da conversa mudou desde o início da geração.

## 7. Requisitos não funcionais

- **NFR-001:** reconhecer webhook válido em p95 inferior a 1 segundo, sem aguardar IA.
- **NFR-002:** refletir mensagem persistida na inbox em p95 inferior a 3 segundos, desconsiderando atraso do provedor.
- **NFR-003:** buscar resposta de IA em p95 inferior a 10 segundos em condições normais, medindo o provedor separadamente.
- **NFR-004:** disponibilidade inicial alvo de 99,5% mensal.
- **NFR-005:** RPO de até 24 horas e RTO de até 4 horas para piloto.
- **NFR-006:** zero acesso cruzado entre tenants em testes automatizados e produção.
- **NFR-007:** suportar inicialmente 50 tenants, 100 usuários concorrentes e 50 mil mensagens/mês por implantação sem mudança de arquitetura.
- **NFR-008:** logs estruturados não contêm chaves, tokens ou números completos de telefone.
- **NFR-009:** operações externas toleram reentrega, timeout e resposta duplicada.

## 8. Critérios de sucesso do piloto

- **SC-001:** provisionar um cliente de teste em até 60 minutos depois de disponíveis as contas aprovadas.
- **SC-002:** pelo menos 95% dos webhooks válidos processados sem intervenção.
- **SC-003:** nenhuma mensagem duplicada após teste de reentrega em massa.
- **SC-004:** operador assume conversa antes de qualquer nova resposta automática em 100% dos testes de corrida.
- **SC-005:** 100% dos testes negativos de isolamento e segredo passam.
- **SC-006:** um cliente piloto opera sete dias com recuperação documentada das falhas observadas.

## 9. Perguntas abertas para aprovação

Estas decisões não bloqueiam o desenho, mas precisam ser fechadas antes das fases indicadas:

1. Nome comercial e identidade visual — antes do piloto.
2. Provedor de hospedagem e cofre gerenciado — antes da Fase 8.
3. Retenção padrão entre 180 e 365 dias — antes da Fase 7.
4. Se o papel `Operator` entra no primeiro piloto ou se haverá apenas `TenantOwner` — antes da Fase 1.
5. Política comercial do SaaS (setup e mensalidade) — fora do código do MVP, antes da venda.

## 10. Dependências externas

- Conta Meta Business aprovada, WABA, número e permissões do cliente.
- Aplicativo Meta configurado para WhatsApp Cloud API e webhook HTTPS público.
- Projeto OpenAI com faturamento, chave e limites do cliente.
- Domínio, TLS, banco PostgreSQL, armazenamento de segredos e backup.
