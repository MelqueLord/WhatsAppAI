# Especificação do produto: plataforma de atendimento WhatsApp com IA

**Status:** Draft para revisão  
**Versão:** 0.4.0
**Data:** 2026-08-16

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
- Autenticação de TenantOwner/Operator, ambos presentes no primeiro piloto, e administração mínima da plataforma.
- Frontend e backend publicados no mesmo site, com sessão baseada em cookie e proteção antiforgery.
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
4. O PlatformAdmin pode criar, suspender e reativar o tenant; a suspensão bloqueia novas operações sem apagar o histórico.
5. A criação devolve uma única vez o link de ativação do TenantOwner para envio manual, sem serviço de e-mail.

### US-002 — Receber e visualizar conversa (P1)

Como Operator, quero ver uma mensagem recebida aparecer na inbox com contato, horário e status para acompanhar o atendimento.

**Aceite:**

1. Reentrega do mesmo evento da Meta não duplica mensagem.
2. A conversa aparece em tempo quase real para usuários conectados do tenant correto.
3. Eventos desconhecidos são preservados para diagnóstico sem quebrar o endpoint.
4. O histórico é paginado por cursor e a mídia é obtida somente por endpoint autenticado da plataforma no tenant corrente.

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

### US-008 — Ativar conta convidada (P1)

Como TenantOwner ou Operator convidado, quero definir minha senha por um link temporário para ativar minha conta com segurança.

**Aceite:**

1. O convite tem uso único, expira em 24 horas e o token é persistido somente como hash.
2. Um token inválido, expirado, usado, revogado ou substituído não ativa a conta e retorna erro sem revelar se o e-mail existe.
3. A ativação define a senha, marca usuário e membership como ativos e consome o convite atomicamente.
4. O link é exibido somente na criação/reenvio para entrega manual; o MVP não envia e-mail.

### US-009 — Gerenciar Operators (P1)

Como TenantOwner, quero listar, convidar, desativar, reativar e reenviar convite para Operators do meu tenant para controlar quem atende conversas.

**Aceite:**

1. Somente TenantOwner autenticado opera memberships `Operator` do tenant corrente.
2. Convite inicial e reenvio retornam um novo link uma única vez; reenvio invalida convites anteriores ainda utilizáveis.
3. Desativar um Operator impede novo login e invalida imediatamente suas sessões existentes.
4. Reativar não restaura sessões antigas; o Operator precisa autenticar novamente.
5. O mesmo usuário não pode pertencer a mais de um tenant no MVP.

## 5. Requisitos funcionais

- **FR-001:** autenticar usuários com frontend e backend no mesmo site; em produção o cookie de sessão é `HttpOnly`, `Secure` e `SameSite=Lax`, e toda mutação autenticada exige token antiforgery enviado em `X-CSRF-TOKEN`.
- **FR-002:** autorizar ações por papel e tenant corrente.
- **FR-003:** permitir ao PlatformAdmin autorizado criar, suspender e reativar tenants por casos de uso, API e interface administrativa, preservando todo o histórico na suspensão.
- **FR-004:** armazenar credenciais de tenant, o `app_secret` e o verify token globais da plataforma por abstração de cofre e nunca em texto puro.
- **FR-005:** usar um único Meta App compartilhado, validar seu challenge com o verify token global e validar a assinatura do POST com o `app_secret` global antes de resolver o tenant pelo `phone_number_id`.
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
- **FR-016:** registrar somente metadados operacionais sanitizados de cada interação, incluindo modelo, tokens, latência e resultado; nunca persistir prompt completo, raciocínio interno, segredo ou conteúdo pessoal não mascarado.
- **FR-017:** permitir criar, editar, ativar e desativar itens de conhecimento com concorrência otimista; o MVP não realiza exclusão física desses itens.
- **FR-018:** disponibilizar estimativas de unidades e custos por período, representando valores monetários em unidade menor inteira e moeda ISO 4217.
- **FR-019:** registrar auditoria imutável para ações sensíveis, com proteção de persistência contra `UPDATE` e `DELETE` pela identidade da aplicação.
- **FR-020:** permitir política de retenção e exclusão operacional por tenant.
- **FR-021:** testar as conexões Meta/OpenAI sem expor credenciais ao navegador.
- **FR-022:** classificar eventos não reconhecidos, preservar um envelope operacional sanitizado separado do payload original cifrado e de acesso restrito, e permitir consulta e reprocessamento auditados.
- **FR-023:** expor mídia básica por endpoint autenticado e limitado ao tenant corrente, atuando como download/proxy sem revelar token ou URL privada da Meta.
- **FR-024:** paginar por cursor a lista de conversas e o histórico de mensagens, com limite máximo definido no contrato.
- **FR-025:** criar e ativar convites de TenantOwner/Operator com uso único, validade de 24 horas, token persistido somente como hash e link retornado uma única vez para envio manual.
- **FR-026:** permitir ao TenantOwner listar, convidar, desativar, reativar e reenviar convite apenas para Operators do tenant corrente.
- **FR-027:** expor `GET /auth/me` com usuário, tenant, papel e permissões derivados da sessão autenticada.
- **FR-028:** invalidar todas as sessões do usuário quando sua membership for desativada; reativação exige nova autenticação.

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
- **BR-011:** a plataforma opera um único Meta App e guarda seu `app_secret` e verify token no cofre global; cada tenant mantém titularidade de WABA, `phone_number_id`, token de acesso e faturamento.
- **BR-012:** no MVP, cada usuário pertence a no máximo um tenant; PlatformAdmin é uma permissão de plataforma sem membership de tenant.
- **BR-013:** um convite expira exatamente 24 horas após `created_at`, é consumido uma única vez e o reenvio revoga qualquer convite anterior ainda utilizável para a mesma membership/purpose.
- **BR-014:** o MVP não possui serviço de e-mail; links de ativação são entregues manualmente e nunca registrados em logs ou auditoria.
- **BR-015:** desativar uma membership rotaciona o marcador de segurança do usuário e invalida sessões; reativar a membership não revalida cookies anteriores.

## 7. Requisitos não funcionais

- **NFR-001:** reconhecer webhook válido em p95 inferior a 1 segundo, sem aguardar IA.
- **NFR-002:** refletir mensagem persistida na inbox em p95 inferior a 3 segundos, desconsiderando atraso do provedor.
- **NFR-003:** obter a decisão estruturada da IA em p95 inferior a 10 segundos em teste controlado com pelo menos 100 requisições elegíveis, medindo separadamente tempo de fila, aplicação e provedor.
- **NFR-004:** atingir SLO mensal de 99,5% para requisições elegíveis da API e reconhecimentos de webhook; `SLI = respostas concluídas sem 5xx nem timeout da plataforma ÷ total de requisições válidas recebidas`, sem excluir manutenção, e falhas atribuídas a Meta/OpenAI recebem dimensão separada sem serem removidas do total.
- **NFR-005:** demonstrar no piloto RPO de até 24 horas restaurando backup cujo ponto tenha no máximo 24 horas e RTO de até 4 horas entre a declaração do incidente e a aprovação do smoke test do serviço restaurado.
- **NFR-006:** zero acesso cruzado entre tenants em testes automatizados e produção.
- **NFR-007:** suportar inicialmente 50 tenants, 100 usuários concorrentes e 50 mil mensagens/mês por implantação sem mudança de arquitetura.
- **NFR-008:** logs estruturados não contêm chaves, tokens ou números completos de telefone.
- **NFR-009:** operações externas toleram reentrega, timeout e resposta duplicada.

## 8. Critérios de sucesso do piloto

- **SC-001:** em até 60 minutos depois de disponíveis as contas aprovadas e com os convidados presentes, criar o tenant, entregar manualmente e consumir os convites do TenantOwner e de pelo menos um Operator, obter `GET /auth/me` correto para ambos e concluir os testes Meta/OpenAI.
- **SC-002:** pelo menos 95% de uma amostra de no mínimo 1.000 webhooks válidos alcançam `Processed` sem reprocessamento manual; retries automáticos controlados não contam como intervenção.
- **SC-003:** nenhuma mensagem duplicada após teste de reentrega em massa.
- **SC-004:** operador assume conversa antes de qualquer nova resposta automática em 100% dos testes de corrida.
- **SC-005:** 100% dos testes negativos de isolamento e segredo passam.
- **SC-006:** um cliente piloto opera sete dias com recuperação documentada das falhas observadas.

## 9. Perguntas abertas para aprovação

Estas decisões não bloqueiam o desenho, mas precisam ser fechadas antes das fases indicadas:

1. Nome comercial e identidade visual — antes do piloto.
2. Provedor de hospedagem e cofre gerenciado — antes da Fase 8.
3. Retenção padrão entre 180 e 365 dias — antes da Fase 7.
4. Política comercial do SaaS (setup e mensalidade) — fora do código do MVP, antes da venda.

## 10. Dependências externas

- Conta Meta Business aprovada, WABA, número e permissões do cliente.
- Um Meta App compartilhado da plataforma, com `app_secret` e verify token em cofre global, configurado para WhatsApp Cloud API e webhook HTTPS público.
- Projeto OpenAI com faturamento, chave e limites do cliente.
- Domínio, TLS, banco MySQL 8.4 LTS, armazenamento de segredos e backup.

## 11. Extensões registradas

### Sistema de Planos e Gestão de Empresas

**Spec:** `spec-planos.md`  
**Status:** Implementado (Fase 9 - T090-T112)

Permite ao PlatformAdmin cadastrar empresas com dois tipos de plano:
- **BOT:** Todos os recursos da plataforma, exceto IA para atendimento (inbox, operadores, resposta humana, todos os modos)
- **IA+BOT:** Completo com IA para atendimento automatizado

Funcionalidades de IA são filtradas pelo plano contratado. Plano BOT não usa IA mas mantém todos os outros recursos.
