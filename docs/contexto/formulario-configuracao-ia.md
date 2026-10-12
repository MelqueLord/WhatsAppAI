# Contexto: formulário de configuração da IA

O formulário coleta informações operacionais da empresa para que a equipe ATENZ prepare a configuração inicial da IA. Ele é aberto sem login por um link aleatório vinculado ao tenant, válido por 30 dias e utilizável uma vez. O segredo fica no fragmento `#` do link, que não é enviado em requisições ou logs HTTP; a SPA o transmite à API em cabeçalho dedicado.

As etapas cobrem perfil da empresa, objetivos da IA, regras de encaminhamento humano, expediente, setores, situação do WhatsApp e fontes de informação. As respostas são apenas material administrativo: não alteram automaticamente diretrizes, filas, BOT, conhecimento ou credenciais.

Somente o PlatformAdmin gera links, lista envios, abre, corrige e exclui o conteúdo. TenantOwner e Operator não recebem endpoints nem rotas para essas respostas. O token é persistido apenas como hash; auditoria registra metadados de geração, envio, edição e exclusão sem conteúdo ou token.

Fonte: [US-018, FR-089 e BR-056](../specs/plataforma/especificacao-plataforma.md).
