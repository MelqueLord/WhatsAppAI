# Spec: login

Usuários autenticam no mesmo site por sessão de cookie. Em produção, cookie é `HttpOnly`, `Secure` e `SameSite=Lax`; mutações autenticadas exigem `X-CSRF-TOKEN`. Erros não revelam dados de contas inexistentes.

Fonte: FR-001 em [especificação-base](../plataforma/especificacao-plataforma.md).
