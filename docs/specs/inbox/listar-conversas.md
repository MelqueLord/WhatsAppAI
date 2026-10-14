# Spec: listar conversas

Operator lista somente conversas do tenant e de filas permitidas. A paginação usa cursor e separa conversas abertas e encerradas sem expor contatos ou conversas de outra empresa.

Sem busca, a lista mostra somente conversas que já possuem ao menos uma mensagem recebida do cliente e, quando houve disparo, uma resposta posterior ao disparo mais recente. O disparo oculta a conversa mesmo quando ela já possuía histórico. Conversas sem resposta permanecem persistidas e podem ser encontradas por busca explícita de nome ou telefone, preservando os mesmos filtros de acesso, linha, fila e estado.

Cada item informa a quantidade de mensagens recebidas depois da última leitura compartilhada da conversa. A interface apresenta essa quantidade em verde e a zera ao abrir a conversa; mensagens enviadas pela empresa não entram no contador.

Fonte: US-002, US-009, US-014, FR-090, FR-091, BR-057 e BR-058 em [especificação-base](../plataforma/especificacao-plataforma.md).
