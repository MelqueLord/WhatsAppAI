# RIPD — WhatsApp AI Manager

**Versão:** 1.0
**Data:** 2026-08-27
**Responsável pela revisão:** configurar antes da publicação externa
**Próxima revisão:** antes de nova finalidade, fornecedor, categoria sensível ou decisão automatizada material

## Escopo e papéis

- O tenant é controlador dos dados de contatos e conversas.
- A plataforma atua como operadora nesses tratamentos.
- Meta e o provedor de IA são suboperadores conforme a função habilitada e os contratos próprios.
- A plataforma é controladora dos dados de conta, autenticação, segurança, auditoria e faturamento.

## Tratamentos avaliados

| Tratamento | Dados | Finalidade | Base a registrar | Retenção |
|---|---|---|---|---|
| Atendimento WhatsApp | telefone, nome, mensagens, mídia | responder e manter histórico de atendimento | definida pelo tenant por finalidade | definida pelo tenant |
| Automação por IA | trechos necessários da conversa, decisão e métricas | sugerir/enviar resposta conforme configuração | mesma finalidade do atendimento | conteúdo segue retenção do atendimento; métricas minimizadas |
| Conta e segurança | nome, e-mail, sessão e auditoria | autenticação, autorização, prevenção e investigação | contrato/obrigação/interesse legítimo conforme avaliação | conta e prazo de defesa aplicável |

## Riscos e controles

| Risco | Impacto | Controle | Risco residual |
|---|---|---|---|
| Acesso cruzado entre tenants | alto | `TenantId`, filtros globais, autorização e testes de isolamento | baixo após testes |
| Uso de base legal inadequada | alto | cadastro obrigatório de finalidade/base/prazo; consentimento apenas quando escolhido | médio, depende da validação do controlador |
| Retenção excessiva | alto | prazo por finalidade, worker existente e solicitações de anonimização | médio até automatizar todas as categorias |
| Vazamento para fornecedor | alto | credenciais por tenant, minimização, contratos e logs sem conteúdo | médio, depende dos contratos/configurações |
| Impossibilidade de atender titular | alto | fluxo de solicitação, exportação, negativa justificada e anonimização transacional | baixo |
| Reidentificação após anonimização | médio | remoção de telefone, nome, foto, conteúdo, mídia e identificadores externos | baixo; payloads criptografados seguem retenção curta |
| Decisão automatizada indevida | alto | limites do bot, handoff humano, avaliações e possibilidade de desativar IA | médio |

## Necessidade e proporcionalidade

O atendimento requer identificador do contato e conteúdo recebido. Exportação para IA deve limitar-se ao contexto necessário. Auditoria armazena IDs, ação, autor e datas, sem copiar mensagens, telefone ou evidências brutas. A eliminação prefere anonimização irreversível para preservar integridade e evidência mínima.

## Direitos e incidentes

O TenantOwner valida a identidade do solicitante, registra o pedido e usa o fluxo de acesso, portabilidade, correção, bloqueio, anonimização ou eliminação. Negativas exigem motivo e revisão. Incidentes seguem o runbook e devem avaliar comunicação ao controlador, titulares e ANPD conforme gravidade e norma aplicável.

## Aprovações a preencher no ambiente de produção

- Controlador (nome e registro):
- Canal de privacidade:
- Encarregado e contato, ou fundamento documentado de dispensa:
- Responsável do tenant pela base legal:
- Responsável pela aprovação deste RIPD:
- Data da aprovação:
