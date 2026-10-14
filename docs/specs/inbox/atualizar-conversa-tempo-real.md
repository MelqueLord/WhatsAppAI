# Spec: atualizar conversa em tempo real

SignalR publica mudanças somente ao grupo do tenant depois de autenticação e resolução no servidor. A interface atualiza Inbox, filas e contadores de mensagens não lidas sem permitir que evento de tenant diferente seja recebido. Se a conversa que recebeu a mensagem estiver aberta, a interface registra a leitura e mantém seu indicador zerado.

Fonte: FR-009 e FR-091 em [especificação-base](../plataforma/especificacao-plataforma.md).
