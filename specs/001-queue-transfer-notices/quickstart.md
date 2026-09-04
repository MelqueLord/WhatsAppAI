# Validation Guide: Queue Transfer Notices

1. Open **Filas de Atendimento**, edit **SUPORTE SISTEMA - CS**, and set a notice such as `Vou encaminhar você ao suporte. Aguarde um instante.`
2. Activate AI for a test conversation and send a configured support keyword.
3. Verify the conversation moves to the Support queue and exactly that notice appears in the customer history.
4. Send another message while the conversation remains in the queue and verify the customer receives `Aguarde, você está na fila Support para atendimento. Caso queira mudar seu atendimento, envie o tipo de atendimento que deseja.` without an AI request being made.
5. Send a keyword configured for a different authorized queue and verify the conversation moves there and receives that queue's transfer notice.
6. Clear the queue notice, repeat with a new automatic conversation, and verify the existing generic transfer message appears instead.
7. Repeat the first scenario from a different tenant and verify only its own configured notice is used.
