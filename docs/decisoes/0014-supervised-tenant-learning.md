# ADR-0014: Aprendizado supervisionado local por tenant

**Status:** Aceito — 2026-09-05

## Contexto

O feedback do operador já identifica respostas úteis e correções, mas sem um
artefato reutilizável o agente não melhora de forma consistente para a empresa.
Ao mesmo tempo, registrar um prompt inteiro, fazer fine-tuning automático ou
compartilhar exemplos entre empresas criaria risco de privacidade e de
contaminação factual.

## Decisão

Uma avaliação do operador será tratada como supervisão explícita do tenant.
Feedback `Helpful` cria um exemplo de estilo e fluxo usando a resposta enviada;
`NeedsCorrection` cria o exemplo somente quando houver resposta corrigida. A
pergunta e a resposta passam por sanitização, o exemplo fica ativo e mantém
`source_interaction_id` para rastreabilidade. Observação sem resposta corrigida
continua apenas como feedback operacional.

O contexto prioriza exemplos supervisionados relevantes, mas continua tratando
todo exemplo como orientação de linguagem e fluxo. Fatos comerciais continuam
exigindo conhecimento ativo da empresa. O TenantOwner pode editar, desativar e
reativar o exemplo, e a plataforma não realiza fine-tuning nem treinamento
global.

## Consequências

- O agente melhora para cada empresa após uma aprovação humana verificável.
- Uma avaliação não dispara nova mensagem para o cliente.
- O limite de uma avaliação e o índice único da origem evitam duplicação.
- O sistema permanece no monólito e no PostgreSQL, sem introduzir um pipeline
  externo de treinamento.
