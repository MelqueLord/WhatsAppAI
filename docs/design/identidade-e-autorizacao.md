# Design: identidade e autorização

Cookie de sessão identifica usuário e membership; antiforgery protege mutações. Policies no backend validam PlatformAdmin, TenantOwner ou Operator e o escopo de tenant e fila. Security stamp e estado de membership invalidam sessão quando necessário.

Fonte: [especificação-base](../specs/plataforma/especificacao-plataforma.md) e [contexto de identidade](../contexto/identidade-e-acesso.md).
