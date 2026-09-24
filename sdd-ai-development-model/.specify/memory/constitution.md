# Constituição do projeto

## Princípios

### I. Valor verificável

Toda mudança deve estar ligada a um problema de usuário, requisito operacional ou risco concreto. A entrega deve definir como seu resultado será observado.

### II. Especificação antes da implementação

Capacidades novas ou mudanças de comportamento começam por especificação, esclarecimentos necessários, plano e tarefas. Se a evidência mudar, os artefatos são corrigidos antes do código.

### III. Simplicidade com intenção

A solução mais simples que atende aos requisitos prevalece. Dependências, serviços externos e abstrações exigem benefício demonstrável e dono operacional definido.

### IV. Segurança e privacidade desde o desenho

Dados, permissões, segredos, retenção e auditoria são definidos antes da implementação. O sistema aplica o menor privilégio e nunca expõe informação sensível em logs ou respostas de erro.

### V. Qualidade comprovada

As regras de maior risco são cobertas por testes automatizados adequados. Cada incremento é formatado, compilado, testado e revisado antes de ser considerado concluído.

### VI. Operação observável

Falhas devem ser compreensíveis para usuários e investigáveis pela equipe. Métricas, logs seguros, correlação e procedimentos de recuperação são definidos proporcionalmente ao impacto.

### VII. Decisões rastreáveis

Requisitos, tarefas, código, testes e decisões técnicas devem permitir entender por que uma mudança existe. Decisões estruturais são registradas como ADRs.

## Portões de qualidade

Antes de integrar uma mudança, confirme:

1. A especificação, o plano e as tarefas ainda descrevem a implementação.
2. A autorização, validação, dados sensíveis e limites operacionais foram considerados.
3. Formatação, compilação e testes relevantes passaram.
4. O diff não introduz dependências ou decisões estruturais sem documentação.
5. Riscos, limitações e acompanhamento necessário foram registrados.

## Governança

Esta constituição prevalece sobre hábitos informais de implementação. Alterações exigem justificativa, revisão da versão e atualização dos documentos afetados.
