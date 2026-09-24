# Contexto: identidade e acesso

PlatformAdmin administra a plataforma; TenantOwner configura a própria empresa e seus Operators; Operator atende somente o escopo permitido. A sessão usa cookie e toda mutação autenticada exige antiforgery. Convites expiram, possuem uso único e são persistidos apenas como hash.

Fonte: [especificação-base](../specs/plataforma/especificacao-plataforma.md) e [regras de autenticação](../regras/autenticacao-autorizacao.md).
