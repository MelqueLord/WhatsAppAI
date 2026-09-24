# Spec: enviar mensagem

Mensagem humana ou automática é criada junto à Outbox e enviada de forma idempotente. Estados `Queued`, `Sent`, `Delivered`, `Read` e `Failed` informam a Inbox. Texto livre fora da janela é bloqueado.

Na Inbox, a opção de template aparece somente quando a conversa está vinculada a uma linha ativa da API Oficial. Conversas QR, legadas sem linha oficial e linhas oficiais desativadas não consultam templates; a interface informa que a conexão da linha precisa ser verificada. O backend valida a linha novamente ao listar e enviar templates.

A lista mostra todas as categorias e situações retornadas pela WABA, agrupando Utilidade, Marketing, Autenticação e outras categorias presentes. Apenas modelos aprovados de Utilidade ou Marketing com parâmetros textuais no corpo e sem componentes adicionais incompatíveis podem ser selecionados para envio individual. Os demais continuam visíveis com o motivo do bloqueio. O disparo em massa permanece limitado a templates de Utilidade aprovados.

Fonte: US-003, FR-010 e FR-012 em [especificação-base](../plataforma/especificacao-plataforma.md).
