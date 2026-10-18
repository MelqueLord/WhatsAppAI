# Spec: visualizar conversa

A visualização retorna histórico paginado, estados de envio e mídia por endpoint autenticado. O acesso valida sessão, tenant, papel e restrição de fila antes de devolver qualquer dado.

A busca textual no histórico aplica as mesmas verificações, filtra conteúdo ou legenda e permite navegar ao resultado, inclusive fora do lote mais recente.

Reações são agregadas ao balão da mensagem alvo; o evento de reação não aparece como uma nova fala no histórico.

Somente mensagens enviadas pela empresa (`Outbound`) podem ser apagadas da Inbox; mensagens recebidas do cliente (`Inbound`) não podem ser apagadas pelo operador. Uma mensagem Outbound apagada da Inbox permanece como aviso sem conteúdo ou mídia no histórico e não aparece na busca textual. O conteúdo persistido continua restrito ao uso interno de auditoria.

Se o cliente apagar uma mensagem no WhatsApp após sua recepção, a Inbox mantém o conteúdo recebido e mostra “Apagada pelo cliente” na própria mensagem quando o canal fornecer o identificador original. O evento não cria nova fala nem altera a leitura da conversa.

Fonte: US-002 e FR-023 em [especificação-base](../plataforma/especificacao-plataforma.md).
