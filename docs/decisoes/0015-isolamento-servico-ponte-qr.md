# ADR-0015 — Isolamento do serviço da ponte QR

**Status:** Proposto
**Data:** 2026-09-13
**Relacionados:** [CR-006](../specs/producao/seguranca-e-prontidao.md), [design](../design/seguranca-e-prontidao.md), [ADR-0011](0011-postgresql-qr-session-leases.md)

## Contexto

A ponte QR mantém estado de sessão e expõe comandos de sessão/configuração. A revisão A-003 identificou autenticação por segredo global, rota pública ampla de webhook e operações que não aplicam o mesmo controle de lease. Um segredo comprometido não pode conferir capacidade administrativa sobre todas as sessões, e comandos internos não devem competir com callbacks externos na mesma superfície pública.

## Decisão pendente

Antes de T263, escolher e registrar uma topologia que separe callbacks públicos de comandos internos e forneça identidade de serviço de escopo mínimo. A decisão deve definir:

- quais rotas podem ser alcançadas a partir da Internet;
- como WebApi e ponte se autenticam, incluindo rotação e revogação;
- como cada comando confirma tenant, linha e lease;
- como a migração preserva sessões QR existentes e como ocorre rollback;
- quais logs, métricas e testes de contrato provam o limite de acesso.

## Opções em avaliação

1. **Rede privada Docker + bloqueio Nginx para comandos internos + segredo de serviço rotacionável.** Mantém a implantação simples, mas exige controle rigoroso do segredo e da rede.
2. **Rede privada Docker + identidade de serviço baseada em certificado/mTLS.** Reduz dependência de segredo compartilhado, mas aumenta a operação de certificados.
3. **Canal interno dedicado com autenticação de workload.** Pode ampliar a separação, mas só é adequado se houver necessidade demonstrada além do monólito modular atual.

## Consequências esperadas

A opção aprovada será implementada somente em T263 e poderá alterar Compose, Nginx, WebApi e a ponte. Ela não introduz microsserviços, broker ou infraestrutura externa sem nova necessidade e decisão explícita. Enquanto este ADR estiver proposto, CR-006 permanece bloqueador de produção.
