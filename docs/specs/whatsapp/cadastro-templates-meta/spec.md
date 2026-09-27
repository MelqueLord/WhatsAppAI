# Feature Specification: Cadastro de templates na Meta

**Feature Branch**: `master`

**Created**: 2026-09-26

**Status**: Draft

**Input**: Permitir que o TenantOwner cadastre, envie para análise e acompanhe templates da API Oficial do WhatsApp diretamente pela plataforma.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cadastrar template de atendimento (Priority: P1)

Como TenantOwner, quero criar um template textual para uma conta oficial e submetê-lo à Meta, para preparar mensagens que possam ser usadas fora da janela de atendimento sem sair da plataforma.

**Why this priority**: Sem a submissão, a plataforma continua dependente do WhatsApp Manager e não entrega o objetivo principal do incremento.

**Independent Test**: Em uma empresa com linha oficial ativa, o TenantOwner informa nome, idioma, categoria, corpo, exemplos das variáveis e rodapé opcional; a submissão válida retorna identificação e estado de análise, sem expor credenciais.

**Acceptance Scenarios**:

1. **Given** uma linha oficial ativa e autorizada, **When** o TenantOwner envia um template de Utilidade válido, **Then** o sistema o submete à conta empresarial correta e mostra que está em análise.
2. **Given** um corpo com variáveis posicionais, **When** o TenantOwner não informa um exemplo para cada variável, **Then** o sistema rejeita o cadastro antes da submissão e indica os campos que precisam de correção.
3. **Given** uma conexão QR Code, inativa ou de outro tenant, **When** o usuário tenta cadastrar um template, **Then** a operação é recusada sem chamada à Meta.
4. **Given** um Operator autenticado, **When** tenta acessar o cadastro de templates, **Then** o sistema nega a operação.

---

### User Story 2 - Acompanhar análise e disponibilidade (Priority: P1)

Como TenantOwner, quero acompanhar aprovação, rejeição e mudanças posteriores do template, para saber quando ele pode ser usado e como corrigir uma rejeição.

**Why this priority**: A criação é assíncrona e não tem valor operacional sem visibilidade do resultado da análise.

**Independent Test**: Um evento válido da Meta atualiza o template da conta correta e a tela apresenta estado, categoria efetiva e motivo sanitizado, inclusive sem recarregamento manual prolongado.

**Acceptance Scenarios**:

1. **Given** um template em análise, **When** a Meta informa aprovação, **Then** o estado local passa a aprovado e o template fica elegível às regras de envio já existentes.
2. **Given** um template em análise, **When** a Meta informa rejeição, **Then** a tela mostra a razão e a recomendação fornecidas, sem registrar conteúdo sensível em logs.
3. **Given** uma alteração feita diretamente no WhatsApp Manager, **When** o sistema recebe o evento ou executa sincronização, **Then** o catálogo local converge para o estado vigente na Meta.
4. **Given** um evento de outra conta empresarial, **When** ele é processado, **Then** nenhum template ou tenant diferente é alterado.

---

### User Story 3 - Consultar catálogo gerenciado (Priority: P2)

Como TenantOwner, quero visualizar os templates da conta oficial com idioma, categoria, estado e compatibilidade, para administrá-los e entender quais podem ser usados pela Inbox ou por disparos permitidos.

**Why this priority**: A consulta unifica templates criados pela plataforma e templates externos, reduzindo dúvidas operacionais sem ampliar as regras de envio.

**Independent Test**: Ao abrir a gestão de templates, o TenantOwner vê um catálogo paginado ou limitado da conta escolhida, incluindo itens criados fora da plataforma e a indicação de compatibilidade com os fluxos atuais.

**Acceptance Scenarios**:

1. **Given** uma conta com templates em vários idiomas e categorias, **When** o TenantOwner consulta o catálogo, **Then** cada combinação de template e idioma aparece separadamente com seu estado atual.
2. **Given** um template aprovado com componentes ainda não suportados no envio, **When** ele aparece no catálogo, **Then** a interface o identifica como incompatível e não altera as regras atuais de envio.

### Edge Cases

- A Meta aceita a requisição, mas a resposta se perde; uma nova tentativa não deve criar submissões repetidas sem reconciliação.
- O mesmo nome existe em idiomas diferentes ou foi criado fora da plataforma.
- A Meta reclassifica a categoria solicitada ou altera o estado depois da aprovação.
- O token permite enviar mensagens, mas não possui autorização para gerenciar templates.
- A conta atinge limite de templates ou limite de criação por período.
- Eventos chegam duplicados, fora de ordem ou antes da sincronização inicial.
- Uma conta oficial é desativada enquanto existe submissão pendente.
- A Meta retorna motivo ou recomendação maiores que os limites locais permitidos.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-TPL-001**: O sistema DEVE permitir que somente TenantOwner autenticado gerencie templates de contas oficiais ativas pertencentes ao tenant corrente.
- **FR-TPL-002**: O cadastro inicial DEVE aceitar apenas categorias Utilidade e Marketing, idioma suportado, corpo textual obrigatório, rodapé textual opcional e variáveis posicionais com um exemplo por variável.
- **FR-TPL-003**: O nome DEVE usar somente letras minúsculas, números e sublinhados e respeitar o limite vigente da Meta; corpo, rodapé, variáveis e exemplos DEVEM ser validados antes da submissão.
- **FR-TPL-004**: O sistema DEVE submeter o template à conta empresarial associada à linha escolhida sem expor WABA, token ou outros segredos ao navegador além dos identificadores operacionais já autorizados.
- **FR-TPL-005**: O sistema DEVE impedir cadastro por conexão QR Code, conta inativa, conta de outro tenant ou credencial sem a autorização de gerenciamento exigida.
- **FR-TPL-006**: Cada submissão DEVE possuir uma chave idempotente e um estado operacional que permita distinguir solicitação local, aceite pela Meta, estado desconhecido e falha definitiva.
- **FR-TPL-007**: Em resultado incerto, o sistema DEVE reconciliar o catálogo da Meta antes de permitir repetição que possa criar duplicidade.
- **FR-TPL-008**: O sistema DEVE armazenar a identificação externa, nome, idioma, categoria solicitada, categoria efetiva, componentes suportados, estado, motivo/recomendação sanitizados e horários de sincronização, sempre associados ao tenant e à WABA corretos.
- **FR-TPL-009**: O catálogo DEVE incluir templates criados na plataforma e fora dela, mantendo separadas as variantes de idioma do mesmo nome.
- **FR-TPL-010**: O sistema DEVE acompanhar mudanças de estado e categoria por eventos autenticados da Meta e oferecer sincronização explícita para recuperação ou reconciliação.
- **FR-TPL-011**: Eventos de template DEVEM ser persistidos de forma idempotente antes do processamento e resolver o tenant pela WABA validada, sem depender de `phone_number_id`.
- **FR-TPL-012**: O catálogo DEVE indicar se cada template é compatível com envio individual e com disparo permitido, reutilizando as restrições vigentes sem ampliar categorias ou componentes enviáveis.
- **FR-TPL-013**: Somente o estado aprovado e compatível DEVE habilitar o template nos fluxos de envio existentes; estados pendente, rejeitado, pausado, desativado, arquivado, excluído ou desconhecido permanecem não enviáveis.
- **FR-TPL-014**: A plataforma DEVE registrar auditoria sanitizada da submissão, sincronização e mudança de estado, contendo ator, tenant, conta, identificador do template, resultado e correlação, sem token ou conteúdo completo do template.
- **FR-TPL-015**: Falhas da Meta DEVEM ser apresentadas com código e orientação sanitizados, distinguindo autorização, validação, limite, indisponibilidade e resultado incerto.
- **FR-TPL-016**: A primeira entrega NÃO DEVE oferecer edição, exclusão, apelação, templates de Autenticação, cabeçalhos, mídia, botões, variáveis nomeadas ou mudança nas regras atuais de disparo.

### Key Entities

- **Template da conta WhatsApp**: representação tenant-scoped de uma variante por nome e idioma, vinculada à WABA, com conteúdo necessário à gestão, categoria, estado, compatibilidade e identificação externa.
- **Submissão de template**: tentativa auditável e idempotente de cadastrar um template, com estado operacional separado do estado de revisão atribuído pela Meta.
- **Evento de template**: notificação autenticada e deduplicada de mudança de estado, categoria ou componentes, resolvida pela WABA.
- **Conta WhatsApp oficial**: conexão existente que fornece tenant, WABA e credencial protegida para a operação.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-TPL-001**: Em testes de aceitação, 100% dos templates válidos são associados à conta e ao tenant corretos e aparecem como submetidos em até 5 segundos, desconsiderando o tempo de análise da Meta.
- **SC-TPL-002**: 100% das tentativas com tenant, papel, canal, credencial, formato ou exemplos inválidos são bloqueadas antes de criar um template utilizável.
- **SC-TPL-003**: Eventos duplicados ou fora de ordem não produzem duplicidade nem regressão indevida de estado em 100% dos casos de contrato cobertos.
- **SC-TPL-004**: Aprovação ou rejeição recebida por evento fica visível para o TenantOwner em até 10 segundos após sua persistência.
- **SC-TPL-005**: Testes com pelo menos dois tenants demonstram zero leitura, submissão ou atualização cruzada de templates.
- **SC-TPL-006**: Uma submissão com resposta incerta pode ser reconciliada sem criação duplicada em 100% dos cenários de timeout simulados.

## Assumptions

- O primeiro incremento usa somente corpo textual, rodapé opcional e parâmetros posicionais porque esses componentes já são compatíveis com os fluxos atuais de envio.
- TenantOwner é o papel adequado para gerir conteúdo enviado à Meta; Operators apenas usam templates aprovados nos fluxos já autorizados.
- Utilidade e Marketing podem ser cadastrados, mas o cadastro não muda as restrições atuais: Marketing permanece individual na Inbox e broadcast continua restrito a Utilidade.
- A plataforma continuará usando as credenciais protegidas já vinculadas à linha oficial; o incremento apenas verificará se elas possuem autorização suficiente para gerenciar templates.
- Edição, exclusão, apelação e componentes avançados serão especificados separadamente caso haja necessidade demonstrada.
