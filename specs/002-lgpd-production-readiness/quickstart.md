# Quickstart: validar controles LGPD

## Pré-requisitos

- Segredos e conexão Supabase fornecidos localmente/ambiente.
- TenantOwner e dois tenants de teste.

## Validação automatizada

1. Compile a solução em Release.
2. Execute os testes unitários de `Privacy`.
3. Execute os testes de integração de `Privacy` e persistência PostgreSQL.
4. Gere/aplique a migration Npgsql no Supabase e confirme ausência de mudanças pendentes.

## Cenário funcional

1. Cadastre uma finalidade não baseada em consentimento e confirme que não há consentimento artificial.
2. Cadastre finalidade baseada em consentimento; registre e revogue evidência.
3. Abra solicitação de portabilidade e obtenha exportação do contato.
4. Abra solicitação de eliminação e execute anonimização.
5. Confirme que nome, telefone e conteúdo não aparecem mais e que tenant diferente permanece intacto.
6. Consulte o aviso público e confirme que somente valores fornecidos pelo ambiente são exibidos.

## Evidências de release

- Resultado de build/testes e migration.
- Captura do aviso com dados institucionais reais (fora do repositório).
- RIPD e checklist revisados por responsável identificado.
