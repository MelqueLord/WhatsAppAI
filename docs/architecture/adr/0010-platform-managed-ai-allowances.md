# ADR-0010: IA contratada e controlada pela plataforma por tenant

**Status:** Aceito — 2026-08-31
**Supersede parcialmente:** ADR-0003

## Contexto

Os planos comerciais STAR, FLOW e SCALA incluem capacidade mensal de IA. O modelo anterior exigia que cada tenant fosse cobrado diretamente pelo provedor e permitia que ele administrasse a própria credencial, o que não atende à operação da plataforma.

## Decisão

A plataforma contrata e administra os provedores de IA. Cada tenant recebe uma configuração de provedor/modelo e uma credencial administrada pela plataforma; o TenantOwner não cadastra, recupera ou altera API keys. A plataforma pode usar uma conta, projeto ou credencial isolada por tenant quando o provedor suportar essa separação.

As diretrizes, o perfil do negócio, o conhecimento, o limiar de confiança, as filas, as tags e as regras de handoff continuam pertencendo ao tenant, pois definem o atendimento daquela empresa. Essas configurações nunca são compartilhadas entre tenants.

Cada tenant possui uma franquia mensal personalizada de respostas e um orçamento técnico controlado pela plataforma. Somente resposta válida criada na outbox conta; mensagem recebida, simulação, falha, fallback, handoff e decisão descartada não contam.

Ao atingir a franquia, o provedor não é chamado para novas respostas e o atendimento segue pelo fallback/handoff seguro já configurado. A contagem usa `UsageLedger`, com revalidação imediatamente antes da criação da resposta. Segredos continuam em `ISecretStore` e nunca são expostos ao tenant.

## Consequências

- O custo do provedor faz parte do custo operacional da plataforma e pode fazer parte da assinatura comercial do tenant.
- O PlatformAdmin define a franquia por empresa e acompanha consumo mensal.
- O PlatformAdmin define orçamento mensal, limites técnicos e política de bloqueio por tenant.
- O TenantOwner administra somente as regras do atendimento da própria empresa, sem acesso a credenciais ou custos técnicos.
- Planos fornecem valores padrão, mas o limite persistido no tenant é a fonte usada pelo runtime.
- ADR-0003 deixa de reger o faturamento e as credenciais de IA; suas regras para Meta permanecem aplicáveis quando não conflitarem com decisões posteriores.
