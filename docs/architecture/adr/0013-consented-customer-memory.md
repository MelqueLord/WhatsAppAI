# ADR-0013: Memória individual do cliente com consentimento

**Status:** Aceito — 2026-09-05

## Contexto

A memória institucional da empresa melhora respostas factuais para todos os
clientes, mas não deve ser usada para guardar preferências ou fatos individuais.
O atendimento precisa personalizar a conversa sem transformar mensagens do
cliente em perfil automático, sem cruzar tenants e sem continuar usando dados
depois da revogação do consentimento.

## Decisão

Criar uma entidade de memória vinculada a `TenantId`, `ContactId` e à evidência
ativa da finalidade padrão de atendimento automatizado por IA. Cada entrada
contém uma chave curta, um valor curto, origem de confirmação operacional,
validade e estado ativo. Operator ou TenantOwner pode salvar ou desativar a
entrada depois que o contato tiver consentimento ativo; a IA não possui
permissão para escrever memória.

O repositório consulta apenas entradas ativas, não expiradas e cuja evidência
de consentimento ainda não foi revogada. O contexto recebe no máximo quatro
entradas sanitizadas. Conteúdo com credenciais, dados pessoais identificáveis,
prompt injection ou conteúdo inseguro é rejeitado. Anonimização do contato
redige e desativa suas memórias; a revogação apenas bloqueia o uso futuro,
preservando a evidência auditável.

## Consequências

- O cliente recebe personalização consistente entre conversas da mesma empresa.
- Um erro do modelo não cria um perfil persistente ou memória falsa.
- A revogação é aplicada no próximo contexto sem depender de limpeza de cache.
- O armazenamento continua no PostgreSQL e tenant-scoped, sem serviço externo.
