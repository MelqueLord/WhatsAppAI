# Constituição do WhatsApp AI Manager

**Versão:** 1.1.0
**Ratificada:** 2026-08-03

## I. Simplicidade orientada ao atendimento

O produto existe para gerenciar conversas do WhatsApp e automatizar atendimento com IA. Funcionalidades que não reduzem esforço de atendimento, melhoram controle humano ou tornam a operação mais segura ficam fora do núcleo. O MVP não é CRM, ferramenta de campanhas ou construtor genérico de bots.

## II. Integrações oficiais e responsabilidade clara

Somente APIs oficiais da Meta e da OpenAI podem participar do caminho crítico. Cada tenant mantém titularidade, aceite de termos e faturamento direto de suas contas. A plataforma configura e opera as conexões, mas não revende consumo dos provedores.

## III. Automação sob controle humano

A IA sugere ou produz respostas dentro de políticas explícitas. O backend valida e envia. Um operador pode assumir qualquer conversa, interromper a automação e devolvê-la à IA. Baixa confiança, solicitação humana, risco ou falta de conhecimento devem provocar transferência segura.

## IV. Isolamento e privacidade por padrão

Vazamento entre tenants é falha crítica. Dados, credenciais, logs, caches e jobs devem preservar contexto de tenant. Coletar apenas dados necessários, limitar retenção, criptografar segredos e permitir auditoria são requisitos, não melhorias opcionais.

## V. Entrega incremental e observável

Cada incremento precisa ser executável, testável e ligado a requisitos. Webhooks e envios são idempotentes. Operações externas têm correlação, métricas e caminhos de recuperação. O sistema reconhece falhas parciais em vez de presumir consistência distribuída.

## VI. Arquitetura proporcional

O início é um monólito modular implantável como uma unidade, com PostgreSQL como infraestrutura central: Supabase nos ambientes gerenciados e PostgreSQL em Docker na produção própria. Novos componentes operacionais só entram mediante métrica, gargalo observado e ADR aprovado. Interfaces protegem o domínio de SDKs externos sem antecipar abstrações genéricas.

## VII. Especificação executável

Requisitos usam identificadores estáveis e critérios verificáveis. Contratos, modelo de dados, testes e tarefas referenciam esses IDs. Alterações relevantes começam na especificação e terminam em validação automatizada e documentação operacional.

## Gates de qualidade

Toda entrega deve passar por:

1. verificação de aderência ao escopo e aos requisitos;
2. compilação e análise estática sem novos warnings;
3. testes unitários e de integração afetados;
4. teste de isolamento de tenant quando houver acesso a dados;
5. revisão de logs e tratamento de segredos;
6. revisão de migration, contrato e runbook quando aplicável.

## Governança

Esta constituição prevalece sobre conveniências locais. Alterações exigem justificativa, impacto nos documentos dependentes e incremento de versão: MAJOR para remoção/redefinição de princípio; MINOR para princípio ou seção material nova; PATCH para esclarecimentos. Exceções temporárias precisam de responsável, prazo e ADR.
