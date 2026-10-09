# Contexto: WhatsApp

Linhas podem usar WhatsApp Cloud API ou ponte WhatsApp Web por QR Code. Webhooks são autenticados e idempotentes. Cada evento é persistido antes de worker processá-lo, e os envios passam pela Outbox. A sessão QR é isolada por tenant e linha.

Fora da janela de atendimento de 24 horas, a Inbox de uma linha oficial consulta todas as páginas de templates da WABA e oferece somente templates `UTILITY` ou `MARKETING` aprovados com formato de parâmetros posicional ou nomeado e componentes compatíveis. A tela mostra o texto do corpo e os nomes/índices das variáveis detectadas. Antes de enfileirar o envio, o backend consulta novamente essa lista e valida nome, idioma e a quantidade de parâmetros do corpo; variáveis nomeadas são mapeadas ao `parameter_name` exigido pela Meta, e linhas QR Code não aceitam templates.

O histórico da conversa deve exibir o corpo aprovado do template com os parâmetros preenchidos, em vez do nome interno do template. O disparo em massa também guarda esse texto no resumo do envio. Rejeições permanentes da API Oficial devem encerrar a tentativa e mostrar um motivo sanitizado por destinatário; limites temporários, timeout e indisponibilidade continuam passíveis de repetição.

Na Inbox, mensagens enviadas mostram relógio enquanto aguardam a Outbox, um tique quando a Meta aceita o envio, dois tiques cinza quando o destinatário recebe e dois tiques azuis quando a Meta confirma a leitura. O worker publica a mudança de status no SignalR após persistir o webhook para atualizar a conversa aberta. Se o status chegar antes de a Outbox persistir o identificador externo da mensagem, o webhook é repetido para reconciliar a corrida. Falhas permanecem identificadas separadamente.

No disparo em massa, ao selecionar um template elegível, o formulário mostra o corpo da mensagem e atualiza a prévia com os parâmetros preenchidos. O disparo inclui os contatos selecionados para templates `UTILITY` e `MARKETING` sem consultar evidência de consentimento no aplicativo. A política do WhatsApp exige aceitação explícita antes de contatar pessoas e respeito às solicitações de cancelamento; obter e cumprir essas condições é responsabilidade do tenant ([política oficial](https://business.whatsapp.com/policy/preview?lang=pt_BR)).

Na tela de integração, cada linha da API Oficial mostra `Conectado` somente após uma verificação sanitizada das credenciais contra a Meta; sem credenciais, token disponível ou resposta válida, mostra `Desconectado`. Esse status é consultado por linha e não expõe o token.

Somente TenantOwner configura, consulta o estado ou desconecta linhas. Desconectar uma linha oficial desativa o canal e remove seu token do cofre, preservando o registro da linha para uma reconexão explícita com novas credenciais. Operators não têm acesso às credenciais e a Inbox aplica a linha atribuída, a fila atribuída ou ambas como escopo de acesso.

A Inbox pode encaminhar uma imagem JPEG ou PNG de até 5 MB por linha oficial, exclusivamente dentro da janela de atendimento de 24 horas. O navegador aplica a mesma restrição para orientar a pessoa usuária, mas o servidor valida tipo, tamanho e assinatura do arquivo; o binário é cifrado até a Outbox enviá-lo à Meta e não é exposto pela mensagem, pela resposta HTTP ou pelo SignalR. O envio de imagem por QR Code permanece bloqueado até a conclusão das decisões e controles de segurança próprios da ponte.

A ponte QR usa uma rede Docker interna para comandos de sessão, lease, credenciais e eventos. Esses comandos exigem identificação de serviço e token montado como segredo de arquivo, com token anterior aceito apenas até o vencimento explícito da rotação; o segredo de callbacks não é usado. A superfície pública não encaminha essas rotas e a ponte não publica porta no host. Cada mutação confirma a propriedade do lease da linha antes de alterar estado ou encaminhar eventos.

Fonte: [integração WhatsApp](../design/integracao-whatsapp.md) e [regras de WhatsApp](../regras/whatsapp.md).
