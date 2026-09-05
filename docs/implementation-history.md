# Histórico consolidado de implementação

**Atualizado em:** 2026-09-05
**Escopo:** resumo do que foi implementado no projeto WhatsAppAI até esta data.
**Fonte de verdade:** código, testes, migrations, especificação e ADRs versionados.

Este documento serve como mapa de entrega e operação. Ele não contém credenciais, tokens, chaves, conteúdo de `.env` ou dados de clientes.

## 1. Visão geral da solução

O projeto é um SaaS multiempresa para atendimento pelo WhatsApp, com uma instalação em produção na Hostinger. Cada empresa possui configuração, dados, filas, contatos, histórico e memória isolados por `TenantId`.

A arquitetura implementada é um monólito modular:

- **Backend:** .NET 10, ASP.NET Core, EF Core, PostgreSQL e workers duráveis.
- **Frontend:** React 19.2, TypeScript e Vite.
- **Operação:** Docker Compose, Nginx como proxy reverso/TLS e SignalR para atualização da Inbox.
- **Mensageria:** Inbox/Outbox, idempotência, retries seletivos e revalidação antes do envio.
- **Canais:** WhatsApp Cloud API oficial e ponte WhatsApp Web por QR Code, com sessão persistida.
- **IA:** provedores administrados pela plataforma atrás de interfaces próprias; o modelo sugere, mas o backend decide o que pode ser enviado.

O fluxo principal é: receber webhook, persistir a mensagem, carregar o contexto do tenant, decidir entre automático/fila/humano, chamar a IA quando permitido, validar a resposta, criar a Outbox e enviar pelo canal correto.

## 2. Plataforma, acesso e segurança

- Login, logout, cookie seguro, antiforgery, `auth/me` e invalidação por estado/security stamp.
- Papéis de PlatformAdmin, TenantOwner e Operator, com permissões por tenant, fila e linha.
- Provisionamento e suspensão de empresas, convites de operadores e controle de capacidade.
- Isolamento de consultas e gravações por tenant; referências de outro tenant são rejeitadas ou ignoradas.
- Segredos tratados por `ISecretStore`, sem chave de provedor em texto puro, bundle frontend ou log.
- Webhooks com validação de autenticidade, idempotência, classificação e reprocessamento auditado.
- Auditoria sanitizada, correlação operacional, limites de uso e health checks.
- Janela de atendimento de 24 horas respeitada; texto livre fora da janela não é liberado no MVP.

## 3. WhatsApp, contatos e Inbox

- Conexão independente por linha Cloud API ou QR Code.
- Sessões QR com ownership/lease, health check e volume persistente; reconectar ou reiniciar os containers de aplicação não apaga a sessão.
- Normalização de telefone, incluindo o padrão brasileiro com nono dígito após o DDD.
- Deduplicação e atualização de contato para evitar registros com zero indevido no final ou números equivalentes duplicados.
- Entrada de mensagens e status normalizados, com janela renovada somente por mensagem recebida do cliente.
- Inbox paginada, mensagens em tempo real, mídia protegida e resposta humana com idempotência.
- Layout responsivo corrigido para celular: menu, botões, modais e mensagens não devem sair da área visível; o conteúdo da conversa pode rolar sem cortar o texto.
- Conversa pode ser encerrada pelo operador, movida para encerradas mantendo o histórico e reaberta no mesmo ID quando o cliente retornar.
- Ao retornar, a IA reutiliza até quatro mensagens recentes autorizadas para continuar o assunto.

## 4. BOT, filas e atendimento humano

- Modos `Manual`, `SimpleAutoReply` e `AiPowered`, com exclusividade operacional preservada.
- Saudação, retorno, fallback, mensagem offline, handoff, transferência, etapas e palavras-chave configuráveis.
- Horário de atendimento por dia e fuso horário, com resposta fora do expediente quando configurada.
- Filas e tags autorizadas por empresa, com validação de plano e escopo.
- Palavra-chave de fila atribui a conversa à fila e mantém o modo **Automatic**.
- A conversa só muda para **Human** quando um operador assume, quando o cliente pede explicitamente uma pessoa ou quando uma regra de segurança/handoff realmente exige isso.
- Enquanto aguarda em uma fila, a IA continua respondendo no automático com contexto; também pode informar a fila atual e orientar o cliente a enviar a palavra-chave de outra fila.
- Aviso de fila pode ser enviado antes da mensagem de permanência, conforme configuração.
- Transferências automáticas e manuais ficam auditadas; takeover humano concorrente impede resposta automática posterior.

## 5. Preparação personalizada da IA por empresa

A configuração de IA passou a reunir, no tenant, os elementos necessários para o agente trabalhar como um funcionário daquela empresa:

- provedor/modelo e ativação controlados pela plataforma;
- diretrizes livres e regras estruturadas de comportamento, segurança e handoff;
- tipo de negócio, público, serviços, localização, horário, identidade e tom de conversa;
- mensagem de boas-vindas e instruções de continuidade;
- base de conhecimento categorizada para FAQ, serviço, preço, horário, pagamento, localização, política e informações gerais;
- exemplos de atendimento para orientar estilo e fluxo;
- filas, tags, confiança, limite de resposta e comportamento fora do horário;
- simulação diagnóstica que mostra decisão, confiança, handoff e fontes/dados utilizados;
- controle de versão, rascunho, publicação e concorrência otimista nas configurações aplicáveis.

A tela de IA foi organizada em etapas de preparação do agente, para reduzir a necessidade de repetir a mesma informação em telas diferentes. A base de conhecimento continua sendo a fonte factual; exemplos orientam a forma de atender e não substituem fatos.

## 6. Contexto, busca e personalização

- Contexto específico por atendimento com identidade segura da empresa, nome sanitizado do contato, primeiro contato/continuidade, fila atual e histórico recente.
- Seleção de conhecimento local com termos, conceitos, sinônimos, intenção, categoria, prioridade e tolerância a pequenas variações de escrita.
- Perguntas como “o que a empresa faz?”, “para que serve”, “preço” e paráfrases devem buscar fatos compatíveis antes de considerar handoff.
- Até seis fontes locais relevantes entram no contexto; conteúdo é sanitizado e limitado para controlar custo.
- Diretrizes, perfil, conhecimento, exemplos, memória institucional, memória consentida do cliente e histórico são combinados sem cruzar empresas.
- Perguntas genuinamente genéricas podem usar conhecimento público/pesquisa web quando habilitado, sem transformar conteúdo público em fato da empresa ou memória institucional.
- A IA é orientada a responder primeiro, aproveitar o histórico, evitar repetir saudação/pergunta, usar tom de conversa e fazer no máximo uma pergunta útil.
- Respostas de WhatsApp permanecem limitadas a 160 caracteres quando a política do tenant exigir esse limite.

## 7. Memória, consentimento e aprendizado supervisionado

- Finalidade padrão de atendimento automatizado por IA criada para novas empresas.
- Consentimento do contato registrado de forma idempotente; sem consentimento, a automação não grava memória pessoal.
- Memória individual do cliente exige consentimento ativo, é curta, sanitizada, tenant/contact-scoped, auditável, revogável e expirável.
- Memória institucional da empresa é separada da memória pessoal e só aproveita respostas seguras e fundamentadas em conhecimento ativo.
- A memória não é salva antes da validação final do envio: se um operador assumir durante a geração, a resposta e a memória associada são descartadas juntas.
- Feedback do operador (`Helpful` ou correção textual segura) pode gerar exemplo supervisionado do próprio tenant, sem enviar nova mensagem ao cliente.
- Exemplos supervisionados mantêm origem, sanitização, ativação, edição/desativação e concorrência otimista.
- Feedback, memória e exemplos não habilitam fine-tuning, não compartilham dados entre empresas e não registram prompts completos ou PII sem mascaramento.

## 8. Decisão de resposta e proteção contra invenção

O backend aplica uma ordem determinística:

1. segurança e pedido explícito de humano;
2. validação de janela, consentimento, plano, credencial e orçamento;
3. contexto autorizado da empresa e histórico;
4. inferência conservadora para perguntas relacionadas ou genéricas;
5. escolha de fila sem desligar o automático;
6. validação da saída e revalidação da conversa;
7. criação da mensagem e da Outbox.

Antes do envio, `AiGroundingPolicy` verifica valores concretos produzidos pela IA, como preço, horário, prazo, percentual, data, link e contato. Se o valor não estiver no contexto autorizado, a resposta não é enviada e vira `out_of_scope`, preservando o handoff seguro. Pesquisa pública só passa quando foi explicitamente permitida para uma pergunta genérica.

Essa proteção impede que a IA invente um plano, preço ou condição apenas porque parece plausível. O tipo de negócio e o tom orientam a linguagem e a condução, mas nunca autorizam inventar catálogo, disponibilidade, política ou preço.

## 9. Uso, custos e operação

- Franquia mensal de respostas por empresa com reserva, commit, release e reconciliação de reservas pendentes.
- Preços por provedor/modelo versionados no painel administrativo; custos reais ficam restritos ao PlatformAdmin.
- Uso do tenant mostra respostas, saldo e alertas, sem expor chaves, tokens ou custo operacional indevido.
- Migrations reversíveis acompanham mudanças persistentes; em produção, o serviço `migrate` deve rodar antes da aplicação.
- Falhas de provedor usam retry limitado e finalização segura, sem loops infinitos.
- Idempotência evita mensagens duplicadas e a revalidação impede envio após takeover humano, mudança de modo ou expiração da janela.

## 10. Publicação segura na Hostinger

O ambiente de produção usado é `/opt/atenz/WhatsAppAI`, com a branch `master` como fonte de atualização.

Sequência registrada para cada publicação:

1. verificar branch, status do Git, existência de `.env` e certificado PFX sem imprimir segredos;
2. fazer `fetch` e `pull --ff-only` de `origin/master`; conflitos devem ser resolvidos antes de continuar;
3. validar `docker compose config --quiet`;
4. construir as imagens necessárias;
5. executar o serviço de migration;
6. recriar apenas `api`, `worker`, `frontend` e `nginx`;
7. manter `postgres` e `whatsapp-web` sem remoção de volumes para preservar dados e sessão;
8. conferir estado dos containers, logs sanitizados e endpoints `/health/live` e `/health/ready`;
9. conferir HTTPS e o endpoint de CSRF sem expor cookie/token;
10. confirmar que a ponte WhatsApp continua saudável e que o volume de sessão permanece presente.

Não executar em produção `docker compose down -v`, `docker system prune` ou remoção de volumes para “limpar cache”. O cache de imagem/front-end é resolvido por build/recriação controlada; a sessão do WhatsApp é persistência operacional e deve ser preservada.

## 11. Validações realizadas e limites conhecidos

- `git diff --check` validado para o incremento atual.
- Testes e build do frontend executados com sucesso no incremento anterior.
- Testes .NET ficaram bloqueados no ambiente local porque o SDK disponível é 8.0.424 e o projeto exige o SDK 10.0.302 definido em `global.json`; isso não foi mascarado nem alterado.
- O lint frontend possui falha preexistente em `AdminTenantsPage.tsx`, fora do escopo destas melhorias.
- RAG vetorial, re-ranking com banco vetorial e citações formais de fonte continuam fora do MVP; a busca semântica atual é determinística e sem dependência de Redis/RabbitMQ.

Antes de abrir uma nova frente, consultar também `docs/runbooks/implemented-flows.md`, `docs/ai/behavior-policy.md`, `docs/architecture/architecture.md`, `docs/security/` e `specs/000-platform/`.

## 12. Rastreamento das últimas entregas

- **T225–T226:** finalidade e consentimento padrão para IA.
- **T227:** encerramento, fila de encerradas e retomada da conversa.
- **T228–T229:** memória institucional e feedback do operador.
- **T230–T234:** inferência segura, tipos de negócio, conhecimento público, personalização e busca semântica.
- **T235:** memória individual com consentimento.
- **T236:** aprendizado supervisionado por empresa.
- **T237:** encaminhamento determinístico, filas automáticas e handoff humano explícito.
- **T238:** respostas naturais e contextualizadas.
- **T239:** proteção contra informação inventada antes do envio.
- **T240:** este histórico consolidado e runbook de atualização segura em produção.
