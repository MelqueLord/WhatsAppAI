# Contexto: Inbox

A Inbox exibe conversas e mensagens do tenant em tempo real via SignalR. Ao encerrar uma conversa, o painel volta para a lista de conversas ativas; a consulta de encerradas permanece disponível pela seleção manual do filtro. A Filas Inbox também mostra, em seção separada, contatos vinculados à fila durante a importação; contatos sem esse vínculo aparecem somente quando possuem conversa atribuída. Histórico é paginado, mídia é entregue apenas por endpoint autenticado da plataforma e eventos em outro tenant nunca são publicados para a conexão atual.

Fonte: [design da Inbox](../design/inbox-tempo-real.md) e [specs de Inbox](../specs/inbox/).

