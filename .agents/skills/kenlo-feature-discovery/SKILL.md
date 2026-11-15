---
name: kenlo-feature-discovery
description: Explorar funcionalidades e decisões de arquitetura, com escopo, dependências e alternativas.
---

# Descoberta de funcionalidade

Use para conceber uma funcionalidade ou avaliar alternativas que tenham decisões ou dependências relevantes. Investigue primeiro os arquivos, contratos, consumidores e restrições relacionados ao pedido. Diferencie fatos observados, hipóteses e dados ainda não disponíveis; não invente requisitos, fontes de dados ou evidências.

- Para backend ou dados, comece pelo domínio, contratos, persistência, integrações e efeitos observáveis pertinentes. Não proponha nem exija concepção de tela quando não houver interface no escopo.
- Para interface, considere usuário, ações e dados necessários; avalie hierarquia, acessibilidade e estados relevantes se a tarefa pedir concepção visual ou de interação.
- Para full-stack, ordene trabalho e validação pelas dependências reais entre domínio, API, persistência e consumidores; analise partes independentes em paralelo quando a sessão permitir.

Compare alternativas apenas quando esclarecer uma decisão, com custos, riscos e critério de escolha. Identifique critérios verificáveis e perguntas que realmente bloqueiam uma decisão; prossiga com suposições explícitas quando puderem ser resolvidas depois. Não exija número fixo de perguntas, artefatos ou aprovação para seguir o trabalho autorizado.

Para um plano ou decisão arquitetural, use os modelos existentes no repositório, se houver. Se o pedido for apenas de exploração ou plano, apresente a proposta sem implementar; se incluir implementação, prossiga conforme o escopo autorizado.

## Origem

Adaptada de [invest-feature-discovery](https://github.com/JonathanBenicio/invest-dashboard/blob/080e1bae1b194fdc3b9ffd5af9cea9119dc7e77f/.agents/skills/invest-feature-discovery/SKILL.md), no commit `080e1bae1b194fdc3b9ffd5af9cea9119dc7e77f` do invest-dashboard.
