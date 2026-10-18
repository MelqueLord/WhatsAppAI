# Spec: responder manualmente

Operator assume a conversa antes de responder. O comando usa concorrência otimista e atualiza para `Human`; mensagem humana é enfileirada com estado rastreável e respeita a janela de 24 horas.

Ao selecionar uma mensagem específica da mesma conversa, a resposta textual inclui a referência do provedor, que aparece na Inbox e no WhatsApp. A permissão do operador, a janela de atendimento e a identidade do tenant são verificadas antes da Outbox.

O texto do compositor continua editável até a confirmação do envio e permanece disponível após falha. Editar texto próprio enviado há até 15 minutos na linha QR altera a mensagem no canal e, após confirmação, na Inbox. Na API Oficial ou após esse prazo, editar cria uma nova correção visível, citando a original; a mensagem antiga permanece no histórico.

Enter insere uma quebra de linha para continuar escrevendo. A mensagem só é enviada pelo botão de envio.

Fonte: US-003 e FR-010 a FR-012 em [especificação-base](../plataforma/especificacao-plataforma.md).
