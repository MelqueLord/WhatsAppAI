# Decisões para prontidão de produção

## Segurança de sessão

**Decisão:** configurar cookies por ambiente, exigir `Secure` em produção e validar antiforgery no login e em toda mutação autenticada.  
**Motivo:** atende **FR-001** e evita sessão/CSRF em transporte inseguro.  
**Alternativas rejeitadas:** manter `Secure=None`; confiar somente em `SameSite`.

## Evolução do banco

**Decisão:** usar migrations EF Core por bundle/job dedicado antes de iniciar a nova API; `EnsureCreated` fica restrito a desenvolvimento/testes descartáveis.  
**Motivo:** permite histórico, rollback e execução em imagem runtime sem SDK.  
**Alternativas rejeitadas:** alterar SQLite manualmente; executar `dotnet ef` dentro da imagem final.

## Configuração de deploy

**Decisão:** manter uma única nomenclatura de variáveis e usar template Nginx processado no início do container.  
**Motivo:** elimina divergência entre exemplo, Compose e processo real.  
**Alternativas rejeitadas:** duplicar aliases de segredos; editar arquivos manualmente no servidor.

## Gate de publicação

**Decisão:** somente promover uma tag imutável que passe CI com MySQL 8.4, zero lint/testes falhos, zero testes críticos ignorados, scanner de segredos e smoke test em staging.  
**Motivo:** materializa os gates da constituição.  
**Alternativas rejeitadas:** publicar a árvore local ou aceitar exceções sem prazo/responsável.

Nenhuma decisão exige novo ADR; elas corrigem a implementação para as decisões e requisitos já aprovados.
