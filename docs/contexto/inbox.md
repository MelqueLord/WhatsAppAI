# Contexto: Inbox

A Inbox exibe conversas e mensagens do tenant em tempo real via SignalR. A lista padrão mostra conversas depois de uma mensagem recebida do cliente; cada novo disparo volta a ocultar a conversa até uma resposta posterior, enquanto o contato continua localizável pela busca de nome ou telefone. Mensagens recebidas ainda não vistas aparecem em um contador verde por conversa, compartilhado pela equipe e zerado quando a conversa é aberta. Ao encerrar uma conversa, o painel volta para a lista de conversas ativas; a consulta de encerradas permanece disponível pela seleção manual do filtro. A Filas Inbox também mostra, em seção separada, contatos vinculados à fila durante a importação; contatos sem esse vínculo aparecem somente quando possuem conversa atribuída. Histórico é paginado, mídia é entregue apenas por endpoint autenticado da plataforma e eventos em outro tenant nunca são publicados para a conexão atual.

Fonte: [design da Inbox](../design/inbox-tempo-real.md) e [specs de Inbox](../specs/inbox/).

O operador pode responder a uma mensagem específica. A citação aparece no histórico e é transmitida ao canal quando a mensagem original tem identificador do provedor na mesma conversa, respeitando tenant, permissão e janela de atendimento.

Mensagens textuais podem ser encaminhadas a outra conversa acessível no mesmo tenant. O servidor lê o conteúdo original, aplica as regras de envio do destino e guarda a referência interna para a Inbox mostrar o marcador de encaminhamento.

Ao editar uma mensagem textual própria enviada há até 15 minutos pelo QR, a Inbox solicita edição no canal e atualiza o conteúdo local somente após confirmação. Nas demais situações, a Inbox envia uma correção nova com citação e marcador no histórico; o conteúdo original permanece inalterado. O rascunho do compositor é preservado se qualquer operação falhar.

O texto exibido pode ser copiado. A busca percorre o histórico da conversa no servidor em páginas, mantém a autorização de tenant, linha e fila e abre o resultado no trecho correspondente.

Reações de emoji são enviadas como operações próprias pela Outbox e aparecem junto da mensagem alvo. Reações recebidas são registradas sem renovar a janela de atendimento nem acionar a IA.

O operador só pode apagar mensagens enviadas pela empresa (`Outbound`); mensagens do cliente (`Inbound`) não oferecem essa ação e o backend também rejeita a tentativa. Apagar da Inbox oculta o conteúdo no histórico da equipe, mantendo o registro interno e impedindo busca, citação, encaminhamento ou correção da mensagem ocultada. Para mensagem própria enviada por QR, o operador também pode solicitar a revogação no WhatsApp; a Inbox só confirma após retorno da ponte. A API Oficial não oferece essa ação.

Quando o cliente revoga uma mensagem já recebida e o canal informa o identificador original, a Inbox mantém o texto ou a mídia já armazenados e acrescenta o aviso “Apagada pelo cliente”. Esse aviso é diferente da exclusão local feita pelo operador. Eventos sem referência segura ao original não são vinculados por aproximação.

