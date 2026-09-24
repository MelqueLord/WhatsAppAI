# Pesquisa técnica

| Decisão | Motivo | Alternativa descartada |
|---|---|---|
| Apenas `UTILITY` aprovado e BODY texto | É o modelo já validado na Inbox e atende a janela de 24h sem ampliar escopo. | Marketing/authentication e componentes avançados exigem governança/modelagem própria. |
| Broadcast produz Message + Outbox | `Message.CreateOutboundTemplate` e `OutboxProcessingWorker` já enviam template, validam limites e têm retry. | Chamada Meta direta no worker perde durabilidade e cria duplicidade. |
| Revalidar no dispatch | Template pode mudar de aprovação ou estrutura depois de carregado. | Confiar no frontend ou apenas na criação. |
| Referenciar mensagem por destinatário | Permite progresso final, retry e reinício seguro. | Marcar como enviado ao enfileirar produz sucesso falso. |
| Política única linha/fila | Endpoints atuais restringem apenas parte do dispatch; coleção/detalhe podem vazar escopo. | Checagens distribuídas e incompletas. |

Riscos adicionais: o worker QR atual chama `SendTextMessageAsync` direto e tem atraso de dois segundos; esse comportamento não serve à API Oficial. O novo modo não deve chamar Meta diretamente e deve ter claim/índice único para concorrência. Consultas com `IgnoreQueryFilters` devem sempre filtrar `TenantId`.

Fontes: implementação em `BroadcastEndpoints.cs`, `BroadcastDispatchWorker.cs`, `ConversationEndpoints.cs` e `OutboxProcessingWorker.cs`; [Cloud API oficial](https://www.postman.com/meta/whatsapp-business-platform/documentation/wlk6lh4/whatsapp-cloud-api?entity=request-13382743-e1446141-746e-4fc8-9b6c-dd590ed4f924); [boas práticas WhatsApp Business](https://whatsappbusiness.com/wp-content/uploads/2026/04/Best-Practices-for-Marketing-Messages-on-WhatsApp-.pdf).
