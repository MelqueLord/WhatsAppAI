# Contexto: WhatsApp

Linhas podem usar WhatsApp Cloud API ou ponte WhatsApp Web por QR Code. Webhooks são autenticados e idempotentes. Cada evento é persistido antes de worker processá-lo, e os envios passam pela Outbox. A sessão QR é isolada por tenant e linha.

Fora da janela de atendimento de 24 horas, a Inbox de uma linha oficial consulta todas as páginas de templates da WABA e oferece somente templates `UTILITY` aprovados. Antes de enfileirar o envio, o backend consulta novamente essa lista e valida nome, idioma e a quantidade de parâmetros do corpo; linhas QR Code não aceitam templates.

Na tela de integração, cada linha da API Oficial mostra `Conectado` somente após uma verificação sanitizada das credenciais contra a Meta; sem credenciais, token disponível ou resposta válida, mostra `Desconectado`. Esse status é consultado por linha e não expõe o token.

Somente TenantOwner configura, consulta o estado ou desconecta linhas. Desconectar uma linha oficial desativa o canal e remove seu token do cofre, preservando o registro da linha para uma reconexão explícita com novas credenciais. Operators não têm acesso às credenciais e a Inbox aplica a linha atribuída, a fila atribuída ou ambas como escopo de acesso.

A Inbox pode encaminhar uma imagem JPEG ou PNG de até 5 MB por linha oficial, exclusivamente dentro da janela de atendimento de 24 horas. O navegador aplica a mesma restrição para orientar a pessoa usuária, mas o servidor valida tipo, tamanho e assinatura do arquivo; o binário é cifrado até a Outbox enviá-lo à Meta e não é exposto pela mensagem, pela resposta HTTP ou pelo SignalR. O envio de imagem por QR Code permanece bloqueado até a conclusão das decisões e controles de segurança próprios da ponte.

Fonte: [integração WhatsApp](../design/integracao-whatsapp.md) e [regras de WhatsApp](../regras/whatsapp.md).
