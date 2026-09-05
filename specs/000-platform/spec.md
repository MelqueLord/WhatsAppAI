# Especificação do produto: plataforma de atendimento WhatsApp com IA

**Status:** Draft para revisão  
**Versão:** 0.31.0
**Data:** 2026-09-03

## 1. Problema

Pequenas empresas precisam atender clientes no WhatsApp com rapidez, sem implantar CRM ou automações complexas. O proprietário da plataforma configura uma conexão Cloud da Meta ou uma sessão WhatsApp Web via QR/Baileys e administra a capacidade dos provedores de IA. Cada tenant recebe uma caixa de entrada pronta, na qual a IA responde dúvidas rotineiras usando as diretrizes e o conhecimento daquela empresa, enquanto uma pessoa assume exceções.

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
- Conexão guiada à WhatsApp Cloud API ou WhatsApp Web via QR/Baileys, e aos provedores de IA com credenciais administradas pela plataforma.
- Webhook de mensagens e status com processamento idempotente.
- Inbox em tempo real, histórico, texto e mídia básica.
- Resposta humana e resposta automática textual por IA.
- Templates transacionais aprovados pela Meta para conversas na API Oficial fora da janela de 24 horas.
- Modos `Automatic`, `Human` e `Paused` por conversa.
- Base de conhecimento textual simples.
- Consumo real de tokens, custo operacional estimado e controle de franquia/orçamento por tenant; o faturamento comercial é administrado pela plataforma.
- Logs de auditoria, métricas operacionais, exclusão e retenção básica.

### Fora do escopo

- Campanhas, listas, templates de marketing e mensagens proativas.
- CRM, funil comercial, agenda, catálogo e integrações externas. O PlatformAdmin registra pagamentos manualmente; não há cobrança online integrada.
- Construtor visual de fluxos, n8n no caminho crítico e múltiplos canais.
- IA para áudio/imagem, treinamento de modelos e RAG vetorial no MVP.
- Aplicativos móveis nativos, marketplace e cobrança do SaaS dentro do produto.
- Múltiplos números por tenant e roteamento avançado entre equipes.

## 4. Histórias de usuário

### US-001 — Provisionar um cliente (P1)

Como PlatformAdmin, quero criar um tenant, configurar as integrações e provisionar a capacidade de IA administrada pela plataforma para entregar o ambiente funcionando.

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

Como TenantOwner, quero consultar o consumo do meu pacote de respostas para antecipar a necessidade de recarga, sem acesso aos custos técnicos da plataforma.

**Aceite:**

1. O dashboard mostra respostas consumidas, saldo do pacote e percentual do mês.
2. Ao atingir 80% do pacote, a empresa vê um aviso de atenção; ao esgotar, vê a orientação para solicitar recarga.
3. O painel não expõe tokens, provedor, modelo ou custo estimado; esses dados ficam exclusivos do PlatformAdmin.

### US-013 — Controlar pacotes e custo de IA (P1)

Como PlatformAdmin, quero acompanhar o consumo de cada empresa e liberar ou renovar pacotes de respostas para controlar o custo real da IA da plataforma.

**Aceite:**

1. O administrador visualiza, por empresa e mês UTC, respostas consumidas, tokens de entrada, tokens de saída, total de tokens, provedor e modelo contratado.
2. O administrador pode alterar o limite-base do pacote e, quando necessário, adicionar recargas de 500 respostas válidas somente no mês UTC corrente, com idempotência e registro de auditoria.
3. A visão permite comparar a distribuição de tokens entre empresas para estimar o gasto da plataforma.
4. O consumo é isolado por tenant e não expõe credenciais, prompts ou conteúdo de conversas.

### US-014 — Encerrar e retomar conversas (P1)

Como Operator, quero encerrar uma conversa quando o atendimento terminar e permitir que o cliente retome o contato sem perder o contexto.

**Aceite:**

1. O operador pode encerrar a conversa com um comando explícito; ela deixa a lista de abertas e aparece na fila de encerradas.
2. O encerramento preserva a conversa e todas as suas mensagens, sem apagar histórico nem derrubar a conexão WhatsApp.
3. Uma nova mensagem do cliente reabre a mesma conversa no modo automático, remove a atribuição humana e mantém o histórico disponível para a IA.
4. Ao retomar, o contexto automático considera a mensagem recebida e até três mensagens anteriores, usando as diretrizes, o perfil e o conhecimento do tenant para guiar o atendimento.

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
6. O TenantOwner pode manter o Operator no atendimento geral ou restringi-lo a uma fila ativa específica; a restrição vale para listar, abrir e responder conversas.

### US-010 — Importar contatos por planilha (P2)

Como TenantOwner, quero importar contatos de uma planilha para cadastrar minha base sem digitação individual.

**Aceite:**

1. A importação aceita arquivo `.csv` ou `.xlsx` com cabeçalhos `nome` e `contato`, independentemente de maiúsculas/minúsculas.
2. Linhas válidas criam contatos somente no tenant corrente; números são normalizados antes da verificação de duplicidade.
3. Contatos existentes e números repetidos no mesmo arquivo são ignorados sem sobrescrever dados cadastrados.
4. Linhas inválidas não impedem as demais e o resultado informa quantidades importadas, ignoradas e inválidas, com motivo por linha sem repetir o número na resposta.
5. Somente TenantOwner pode importar; arquivos maiores que 2 MB ou com mais de 5.000 linhas são rejeitados.

### US-011 — Acompanhar capacidade da infraestrutura (P1)

Como PlatformAdmin, quero acompanhar clientes, linhas e operadores hospedados para migrar a instalação antes de exceder a capacidade do KVM.

**Aceite:**

1. A administração mostra clientes não encerrados, conexões WhatsApp ativas e Operators ativos.
2. Os limites são configuráveis por ambiente e usam, por padrão, 25 clientes, 40 linhas e 90 operadores.
3. Ao atingir qualquer limite, a interface mostra de forma destacada que a migração do KVM é necessária.
4. Tenants suspensos continuam na contagem; tenants encerrados e seus recursos não contam.

### US-012 — Provisionar plano comercial e franquia de IA (P1)

Como PlatformAdmin, quero selecionar STAR, FLOW ou SCALA e personalizar a franquia mensal de IA para que cada empresa receba automaticamente os recursos contratados ao entrar.

**Aceite:**

1. STAR provisiona 1 vaga de linha e 2 Operators; FLOW, 2 vagas e 4 Operators; SCALA, 3 vagas e 8 Operators. Em cada vaga, o PlatformAdmin escolhe API Oficial ou QR Code.
2. Todos incluem IA; BOT, tags e filas automáticas são liberados apenas nos planos que os oferecem.
3. A franquia começa com o padrão do plano, mas pode ser personalizada por empresa com concorrência otimista.
4. Somente respostas válidas da IA enfileiradas para envio consomem franquia; entradas, simulações, falhas, fallback e handoff não consomem.
5. Ao atingir a franquia efetiva, a IA é suspensa automaticamente: nenhuma nova resposta da IA é enviada e o fluxo aplica o handoff/fallback seguro existente; a IA volta a ficar disponível após recarga ou na virada do mês.

## 5. Requisitos funcionais

- **FR-001:** autenticar usuários com frontend e backend no mesmo site; em produção o cookie de sessão é `HttpOnly`, `Secure` e `SameSite=Lax`, e toda mutação autenticada exige token antiforgery enviado em `X-CSRF-TOKEN`.
- **FR-002:** autorizar ações por papel e tenant corrente.
- **FR-003:** permitir ao PlatformAdmin autorizado criar, suspender e reativar tenants por casos de uso, API e interface administrativa, preservando todo o histórico na suspensão.
- **FR-004:** armazenar credenciais administradas pela plataforma, o `app_secret` e o verify token globais por abstração de cofre e nunca em texto puro; nenhuma API key de IA deve ser exibida ou alterada pelo tenant.
- **FR-005:** usar um único Meta App compartilhado, validar seu challenge com o verify token global e validar a assinatura do POST com o `app_secret` global antes de resolver o tenant pelo `phone_number_id`.
- **FR-006:** persistir cada evento de webhook antes do processamento assíncrono.
- **FR-007:** deduplicar eventos e mensagens por identificadores do provedor.
- **FR-008:** associar mensagens a contato e conversa do tenant/número corretos.
- **FR-009:** publicar atualizações de inbox via SignalR somente ao grupo do tenant.
- **FR-010:** enviar mensagens humanas por fila durável e registrar status.
- **FR-011:** controlar modo e responsável da conversa com concorrência otimista.
- **FR-012:** impedir texto livre fora da janela de atendimento no MVP e permitir somente templates aprovados pela Meta quando a conversa usa a API Oficial; conexões QR Code não aceitam templates.
- **FR-013:** montar contexto de IA com política, conhecimento ativo e histórico limitado.
- **FR-014:** solicitar resposta estruturada contendo ação, texto, confiança e razão de handoff.
- **FR-015:** validar conteúdo, janela e modo novamente antes de enfileirar resposta de IA.
- **FR-016:** registrar somente metadados operacionais sanitizados de cada interação, incluindo modelo, tokens, latência e resultado; nunca persistir prompt completo, raciocínio interno, segredo ou conteúdo pessoal não mascarado.
- **FR-017:** permitir criar, editar, ativar e desativar itens de conhecimento com concorrência otimista; o MVP não realiza exclusão física desses itens.
- **FR-018:** disponibilizar estimativas de unidades e custos por período, representando valores monetários em unidade menor inteira e moeda ISO 4217.
- **FR-019:** registrar auditoria imutável para ações sensíveis, com proteção de persistência contra `UPDATE` e `DELETE` pela identidade da aplicação.
- **FR-020:** permitir política de retenção e exclusão operacional por tenant.
- **FR-021:** permitir ao PlatformAdmin testar as conexões Meta, WhatsApp Web/QR e provedores de IA sem expor credenciais ao navegador ou ao tenant.
- **FR-022:** classificar eventos não reconhecidos, preservar um envelope operacional sanitizado separado do payload original cifrado e de acesso restrito, e permitir consulta e reprocessamento auditados.
- **FR-023:** expor mídia básica por endpoint autenticado e limitado ao tenant corrente, atuando como download/proxy sem revelar token ou URL privada da Meta.
- **FR-024:** paginar por cursor a lista de conversas e o histórico de mensagens, com limite máximo definido no contrato.
- **FR-025:** criar e ativar convites de TenantOwner/Operator com uso único, validade de 24 horas, token persistido somente como hash e link retornado uma única vez para envio manual.
- **FR-026:** permitir ao TenantOwner listar, convidar, desativar, reativar e reenviar convite apenas para Operators do tenant corrente.
- **FR-027:** expor `GET /auth/me` com usuário, tenant, papel e permissões derivados da sessão autenticada.
- **FR-028:** invalidar todas as sessões do usuário quando sua membership for desativada; reativação exige nova autenticação.
- **FR-029:** permitir ao PlatformAdmin registrar no tenant a quantidade contratada de linhas via API oficial e via QR Code, aceitando somente valores inteiros não negativos e exibindo os limites no cadastro administrativo.
- **FR-030:** permitir ao PlatformAdmin editar nome, plano e quantidades de linhas do tenant com concorrência otimista por `If-Match`, sem alterar credenciais ou o responsável pela empresa.
- **FR-031:** permitir ao tenant conectar cada slot contratado de API oficial e QR Code de forma independente, com sessão QR e credencial associadas ao número do slot, sem sobrescrever outra linha.
- **FR-032:** permitir ao PlatformAdmin definir o limite de Operators por tenant; a criação de Operators deve bloquear novas inclusões ao atingir o limite, e `0` representa capacidade ilimitada para compatibilidade.
- **FR-033:** permitir ao TenantOwner atribuir uma linha API oficial ou QR Code a cada Operator do próprio tenant; a atribuição deve ser validada pela quota e aparecer no `/auth/me` e no painel do operador.
- **FR-034:** permitir ao PlatformAdmin registrar manualmente o pagamento mensal do tenant; o próximo vencimento é sempre 30 dias após a data do lançamento.
- **FR-035:** suspender automaticamente o tenant ativo após 35 dias de atraso. A empresa suspensa mantém login e leitura, mas não pode enviar mensagens nem executar automações WhatsApp até a reativação por pagamento.
- **FR-036:** permitir ao TenantOwner selecionar, entre as filas ativas do próprio tenant, quais podem ser usadas pela IA; quando o cliente escolher ou solicitar uma dessas filas, a IA deve retornar seu nome e o backend deve validar, atribuir a conversa à fila e transferi-la para atendimento humano.
- **FR-037:** permitir ao TenantOwner selecionar tags ativas do próprio tenant para categorização pela IA; após cada decisão válida, o backend deve adicionar ao contato somente as tags autorizadas reconhecidas pela IA, sem remover tags existentes.
- **FR-038:** permitir ao TenantOwner atribuir ou remover uma fila ativa do próprio tenant em cada Operator; sem fila atribuída o atendimento permanece geral, e com fila atribuída o backend deve limitar o Operator às conversas dessa fila.
- **FR-039:** permitir ao TenantOwner importar até 5.000 contatos por arquivo `.csv` ou `.xlsx` de até 2 MB, usando as colunas obrigatórias `nome` e `contato`, com resultado parcial e isolamento pelo tenant corrente.
- **FR-040:** disponibilizar somente ao PlatformAdmin um resumo global de capacidade com quantidade atual, limite configurado e percentual de uso para clientes não encerrados, conexões WhatsApp ativas e memberships `Operator` ativas.
- **FR-041:** sinalizar necessidade de migração da infraestrutura quando qualquer indicador atingir ou ultrapassar seu limite; os padrões são 25 clientes, 40 linhas e 90 operadores e podem ser substituídos por configuração de ambiente.
- **FR-042:** cadastrar novos tenants somente nos planos comerciais STAR, FLOW e SCALA, aplicando no backend as quantidades padrão de linhas e Operators do plano e preservando BOT/IA_BOT apenas para tenants legados.
- **FR-043:** expor no login as permissões efetivas de IA, BOT, tags e distribuição/filas para que frontend e backend ocultem ou bloqueiem recursos não contratados.
- **FR-044:** permitir ao PlatformAdmin definir o `monthly_ai_response_limit` base por tenant com `If-Match` e adicionar recargas idempotentes de exatamente 500 respostas ao mês UTC corrente, registradas no `UsageLedger` sem alterar o limite-base do plano.
- **FR-045:** contabilizar em `UsageLedger` somente respostas válidas da IA criadas para envio e suspender automaticamente novas respostas ao atingir a franquia efetiva do mês, usando fallback/handoff configurado sem reprocessamento infinito; recarga ou novo mês removem a suspensão derivada.
- **FR-046:** disponibilizar ao PlatformAdmin o consumo mensal real de tokens por tenant, separado em entrada/saída e distribuído por provedor e modelo, usando os valores retornados pelo provedor.
- **FR-047:** exibir no dashboard do TenantOwner o pacote efetivo de respostas, separando limite-base e recargas, saldo, percentual usado, aviso a partir de 80% e estado de IA suspensa por franquia; recarga permanece uma ação administrativa.
- **FR-048:** calcular custo somente quando houver preço registrado para o modelo/provedor, preservando tokens como fonte auditável e identificando a estimativa como não faturamento.
- **FR-061:** permitir ao PlatformAdmin definir orçamento financeiro e limites técnicos mensais por tenant e impedir novas chamadas de IA quando qualquer limite efetivo for atingido.
- **FR-062:** manter no tenant as diretrizes, o perfil, o conhecimento, a confiança, as filas, as tags e o handoff do atendimento da própria empresa, com isolamento completo entre tenants.
- **FR-063:** permitir ao TenantOwner classificar cada item de conhecimento como geral, pergunta frequente, serviço, preço, horário, pagamento, localização ou política, usando um cadastro guiado que preserve itens existentes e não altere o isolamento, a ativação ou a concorrência otimista.
- **FR-064:** permitir ao TenantOwner criar, editar, ativar e desativar exemplos de mensagem do cliente e resposta ideal, isolados por tenant e com concorrência otimista; no máximo um exemplo ativo lexicalmente relevante pode orientar o estilo de cada resposta ou simulação, sem servir como fonte de fatos comerciais.
- **FR-065:** permitir ao TenantOwner testar a IA por cenários predefinidos de preço, reclamação, agendamento, pedido de atendimento humano e assunto fora do escopo, além de uma mensagem personalizada, sem enviar mensagem ao WhatsApp, alterar conversas/Outbox ou consumir a franquia de respostas; cada teste deve usar as diretrizes, o perfil, o conhecimento e o exemplo relevante do tenant e retornar decisão, texto, confiança e motivo de handoff sanitizados.
- **FR-066:** manter a navegação lateral utilizável em telas móveis, com cabeçalho e rodapé acessíveis e lista de itens rolável; o controle de borda recolhe/expande somente no desktop e, no mobile, fecha o drawer que é aberto novamente pelo botão do cabeçalho, para todos os papéis.
- **FR-049:** manter preços versionados por provedor/modelo, calcular separadamente entrada e saída no instante do uso e persistir custo, moeda e versão no `UsageLedger`.
- **FR-050:** limitar tentativas do provedor, abrir circuito por tenant/provedor após falhas consecutivas e finalizar novas mensagens com fallback/handoff seguro enquanto o circuito estiver aberto.
- **FR-051:** persistir alterações de configuração, modo, handoff e auditoria na mesma transação, validando todas as versões `If-Match` antes de qualquer escrita para impedir estado parcial.
- **FR-052:** exigir avaliação aprovada para ativar o modelo de IA, registrar a decisão de promoção e permitir retorno transacional ao modelo de rollback aprovado.
- **FR-053:** tratar a quantidade de linhas do plano como limite total e exigir que o PlatformAdmin distribua todas as vagas entre API Oficial e QR Code no cadastro e na edição do tenant, inclusive em planos com uma única linha.
- **FR-054:** expor tokens de entrada/saída, provedor, modelo e custo estimado somente ao PlatformAdmin, por tenant; endpoints e telas de TenantOwner/Operator retornam apenas a franquia de respostas, saldo e alertas operacionais.
- **FR-055:** aplicar orçamento determinístico ao contexto de IA: histórico recente limitado, no máximo três itens relevantes de conhecimento com tamanho limitado, diretrizes livres limitadas e teto de saída de 240 tokens; as regras estruturadas e o contrato JSON devem permanecer íntegros mesmo sob contexto extenso.
- **FR-056:** usar a base de conhecimento ativa do tenant como fonte prioritária para fatos da empresa; itens sem correspondência com a solicitação não podem ser injetados como contexto e, sem informação relevante, a IA deve evitar invenção e encaminhar quando a pergunta exigir um fato não documentado.
- **FR-057:** coletar na tela de IA um perfil estruturado do negócio (descrição, público-alvo, produtos/serviços, tom, horário e localização), persistindo-o junto às diretrizes existentes para personalizar a abordagem sem substituir a base de conhecimento para fatos comerciais.
- **FR-058:** permitir ao TenantOwner configurar expediente do BOT por dia da semana e fuso horário; quando habilitado, mensagens recebidas fora de um período aberto não podem seguir o fluxo automático e devem usar a mensagem configurada de fora do horário.
- **FR-059:** permitir ao operador enviar template transacional aprovado pela Meta com idioma e parâmetros limitados quando a conversa oficial estiver fora da janela de 24 horas; o backend deve rejeitar templates em conexões QR Code e persistir a intenção na Outbox.
- **FR-060:** permitir executar múltiplas instâncias da ponte QR sem que duas instâncias controlem a mesma sessão; cada sessão deve ter lease exclusivo e renovável no PostgreSQL, e chamadas devem ser roteadas para a instância dona.
- **FR-067:** criar para toda empresa nova uma finalidade ativa de atendimento automatizado por IA, com base legal de consentimento e retenção de 365 dias; a finalidade não substitui o registro individual de consentimento de cada contato.
- **FR-068:** solicitar, uma única vez por conversa, o aceite padronizado para atendimento automatizado por IA ao contato sem consentimento ativo; ao receber a resposta explícita `SIM`, registrar evidência vinculada à mensagem recebida, confirmar o registro e habilitar os próximos atendimentos automáticos daquele contato no tenant corrente.
- **FR-069:** permitir o encerramento explícito de uma conversa pelo operador, listá-la separadamente como encerrada sem apagar mensagens, e reabrir a mesma conversa no modo automático quando o cliente enviar nova mensagem, preservando o contexto recente.
- **FR-070:** manter memória institucional automática por tenant, aproveitando respostas de IA com alta confiança e fundamentadas em conhecimento ativo para orientar atendimentos futuros da mesma empresa, sem compartilhar conteúdo entre tenants.
- **FR-071:** permitir que Operator ou TenantOwner avalie uma resposta da IA como útil ou necessitando correção; uma correção textual sanitizada pode ser convertida em conhecimento validado do tenant, sem enviar uma nova mensagem automaticamente ao cliente.
- **FR-072:** responder perguntas de atendimento por inferência segura usando, em conjunto, diretrizes, perfil, conhecimento ativo, exemplos e memória institucional da empresa; paráfrases e perguntas gerais sobre o negócio devem recuperar fatos compatíveis antes de considerar transferência.

## 6. Regras de negócio

- **BR-001:** um tenant possui exatamente um número ativo no MVP.
- **BR-002:** um identificador de telefone de cliente pertence a um contato por tenant.
- **BR-003:** uma conversa em `Human` ou `Paused` nunca gera envio automático.
- **BR-004:** a mudança para `Human` vence uma resposta de IA concorrente ainda não enviada.
- **BR-005:** somente mensagem recebida do cliente abre/renova a janela de 24 horas.
- **BR-006:** marketing, promoção ou template proativo não é enviado pelo MVP; template transacional aprovado somente é permitido pela API Oficial.
- **BR-007:** preço exibido é estimativo e versionado; unidades originais são a fonte auditável.
- **BR-008:** credenciais de IA são administradas pela plataforma; o PlatformAdmin pode provisioná-las, rotacioná-las e testá-las sem recuperá-las em claro, e o tenant nunca pode visualizá-las ou substituí-las.
- **BR-009:** após limite controlado de tentativas, o job vai para estado morto e gera alerta.
- **BR-010:** resposta da IA é descartada se a versão da conversa mudou desde o início da geração.
- **BR-011:** para conexões Cloud, a plataforma opera um único Meta App e guarda seu `app_secret` e verify token no cofre global; cada tenant mantém titularidade de WABA, `phone_number_id`, token de acesso e faturamento. Para conexões QR, a sessão Baileys é isolada por tenant e linha, com aceite explícito do tenant para os riscos operacionais do canal.
- **BR-012:** no MVP, cada usuário pertence a no máximo um tenant; PlatformAdmin é uma permissão de plataforma sem membership de tenant.
- **BR-013:** um convite expira exatamente 24 horas após `created_at`, é consumido uma única vez e o reenvio revoga qualquer convite anterior ainda utilizável para a mesma membership/purpose.
- **BR-014:** o MVP não possui serviço de e-mail; links de ativação são entregues manualmente e nunca registrados em logs ou auditoria.
- **BR-015:** desativar uma membership rotaciona o marcador de segurança do usuário e invalida sessões; reativar a membership não revalida cookies anteriores.
- **BR-016:** a IA somente pode encaminhar para filas ativas explicitamente selecionadas em sua configuração e pertencentes ao tenant corrente; nome inexistente, fila desativada ou de outro tenant não altera a conversa.
- **BR-017:** a IA somente pode categorizar com tags ativas explicitamente selecionadas em sua configuração e pertencentes ao tenant corrente; atribuições são idempotentes e nunca removem tags automaticamente.
- **BR-018:** `assigned_queue_id` nulo representa atendimento geral; quando preenchido, uma conversa só pode ser listada, aberta ou respondida pelo Operator se possuir exatamente essa fila, sem aceitar fila inativa, inexistente ou de outro tenant na configuração.
- **BR-019:** a importação normaliza `contato` para dígitos em formato internacional de 8 a 15 caracteres, ignora duplicados do arquivo ou já existentes no tenant e nunca atualiza um contato preexistente.
- **BR-020:** capacidade de clientes inclui tenants `Pending`, `Active` e `Suspended`; linhas incluem `WhatsAppAccount` ativo desses tenants; operadores incluem membership `Operator` ativa desses tenants. Tenant `Closed` e seus recursos ficam fora dos três indicadores.
- **BR-021:** STAR libera IA e base de conhecimento, com 1 vaga de linha e 2 Operators; FLOW acrescenta BOT, tags e filas, com 2 vagas e 4 Operators; SCALA mantém todos os recursos implementados do FLOW, com 3 vagas e 8 Operators. Cada vaga pode ser provisionada como API Oficial ou QR Code, sem ultrapassar nem deixar vaga não distribuída.
- **BR-022:** a franquia efetiva é o limite mensal persistido no tenant somado às recargas de 500 registradas no mês civil UTC; `null` preserva tenants legados sem limite, `0` sem recarga bloqueia respostas da IA e valores positivos limitam o mês. A suspensão por esgotamento afeta somente respostas da IA, preservando BOT, WhatsApp e atendimento humano. O alerta começa em 80%; tokens são medidos para controle de custo, não formam uma segunda franquia operacional.
- **BR-023:** em planos comerciais, `official_api_line_count + qr_code_line_count` deve ser exatamente igual ao total de linhas do plano; valores negativos, excesso ou soma incompleta são rejeitados pelo backend.
- **BR-024:** tokens, custos, orçamento e credenciais são dados técnicos da plataforma e não podem ser retornados nem exibidos para TenantOwner ou Operator; o tenant vê apenas sua franquia e alertas operacionais.
- **BR-031:** diretrizes, perfil, conhecimento, confiança, filas, tags e handoff são configurações de atendimento do tenant e só podem ser lidos ou alterados no tenant corrente.
- **BR-032:** a categoria do conhecimento organiza o cadastro e orienta o preenchimento; cada item continua contendo um único fato oficial, e somente itens ativos semanticamente compatíveis com a intenção podem fundamentar respostas da IA. Para perguntas gerais sobre a empresa, itens ativos de serviço, geral e FAQ podem formar um resumo factual limitado.
- **BR-033:** exemplos de atendimento orientam tom, vocabulário e estrutura, nunca comprovam preço, política, prazo ou disponibilidade; exemplo sem correspondência com a mensagem atual não entra no contexto.
- **BR-034:** testes de IA por cenário são diagnósticos e não consomem a franquia de respostas, mas podem gerar tokens e custo no provedor; mensagem, prompt e resposta completos do teste não são persistidos, e a auditoria registra somente metadados sanitizados.
- **BR-025:** o contexto de IA privilegia a mensagem recente e o conhecimento mais relevante; conteúdo adicional é truncado antes da chamada ao provedor e nunca pode remover as regras obrigatórias de segurança, handoff ou formato de saída.
- **BR-026:** conhecimento da empresa não correspondente à mensagem não é considerado evidência; ausência de item relevante exige resposta genérica segura, nunca uma afirmação específica inventada, e não muda a conversa para `Human`. A transferência automática por regra de negócio ocorre por palavra-chave de fila humana autorizada ou pedido explícito de atendente; proteções críticas de segurança e indisponibilidade preservam o handoff seguro existente, e a transferência manual permanece disponível ao operador.
- **BR-027:** o perfil estruturado orienta estilo e enquadramento do atendimento; preços, políticas, disponibilidade e demais fatos operacionais devem ser consultados na base de conhecimento correspondente.
- **BR-028:** agenda desabilitada mantém compatibilidade 24 horas; agenda habilitada exige sete dias válidos, horários de abertura/fechamento coerentes e fuso permitido. Sem mensagem de fora do horário, o BOT finaliza a entrada sem criar resposta automática.
- **BR-029:** template transacional exige nome e idioma aprovados, no máximo dez parâmetros de texto de até 1.024 caracteres, e só pode ser despachado por uma conta `OfficialApi`; QR Code deve finalizar a Outbox sem chamada externa.
- **BR-030:** uma instância QR sem lease válido não pode abrir socket Baileys, gravar credenciais, enviar mensagem, publicar webhook de entrada ou encerrar a sessão; após expiração do lease, outra instância pode assumir usando as credenciais protegidas no cofre.
- **BR-035:** a finalidade padrão de IA é isolada pelo tenant e não autoriza processamento por si só; uma evidência de consentimento ativa do contato continua obrigatória antes de uma resposta automática.
- **BR-036:** somente a mensagem recebida cujo conteúdo normalizado seja exatamente `SIM` constitui aceite automatizado; a evidência registra a origem WhatsApp e a referência técnica da mensagem, sem registrar seu conteúdo em auditoria. Uma mensagem ambígua não concede consentimento.
- **BR-037:** encerrar uma conversa altera somente seu estado operacional e remove atribuições ativas; uma nova mensagem do cliente reabre o mesmo registro, retorna ao modo `Automatic` e deixa a IA usar a mensagem atual e até três mensagens anteriores.
- **BR-038:** memória institucional só pode ser criada a partir de resposta segura, com confiança mínima de 0,8 e ao menos uma fonte ativa do tenant; a pergunta deve ser sanitizada, a memória permanece tenant-scoped e não pode conter credenciais, dados pessoais ou conteúdo de outro tenant.
- **BR-039:** feedback de IA exige conversa e resposta pertencentes ao tenant corrente, registra o usuário e o horário em auditoria, permite no máximo uma avaliação vigente por resposta e só cria conhecimento corrigido após validação de tamanho e segurança.
- **BR-040:** antes de transferir por baixa confiança ou escopo, o agente deve consultar o contexto autorizado e tentar uma inferência conservadora; só deve responder quando a conclusão for sustentada por fatos compatíveis, mantendo handoff para lacuna real, pedido explícito de humano ou regra de segurança.

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
- Um Meta App compartilhado da plataforma, com `app_secret` e verify token em cofre global, para conexões WhatsApp Cloud API; ou uma ponte WhatsApp Web/Baileys com QR e segredo de webhook para conexões QR.
- Conta ou projetos dos provedores de IA contratados e administrados pela plataforma, com segregação de credenciais e limites por tenant sempre que disponível.
- Domínio, TLS, PostgreSQL, armazenamento de segredos e backup.

## 11. Extensões registradas

### Sistema de Planos e Gestão de Empresas

**Spec:** `spec-planos.md`  
**Status:** Implementado (Fase 9 - T090-T112)

Permite ao PlatformAdmin cadastrar empresas com dois tipos de plano:
- **BOT:** Todos os recursos da plataforma, exceto IA para atendimento (inbox, operadores, resposta humana, todos os modos)
- **IA+BOT:** Completo com IA para atendimento automatizado

Funcionalidades de IA são filtradas pelo plano contratado. Plano BOT não usa IA mas mantém todos os outros recursos.

### Multi-provedor de IA e configuração separada por plano

**Spec:** `spec-ai-multi-provider.md`
**Status:** Implementado (Fase 10 - T150-T165)

> **Correção de decisão (2026-08-27):** a configuração de BOT é separada da configuração de IA porque existem planos BOT e BOT + IA. O BOT deve funcionar sem provedor de IA; somente o plano BOT + IA expõe a configuração de IA.

A tela de configuração de IA deve suportar múltiplos provedores e reunir apenas as configurações de IA. Modo, mensagens e fluxo do BOT permanecem na tela e nos endpoints próprios do BOT.

**Provedores suportados e administrados pelo PlatformAdmin:**
- **OpenAI** (GPT-4o, GPT-4o-mini, etc.)
- **Google Gemini** (Gemini 2.5 Pro, Gemini 2.5 Flash, etc.)
- **Anthropic** (Claude Sonnet 4, Claude Haiku 3.5, etc.)
- **Xiaomi MiMo** (mimo-v2.5-pro, mimo-v2.5, etc.)

**Configuração na tela de IA (somente BOT + IA):**
1. Seleção de provedor e modelo autorizados para o tenant
2. Provisionamento, rotação e teste da credencial pelo PlatformAdmin
3. Diretrizes estruturadas, perfil, limiar de confiança e handoff mantidos no tenant
4. Status, limites e indicadores de custo visíveis somente ao PlatformAdmin

**Configuração na tela de BOT (BOT e BOT + IA):** modo de operação, mensagens automáticas, fluxo e fallback do BOT.

**Requisitos:**
- FR-AI-001: suportar múltiplos provedores de IA com adapter específico por provider
- FR-AI-002: credenciais são administradas pela plataforma por provedor; um tenant pode ter mais de um provedor provisionado, mas apenas um fica ativo por vez
- FR-AI-003: `AiConfigPage` e `BotConfigPage` são telas distintas e condicionadas ao pacote; IA exige `aiEnabled`, BOT está disponível nos dois planos
- FR-AI-004: testes de contrato para cada provedor devem validar a interface comum
- BR-AI-001: provedor não suportado ou credencial inválida não impede operação em modo Manual
- BR-AI-002: trocar de provedor preserva histórico de interações do provedor anterior
