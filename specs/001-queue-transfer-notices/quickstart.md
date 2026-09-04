# Validation Guide: Queue Transfer Notices

1. Open **Filas de Atendimento**, edit **SUPORTE SISTEMA - CS**, and set a notice such as `Vou encaminhar você ao suporte. Aguarde um instante.`
2. Activate AI for a test conversation and send a configured support keyword.
3. Verify the conversation moves to the Support queue and exactly that notice appears in the customer history.
4. Send another message covered by the company's guidelines or knowledge while the conversation remains in the queue and verify that the AI responds using that context.
5. Send a message outside the configured service and verify that the AI sends the human-transfer message and changes the conversation to human mode.
6. Send a message with no applicable AI action while the conversation remains automatic in the queue and verify the customer receives `Aguarde, você está na fila Support para atendimento. Caso queira mudar seu atendimento, envie o tipo de atendimento que deseja.`.
7. Send a keyword configured for a different authorized queue and verify the conversation moves there and receives that queue's transfer notice.
8. Start a new automatic conversation and verify the configured business-specific welcome message is used as first-contact guidance for the AI. If the configured text is generic or empty, verify that the reply starts with a welcome and reflects the company profile instead of `Olá! Como posso ajudar?`.
9. In an existing queued conversation, send `oi` and verify the AI considers the current message plus the three previous messages instead of restarting the welcome flow.
10. Clear the queue notice, repeat with a new automatic conversation, and verify the existing generic transfer message appears instead.
11. Repeat the first scenario from a different tenant and verify only its own configured notice is used.
