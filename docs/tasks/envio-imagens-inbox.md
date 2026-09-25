# Tarefas: envio transitório de imagens pela Inbox

**Status:** em execução  
**Especificação:** [enviar mensagem](../specs/whatsapp/enviar-mensagem.md)  
**Design:** [plano de envio de imagens](../design/plano-envio-imagens-inbox.md)

## Dependências

`T270 → T271 → T272 → T273 → T274`. A fatia QR depende adicionalmente de `T262 → T263` no backlog de segurança; ela não é iniciada por estas tarefas.

## Fase 1 — Cloud API

- [X] **T270** Formalizar a finalidade transitória do anexo: transmitir uma informação ao cliente e remover o binário em estado terminal. **Refs:** US-003, FR-010, FR-023, NFR-006. **Paths:** especificação e plano. **Aceite:** nenhum prazo inventado; o histórico não exige retenção do binário.
- [ ] **T271** Criar armazenamento protegido e tenant-scoped do anexo de saída, com migration reversível e remoção idempotente. **Refs:** FR-010, FR-023, NFR-006, NFR-009. **Paths:** Domain Messaging, AppDbContext, configurações, migration, repositório e testes. **Depends:** T270. **Aceite:** JPEG/PNG até 5 MB permanece recuperável somente pela Outbox até estado terminal; não há `data:` URL em Message nem acesso cruzado entre tenants.
- [ ] **T272** Implementar upload multipart e envio por `media_id` no adaptador Cloud API. **Refs:** FR-010, FR-012, NFR-009. **Paths:** IWhatsAppClient, WhatsAppClient, Outbox worker e testes de contrato. **Depends:** T271. **Aceite:** o token só aparece no header, a Meta recebe upload e mensagem `image`, e falhas transitórias não duplicam a intenção.
- [ ] **T273** Restringir a API/UI a imagem JPEG/PNG de até 5 MB e refletir estado de envio sem expor conteúdo. **Refs:** US-003, FR-023, NFR-006. **Paths:** ConversationEndpoints, api.ts, MessagePanel e testes. **Depends:** T271. **Aceite:** validação servidor é autoritativa; autorização de tenant/linha/fila e janela de 24 horas permanecem intactas.
- [ ] **T274** Validar fatia Cloud e documentar evidências. **Refs:** FR-010, FR-012, FR-023, NFR-006, NFR-009. **Paths:** testes unitários, integração, frontend e contexto WhatsApp. **Depends:** T272, T273. **Aceite:** build, lint, testes afetados, teste negativo de tenant, retry e revisão do diff passam.

## Fase 2 — QR Code

- [ ] **T275** Implementar transporte interno e mídia QR conforme ADR-0015 aceito. **Refs:** CR-006, CR-007, FR-010, FR-023. **Depends:** T262, T263, T264, T271. **Aceite:** rota pública não envia mídia; ponte usa lease, identidade interna e stream; entrada QR preserva referência recuperável sem snapshot de conversa.
