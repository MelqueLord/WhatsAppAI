# Research: LGPD Production Readiness

## Base legal e consentimento

**Decision:** Registrar uma base legal por finalidade e exigir evidência somente quando `Consent` for selecionado.

**Rationale:** A LGPD prevê múltiplas hipóteses legais; consentimento universal seria incorreto e criaria revogações sem semântica para tratamentos baseados em contrato, obrigação legal ou legítimo interesse.

**Alternatives considered:** Checkbox universal; política apenas documental. Ambas foram rejeitadas por não representarem a finalidade real de cada tratamento.

## Direitos e eliminação

**Decision:** Criar solicitação auditável e executar exportação ou anonimização por contato dentro do tenant. A negativa/adiamento exige justificativa e revisão.

**Rationale:** O titular possui direitos de confirmação, acesso, correção, portabilidade, anonimização, bloqueio e eliminação, sujeitos às hipóteses de conservação aplicáveis. A anonimização preserva integridade relacional sem manter conteúdo pessoal.

**Alternatives considered:** Exclusão física em cascata, rejeitada por quebrar relacionamentos e evidências; remoção manual por SQL, rejeitada por não ser repetível nem auditável.

## RIPD e encarregado

**Decision:** Versionar um RIPD inicial e uma matriz de papéis; publicar identidade/canal/encarregado por configuração ambiental. Ausência gera diagnóstico administrativo, não indisponibilidade.

**Rationale:** RIPD e dados do encarregado dependem da organização real. Código não pode fabricar esses elementos, mas pode fornecer estrutura, transparência e evidência operacional.

**Alternatives considered:** Hardcode de pessoa/empresa; bloqueio total do runtime. Ambos foram rejeitados.

## Fontes oficiais

- [Lei nº 13.709/2018 — LGPD](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm)
- [ANPD — Direitos dos titulares](https://www.gov.br/anpd/pt-br/assuntos/titular-de-dados-1/direito-dos-titulares)
- [ANPD — Relatório de Impacto à Proteção de Dados Pessoais](https://www.gov.br/anpd/pt-br/canais_atendimento/agente-de-tratamento/relatorio-de-impacto-a-protecao-de-dados-pessoais-ripd)
- [Resolução CD/ANPD nº 18/2024 — encarregado](https://www.gov.br/anpd/pt-br/acesso-a-informacao/institucional/atos-normativos/regulamentacoes_anpd/encarregado-completo_ocultado.pdf)
