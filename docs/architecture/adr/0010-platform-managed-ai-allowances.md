# ADR-0010: franquia de IA gerenciada por tenant

**Status:** Aceito — 2026-08-28
**Supersede parcialmente:** ADR-0003

## Contexto

Os planos comerciais STAR, FLOW e SCALA incluem capacidade mensal de IA. O modelo anterior exigia que cada tenant fosse sempre cobrado diretamente pelo provedor, o que não atende à oferta publicada.

## Decisão

A plataforma aceita dois modelos: credencial própria do tenant ou provedor operado pela plataforma. Em ambos, cada tenant possui uma franquia mensal personalizada de respostas de IA. Somente resposta válida criada na outbox conta; mensagem recebida, simulação, falha, fallback, handoff e decisão descartada não contam.

Ao atingir a franquia, o provedor não é chamado para novas respostas e o atendimento segue pelo fallback/handoff seguro já configurado. A contagem usa `UsageLedger`, com revalidação imediatamente antes da criação da resposta. Segredos continuam em `ISecretStore` e nunca são expostos ao tenant.

## Consequências

- O custo do provedor pode fazer parte da assinatura comercial.
- O PlatformAdmin define a franquia por empresa e acompanha consumo mensal.
- Planos fornecem valores padrão, mas o limite persistido no tenant é a fonte usada pelo runtime.
- ADR-0003 permanece válido para Meta e para tenants que utilizem credencial própria de IA.
