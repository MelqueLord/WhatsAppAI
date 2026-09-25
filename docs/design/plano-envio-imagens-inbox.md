# Plano: envio de imagens pela Inbox

**Status:** planejado  
**Data:** 2026-09-25  
**Especificação:** [enviar mensagem](../specs/whatsapp/enviar-mensagem.md)  
**Rastreabilidade:** US-002, US-003, FR-010, FR-012, FR-023, NFR-006 e NFR-009

## Objetivo e escopo

Permitir que TenantOwner e Operator autorizados enviem uma imagem JPEG ou PNG de até 5 MB, com legenda opcional, a partir da Inbox para conversas abertas em linhas Cloud API ou QR Code. O usuário verá o estado normal de entrega da Outbox e poderá abrir ou baixar mídia recebida pelo endpoint autenticado da plataforma.

Ficam fora deste incremento: documentos, áudio, vídeo, stickers, álbum, visualização única, mídia gerada pela IA, envio em broadcast e qualquer URL pública de arquivo. A IA continua respondendo somente texto.

## Decisões de projeto

| Decisão | Motivo | Alternativa descartada |
|---|---|---|
| Limitar a JPEG/PNG e 5 MB | É o subconjunto comum e documentado para imagem na Cloud API; permite um contrato idêntico para ambos os canais. | Aceitar todo `image/*` e 16 MB, pois diverge da Meta e gera falha tardia. |
| Persistir uma referência de anexo protegida, não `data:` URL em `Message.MediaUrl` | Evita gravar conteúdo binário em texto, permite retry durável e separa retenção de mensagem. | Encaminhar o arquivo diretamente ao provedor, que perde a recuperação após timeout/reinício. |
| Usar upload Meta e `media_id` na Cloud API | Evita URL pública e torna a mensagem independente de hospedagem externa. | Enviar `link` público ou presigned, que amplia a superfície de exposição. |
| Transferir à ponte QR por streaming autenticado interno | Baileys suporta stream/URL/Buffer; streaming evita duplicar base64 e o limite JSON atual de 2 MB. | Incluir base64 no JSON da ponte, que é ineficiente e excede seu limite. |
| Preservar Outbox como único despachante | Mantém idempotência, retry, estados e auditoria atuais. | O endpoint de upload enviar diretamente ao canal. |

## Pré-requisito bloqueador de QR

O caminho QR não pode ser implementado antes de **T262** aceitar o [ADR-0015](../decisoes/0015-isolamento-servico-ponte-qr.md) e de **T263** restringir os comandos internos da ponte. O plano presume uma rota interna não pública com identidade de serviço rotacionável ou mTLS, conforme a decisão aprovada. Enquanto isso, a fatia Cloud pode ser planejada e implementada sem expor a ponte.

## Modelo de dados e retenção

Criar uma entidade tenant-scoped de anexo de saída, por exemplo `OutboundMediaAttachment`, vinculada um-para-um à `Message` de mídia. Ela contém somente: identificadores UUID, `TenantId`, tipo MIME validado, tamanho, nome original sanitizado opcional, hash de integridade, conteúdo protegido ou referência protegida ao conteúdo, estado de retenção, `CreatedAt` e `PurgeAfter`.

`Message` continua sendo a fonte de verdade do estado de entrega e mantém apenas a referência ao anexo, tipo e legenda. O modelo não persiste URL privada da Meta nem dados de conexão QR. A finalidade declarada pelo produto é exclusivamente transmitir uma informação ao cliente: a migration deve ter `Down`, índice iniciado por `TenantId` e a Outbox deve eliminar o conteúdo protegido assim que o envio alcançar `Sent`, `Failed` terminal ou dead-letter. O histórico conserva o evento, sem reter o binário.

## Contratos

### Browser para WebApi

`POST /api/conversations/{conversationId}/media` recebe `multipart/form-data` com `file` e `caption` opcional. A resposta contém somente `messageId`, `status`, `type`, `caption` sanitizada e `createdAt`. O cliente valida extensão/MIME/tamanho antes do envio apenas para experiência; o servidor valida tipo real, tamanho e autorização como fonte de verdade.

### WebApi para Meta

1. A Outbox abre o anexo protegido em stream.
2. O adaptador envia `multipart/form-data` a `/{phone-number-id}/media`, com token do cofre, arquivo e MIME.
3. Com o `media_id` retornado, envia a mensagem `image` a `/{phone-number-id}/messages`, com legenda quando houver.
4. O `media_id` e URLs temporárias nunca deixam o processo de integração ou entram em log.

### WebApi para ponte QR

Após ADR-0015/T263, a Outbox entrega o stream e os metadados mínimos por rota interna autenticada da instância que possui o lease. A ponte resolve o destinatário e chama Baileys com `{ image: stream, caption, mimetype }`. O contrato inclui chave de idempotência da mensagem, `tenantId`, linha QR, hash/tamanho e correlação; não aceita `tenantId` confiado ao browser nem comandos pela superfície pública.

Para entrada QR, a ponte identifica `imageMessage`, preserva caption e uma referência de mídia recuperável. O backend recupera o binário por canal interno autenticado e o serve pelo mesmo controle de autorização aplicado às mídias Cloud. Não deve salvar snapshot independente de conversa em texto puro, conforme CR-007.

## Fases de implementação

### Fase 0 — decisões e testes que falham primeiro

1. Concluir T262 e T263 para definir identidade, rede e contrato interno da ponte QR.
2. Registrar a política de retenção de anexo e o contrato de limpeza/anonimização.
3. Escrever testes de regressão que evidenciem o estado atual: cliente Cloud e QR recusam mídia; QR não preserva referência de imagem recebida; endpoint não deve aceitar MIME/tamanho divergentes.

### Fase 1 — núcleo durável e API da Inbox

1. Criar entidade/repositório de anexo e migration reversível, com `TenantId`, criptografia/proteção e limpeza idempotente.
2. Substituir a persistência de `data:` URL por referência de anexo; tornar o endpoint `multipart` exclusivo de JPEG/PNG até 5 MB e validar assinatura do arquivo, não apenas `Content-Type` informado.
3. Reusar as políticas existentes de conversa aberta, tenant ativo, linha/fila de Operator e janela de 24 horas.
4. Publicar evento SignalR sem binário, URL, legenda completa ou nome original; a UI invalida a conversa e reflete `Queued`.

### Fase 2 — Cloud API oficial

1. Implementar `SendMediaMessageAsync` no adaptador Meta com upload multipart e payload de mensagem por `media_id`.
2. Fazer a Outbox obter o stream do anexo, usar a chave de idempotência existente e classificar falhas transitórias versus terminais sem vazar detalhes do provedor.
3. Confirmar que timeout entre upload e envio não duplica a mensagem: a recuperação deve reconciliar a intenção antes de novo envio.
4. Manter o endpoint autenticado de download para imagens recebidas e não expor a URL temporária da Meta.

### Fase 3 — QR Code/Baileys

1. Implementar o endpoint interno da ponte apenas conforme ADR-0015 aprovado; validar identidade de serviço, lease, tenant, linha, tamanho, hash e idempotência.
2. Implementar o envio Baileys em stream e retornar o identificador externo para a Outbox.
3. Evoluir o webhook QR para distinguir texto e `imageMessage`, persistir tipo/legenda/referência e suportar recuperação autenticada do binário.
4. Testar troca de proprietário do lease e indisponibilidade da ponte sem envio duplicado ou acesso de outra linha/tenant.

### Fase 4 — experiência e operação

1. Adicionar seletor de imagem, prévia local descartável, legenda e estado de envio na `MessagePanel`; impedir envio duplicado enquanto há upload pendente.
2. Exibir imagem recebida/enviada por URL de objeto efêmera criada a partir do endpoint autenticado; revogá-la ao desmontar o componente.
3. Medir tamanho, duração, resultado e tentativas somente com IDs/categorias sanitizadas; criar alerta para falhas e anexos presos além da idade esperada.
4. Atualizar contexto de WhatsApp, runbook e checklist de produção com smoke Cloud e QR.

## Testes e critérios de aceite

| Área | Evidência necessária |
|---|---|
| Domínio e persistência | anexo requer tenant, só aceita JPEG/PNG até 5 MB, não persiste data URL, migration sobe/desce e limpeza respeita retenção. |
| Autorização/tenant | usuário de outro tenant, Operator sem linha/fila e conversa fechada não enviam nem baixam mídia; teste negativo entre dois tenants é obrigatório. |
| Cloud | fake HTTP verifica upload multipart, envio por `media_id`, token somente no header, bloqueio fora de 24h, retry e timeout sem duplicar. |
| QR | teste de contrato verifica identidade interna, lease, stream, resposta Baileys, reconexão e negação a rota pública; depende de T262/T263. |
| Entrada | webhooks Cloud e QR preservam tipo, legenda e referência; download autenticado não revela token nem URL privada. |
| Frontend | seleção, erro de tipo/tamanho, legenda, `Queued`/falha/sucesso, prévia e limpeza de object URL. |
| Operação | logs com sentinelas provam ausência de bytes, tokens, telefones, legenda e URL privada; smoke usa contas de teste, nunca conta de cliente. |

## Riscos e controles

| Risco | Controle |
|---|---|
| Mídia contém dado pessoal ou malicioso | MIME e assinatura validados, limite estrito, armazenamento protegido, retenção/anonimização e nenhum conteúdo em logs. |
| Duplicação após timeout | intenção e Outbox atômicas, chave idempotente, reconciliação antes de novo envio. |
| Vazamento por link de mídia | nenhum link público; download apenas pelo backend autenticado no tenant atual. |
| Ponte QR exposta | bloquear a fase QR até ADR-0015/T262–T263 e usar somente canal interno autenticado. |
| Crescimento de banco | máximo inicial de 5 MB, limpeza idempotente, métricas de idade/volume e reavaliação por evidência antes de storage externo. |

## Ordem sugerida

`T262 → T263 → Fase 0 → Fase 1 → Fase 2 → Fase 3 → Fase 4`.

As tarefas canônicas devem receber novos IDs somente depois de reconciliar a colisão atual de numeração T255 entre `docs/tasks/plataforma.md` e `docs/tasks/seguranca-e-prontidao.md`; este plano não reusa esses IDs para evitar perder rastreabilidade.
