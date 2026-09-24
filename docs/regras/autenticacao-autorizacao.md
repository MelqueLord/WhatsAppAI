# Regras: autenticação e autorização

- Autenticar com sessão baseada em cookie e proteger mutações por antiforgery.
- Autorizar cada ação por papel e tenant no backend.
- Convites têm uso único, expiração e hash persistido; reenvio invalida o anterior.
- Desativação invalida sessões; reativação não restaura sessões antigas.

Fonte: [especificação-base](../specs/plataforma/especificacao-plataforma.md).
