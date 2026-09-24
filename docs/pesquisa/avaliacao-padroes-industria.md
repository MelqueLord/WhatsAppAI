# Avaliação de padrões de indústria — segurança e prontidão

**Data:** 2026-09-13
**Status:** achados registrados para correção
**Escopo:** revisão estática da implementação, configuração de entrega e documentação; verificação local do frontend e auditoria de dependências de produção.

## Método e fontes

A avaliação confrontou o código e a configuração com a [especificação da plataforma](../specs/plataforma/especificacao-plataforma.md), a [constituição](../../.specify/memory/constitution.md), as [regras de segurança](../regras/seguranca.md) e os documentos operacionais. Foram inspecionados os fluxos de autenticação, isolamento de tenant, webhook/ponte QR, persistência, logs, health checks, backup, CI e frontend.

Também foram executados, no estado avaliado, `npm audit --omit=dev --audit-level=high` nas aplicações web e da ponte QR (sem vulnerabilidades reportadas), lint, testes e build do frontend. O SDK .NET 10.0.302 exigido por `global.json` não estava instalado na máquina de avaliação, portanto a compilação e a auditoria de pacotes .NET não foram concluídas localmente.

## Achados que bloqueiam produção

| ID | Evidência | Divergência | Correção rastreada |
|---|---|---|---|
| A-001 | `apps/web/src/lib/api.ts`, `auth.tsx` e `signalr.ts` persistem e registram JWT; `AuthEndpoints` também o devolve ao navegador. | O fluxo efetivo usa bearer persistente por até 30 dias, em vez da sessão por cookie prevista em FR-001. A invalidação de membership e security stamp não é verificada no bearer a cada uso, contrariando US-009 e FR-028. | CR-001, CR-002; T255–T257 |
| A-002 | `ObservabilityExtensions` cria apenas o registrador de health checks; nenhum check é marcado como `ready`. | `/health/ready` não verifica PostgreSQL nem a persistência essencial. | CR-005; T258 |

## Achados de prioridade alta

| ID | Evidência | Risco | Correção rastreada |
|---|---|---|---|
| A-003 | A ponte QR aceita um segredo global para rotas de sessão; comandos de configuração não aplicam a mesma verificação de lease; `inbox.json` mantém conteúdo de conversas no volume. | Comprometimento de um único segredo amplia o acesso à ponte e cria retenção adicional de dados pessoais. | CR-006, CR-007; ADR-0015, T262–T264 |
| A-004 | `SanitizingEnricher` existe, mas não é aplicado como proteção efetiva de propriedades e exceções em todos os eventos. | FR-016 e NFR-008 não têm garantia executável contra segredos ou dados pessoais em logs. | CR-004; T259 |
| A-005 | Há consultas com `IgnoreQueryFilters()` e repositórios tenant-scoped sem `TenantId` obrigatório no contrato; o interceptor abre escopo próprio ao obter o tenant. | Um chamador futuro pode retirar o filtro global sem uma barreira suficiente; o preenchimento automático de tenant não é confiável. | CR-003; T260–T261 |
| A-006 | `backup.sh` gera arquivo local sem cifragem, cópia externa nem prova de restauração. | Não há evidência de cumprimento de RPO/RTO definidos em NFR-005. | CR-008; T265 |

## Melhorias necessárias antes do piloto

| ID | Evidência | Correção rastreada |
|---|---|---|
| A-007 | Limites de taxa estão codificados, embora existam variáveis de ambiente; Nginx não define CSP. | CR-009; T266 |
| A-008 | Actions, imagens de contêiner e alguns pacotes usam versões flutuantes; faltam verificação de dependências .NET, SBOM e varredura de imagem. | CR-010; T267 |
| A-009 | O teste do frontend reporta estrutura inválida de tabela na tela de broadcast e o build gera bundle inicial acima do limite configurado. | CR-011; T268 |
| A-010 | `docs/tasks/producao.md` registra hardening concluído sem refletir os achados acima. | CR-012; T269 |

## Aspectos já sólidos

O projeto separa Domain, Application, Infrastructure e WebApi com testes de arquitetura; usa Outbox durável, idempotência de webhook, filtros globais de tenant, autorização por grupo SignalR e circuit breaker para IA. Esses controles são uma base relevante, mas não substituem as correções registradas neste incremento.

## Resultado

O estado atual é **NO-GO para produção**. Este relatório é insumo da [especificação de correção](../specs/producao/seguranca-e-prontidao.md), do [design](../design/seguranca-e-prontidao.md) e das [tarefas](../tasks/seguranca-e-prontidao.md). Nenhum achado representa, por si só, evidência de acesso cruzado já explorado; A-005 é uma lacuna de defesa e de contrato que deve ser eliminada preventivamente.
