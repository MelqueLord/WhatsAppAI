# Feature Specification: LGPD Production Readiness

**Feature Branch**: `master`

**Created**: 2026-08-27

**Status**: Approved for implementation

**Input**: User description: "Preparar a plataforma para produção com as correções necessárias de LGPD, sem criar um bloqueio genérico."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar fundamento do tratamento (Priority: P1)

Como responsável de um tenant, quero declarar a finalidade e a base legal usada para tratar dados de contatos, registrando evidência quando a base escolhida for consentimento, para que o tratamento não dependa de consentimento indiscriminado nem fique sem justificativa verificável.

**Why this priority**: Sem finalidade e base legal registradas, os demais controles não demonstram por que o dado é tratado.

**Independent Test**: Configurar a base legal de um tenant, registrar e revogar um consentimento quando aplicável e verificar que outro tenant não consegue consultar ou alterar esses registros.

**Acceptance Scenarios**:

1. **Given** um TenantOwner autenticado, **When** informa finalidade, base legal e prazo de retenção, **Then** a configuração fica registrada apenas para seu tenant com autor e data.
2. **Given** uma finalidade baseada em consentimento, **When** o consentimento é registrado ou revogado, **Then** a evidência e seu histórico permanecem auditáveis.
3. **Given** uma finalidade baseada em outra hipótese legal, **When** a configuração é salva, **Then** o sistema não exige nem simula consentimento.

---

### User Story 2 - Atender direitos do titular (Priority: P1)

Como TenantOwner, quero localizar os dados de um contato, exportá-los e executar anonimização ou exclusão justificada, para atender solicitações do titular de forma rastreável e isolada por tenant.

**Why this priority**: A plataforma hoje não oferece fluxo operacional para acesso, portabilidade ou eliminação de dados pessoais.

**Independent Test**: Abrir uma solicitação para um contato, gerar o pacote de dados, executar a eliminação e provar que os dados pessoais deixaram de aparecer sem afetar contato homônimo de outro tenant.

**Acceptance Scenarios**:

1. **Given** um contato do tenant corrente, **When** o TenantOwner solicita acesso ou portabilidade, **Then** recebe um pacote legível e estruturado com os dados pessoais mantidos pela plataforma.
2. **Given** uma solicitação de eliminação válida, **When** o TenantOwner a executa, **Then** dados pessoais são apagados ou anonimizados e o resultado fica registrado sem preservar o conteúdo eliminado.
3. **Given** obrigação de retenção ou disputa, **When** a eliminação é impedida, **Then** a decisão exige justificativa, prazo de revisão e registro auditável.
4. **Given** identificador pertencente a outro tenant, **When** qualquer operação é tentada, **Then** nenhum dado ou confirmação de existência é revelado.

---

### User Story 3 - Manter evidências de governança (Priority: P2)

Como responsável pela plataforma, quero manter RIPD, responsabilidades de controlador/operador, canal de privacidade e dados do encarregado como artefatos configuráveis e versionados, para publicar informações reais no ambiente de produção sem gravá-las no código.

**Why this priority**: Governança e transparência dependem de informações da organização que não podem ser inventadas pela implementação.

**Independent Test**: Preencher a configuração de produção e verificar que a política e o canal de direitos exibem os dados informados, enquanto a documentação registra riscos, controles e responsáveis.

**Acceptance Scenarios**:

1. **Given** dados institucionais configurados, **When** uma pessoa acessa a página de privacidade, **Then** encontra controlador, canal, encarregado ou justificativa de dispensa e informações sobre seus direitos.
2. **Given** dados institucionais ausentes, **When** o sistema inicia, **Then** a ausência é reportada como pendência operacional sem expor valores fictícios ao público nem derrubar as funções de atendimento.
3. **Given** uma mudança de finalidade, fornecedor ou risco, **When** a revisão de privacidade ocorre, **Then** o RIPD registra versão, decisão, mitigação e responsável.

### Edge Cases

- Um mesmo número pode existir em tenants diferentes; toda configuração, evidência e solicitação deve usar também `TenantId`.
- Revogar consentimento não elimina automaticamente dados cuja retenção ainda possua outra base legal válida; a decisão deve ser registrada.
- Eliminação deve preservar somente evidência mínima sem conteúdo pessoal quando houver obrigação legal ou necessidade de defesa.
- Solicitações repetidas ou concorrentes para o mesmo contato devem ser idempotentes e não produzir exportações divergentes.
- Dados já enviados à Meta/OpenAI seguem também os contratos e mecanismos de atendimento desses operadores/suboperadores.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-LGPD-001**: O sistema MUST registrar, por tenant e finalidade, a base legal, descrição, prazo de retenção, estado e histórico de alteração.
- **FR-LGPD-002**: O sistema MUST registrar prova e revogação de consentimento somente para finalidades cuja base legal seja consentimento.
- **FR-LGPD-003**: O sistema MUST permitir ao TenantOwner abrir e acompanhar solicitações de acesso, portabilidade, correção, anonimização, bloqueio ou eliminação ligadas a um contato do tenant.
- **FR-LGPD-004**: O sistema MUST gerar exportação estruturada dos dados pessoais do contato e seus relacionamentos pertencentes ao tenant corrente.
- **FR-LGPD-005**: O sistema MUST anonimizar ou excluir dados pessoais do contato e conteúdo de conversas, preservando apenas registros mínimos que sejam necessários e não contenham o conteúdo eliminado.
- **FR-LGPD-006**: O sistema MUST exigir justificativa e data de revisão para negar, bloquear ou adiar uma eliminação.
- **FR-LGPD-007**: Toda operação MUST aplicar isolamento por tenant e autorização de TenantOwner ou PlatformAdmin em sessão de suporte auditada.
- **FR-LGPD-008**: Toda transição de solicitação MUST registrar autor, instante, ação e resultado sem copiar dados pessoais ou conteúdo completo para logs/auditoria.
- **FR-LGPD-009**: A plataforma MUST publicar aviso de privacidade e canal de direitos a partir de configuração de ambiente, sem valores institucionais fictícios no código.
- **FR-LGPD-010**: O repositório MUST conter RIPD inicial, matriz controlador/operador/suboperador, registro de bases legais e procedimento de resposta a titulares e incidentes.
- **FR-LGPD-011**: A implantação MUST permanecer funcional quando dados institucionais ainda não estiverem configurados, sinalizando a pendência em health/readiness administrativo sem bloquear conversas.
- **FR-LGPD-012**: Mudanças de banco MUST possuir migration reversível e teste de isolamento por tenant no PostgreSQL/Supabase adotado para homologação e para o candidato de produção.

### Key Entities

- **ProcessingPurpose**: Finalidade de tratamento de um tenant, com base legal, retenção, estado e autoria.
- **ConsentEvidence**: Evidência vinculada a contato e finalidade quando a base legal escolhida é consentimento, incluindo concessão, origem e revogação.
- **DataSubjectRequest**: Solicitação de direito do titular, seu tipo, estado, prazos, decisão e referências sem duplicar conteúdo pessoal.
- **PrivacyIdentity**: Configuração ambiental pública do controlador, canal de privacidade e encarregado ou fundamento de dispensa.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das novas finalidades ativas possuem base legal e prazo de retenção identificados.
- **SC-002**: Um TenantOwner conclui exportação ou eliminação de um contato em até 5 minutos, excluído o tempo de validação jurídica da solicitação.
- **SC-003**: Testes automatizados demonstram zero leitura, alteração, exportação ou eliminação cruzada entre tenants.
- **SC-004**: 100% das transições de solicitações ficam auditáveis sem armazenar conteúdo pessoal no evento de auditoria.
- **SC-005**: Política, RIPD, matriz de responsabilidades e procedimento operacional possuem versão, responsável e data de revisão antes da publicação da release.

## Assumptions

- O tenant é controlador dos dados de seus contatos e conversas; a plataforma atua como operadora nesse contexto. A plataforma é controladora dos dados de sua própria conta, segurança e faturamento.
- Consentimento é uma possível base legal, não requisito universal; cada finalidade deve usar a hipótese adequada validada pelo responsável do negócio.
- O TenantOwner recebe e valida a identidade do titular fora da plataforma nesta primeira versão; a plataforma registra e executa a solicitação validada.
- A eliminação segue exceções legais aplicáveis e prefere anonimização irreversível quando a remoção física quebraria integridade referencial ou evidência mínima necessária.
- Supabase/PostgreSQL continua sendo o ambiente de homologação e validação desta correção; a conexão e segredos permanecem somente na configuração local/ambiente.
- O destino público, domínio e dados institucionais serão fornecidos por configuração de produção, não incorporados ao repositório.

## Out of Scope

- Emitir parecer jurídico ou substituir validação do responsável legal da organização.
- Receber automaticamente solicitações por WhatsApp sem validação de identidade pelo tenant.
- Apagar dados mantidos exclusivamente por Meta, OpenAI ou outro terceiro; o procedimento documenta como encaminhar a solicitação a esses operadores.
