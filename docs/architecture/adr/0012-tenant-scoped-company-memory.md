# ADR-0012: Memória institucional isolada por tenant

## Status

Aceito

## Contexto

A IA precisa aproveitar respostas factuais já estabelecidas para atender outros clientes da mesma empresa, inclusive quando a pergunta usa uma paráfrase. O modelo externo é stateless e não deve reutilizar dados de clientes de empresas diferentes.

## Decisão

Usar os itens de conhecimento do próprio tenant como memória institucional. O worker pode criar uma entrada sanitizada somente quando a resposta for Reply, segura, tiver confiança mínima de 0,8 e houver pelo menos uma fonte ativa de conhecimento no contexto. A seleção existente por tenant reutiliza essa memória em atendimentos posteriores.

Perguntas e respostas são limitadas e sanitizadas antes da persistência. Handoffs, respostas sem fonte autorizada, conteúdo inseguro e dados pessoais não são promovidos para memória. A memória não é compartilhada entre tenants.

## Consequências

- Clientes da mesma empresa recebem respostas mais consistentes ao longo do tempo.
- A implementação não exige um serviço externo de memória nem altera o caminho crítico do WhatsApp.
- Memórias aparecem como itens de conhecimento e podem ser revisadas, editadas ou desativadas pelo responsável.
- Uma resposta aprovada pelo limiar pode ser persistida automaticamente; falhas de conteúdo devem ser corrigidas desativando o item gerado.
