# Contexto: Inbox

A Inbox exibe conversas e mensagens do tenant em tempo real via SignalR. A lista padrão mostra conversas depois de uma mensagem recebida do cliente; cada novo disparo volta a ocultar a conversa até uma resposta posterior, enquanto o contato continua localizável pela busca de nome ou telefone. Mensagens recebidas ainda não vistas aparecem em um contador verde por conversa, compartilhado pela equipe e zerado quando a conversa é aberta. Ao encerrar uma conversa, o painel volta para a lista de conversas ativas; a consulta de encerradas permanece disponível pela seleção manual do filtro. A Filas Inbox também mostra, em seção separada, contatos vinculados à fila durante a importação; contatos sem esse vínculo aparecem somente quando possuem conversa atribuída. Histórico é paginado, mídia é entregue apenas por endpoint autenticado da plataforma e eventos em outro tenant nunca são publicados para a conexão atual.

Fonte: [design da Inbox](../design/inbox-tempo-real.md) e [specs de Inbox](../specs/inbox/).

