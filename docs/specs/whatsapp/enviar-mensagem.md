# Spec: enviar mensagem

Mensagem humana ou automática é criada junto à Outbox e enviada de forma idempotente. Estados `Queued`, `Sent`, `Delivered`, `Read` e `Failed` informam a Inbox. Texto livre fora da janela é bloqueado.

Na Inbox, a opção de template aparece somente quando a conversa está vinculada a uma linha ativa da API Oficial. Conversas QR, legadas sem linha oficial e linhas oficiais desativadas não consultam templates; a interface informa que a conexão da linha precisa ser verificada. O backend valida a linha novamente ao listar e enviar templates.

A lista mostra todas as categorias e situações retornadas pela WABA, agrupando Utilidade, Marketing, Autenticação e outras categorias presentes. Apenas modelos aprovados de Utilidade ou Marketing com parâmetros textuais no corpo e sem componentes adicionais incompatíveis podem ser selecionados para envio individual. Os demais continuam visíveis com o motivo do bloqueio. O disparo em massa permanece limitado a templates de Utilidade aprovados.

## Imagem enviada manualmente pela Inbox

TenantOwner ou Operator autorizado para a linha e fila da conversa pode anexar uma imagem JPEG ou PNG de até 5 MB, com legenda opcional, a uma conversa aberta. O primeiro incremento não inclui documentos, áudio, vídeo, sticker, mídia de visualização única, envio automático por IA ou broadcast.

O envio conserva o mesmo contrato operacional de uma mensagem humana: a intenção, a mensagem e a Outbox são persistidas de modo atômico e idempotente antes de qualquer chamada externa; a Inbox mostra `Queued`, `Sent`, `Delivered`, `Read` ou `Failed`. Em linha oficial, texto livre e imagem seguem bloqueados fora da janela de 24 horas. Em linha QR, o envio depende da sessão conectada e dos controles internos aprovados para a ponte.

O conteúdo binário tem a finalidade exclusiva de transmitir uma informação ao cliente. Ele não é gravado como data URL em `Message`, nem exposto por URL pública: fica protegido e disponível somente até a Outbox atingir um resultado terminal, quando é removido. Logs, SignalR e respostas da API só transportam identificadores e metadados permitidos; nunca bytes, URL privada, token ou legenda completa.

Uma imagem recebida de qualquer canal permanece acessível exclusivamente por endpoint autenticado da plataforma no tenant corrente. No QR, a ponte deve preservar tipo, legenda e referência recuperável da imagem, em vez de reduzi-la a texto genérico.

Fonte: US-002, US-003, FR-010, FR-012, FR-023 e NFR-006/NFR-009 em [especificação-base](../plataforma/especificacao-plataforma.md); [plano de envio de imagens](../../design/plano-envio-imagens-inbox.md).
