---
name: kenlo-debug
description: Investigar falhas, erros intermitentes e comportamentos inesperados no projeto.
---

# Diagnóstico no projeto Kenlo + WhatsApp

Entenda o sintoma, o comportamento esperado e o escopo informado. Confira o estado atual do código, configuração e evidências disponíveis antes de atribuir uma causa; não trate informação histórica ou hipótese como fato atual. Siga o caminho relevante entre interface, API, serviços, persistência e integrações somente onde o problema exigir.

Reproduza ou reduza o problema quando for viável e seguro. Se não conseguir reproduzir, avance com inspeção e evidências disponíveis, informe o limite e descreva o que confirmaria a hipótese. Compare hipóteses com observações concretas e busque o ponto em que o comportamento diverge; não imponha um número fixo de perguntas ou de tentativas.

Respeite o resultado pedido: em investigação ou revisão somente de leitura, apresente achados e próximos passos sem modificar código nem executar ações com efeitos externos. Se o usuário pedir correção, faça a menor alteração que resolva a causa identificada dentro do escopo autorizado e preserve os contratos existentes.

Valide com a reprodução, testes ou verificações proporcionais ao problema e ao ambiente. Registre o que foi executado, o resultado observado e cenários não verificados. Não declare uma hipótese confirmada por uma verificação indireta nem trate testes ignorados como aprovados.

## Origem

Adaptada de [invest-debug](https://github.com/JonathanBenicio/invest-dashboard/blob/080e1bae1b194fdc3b9ffd5af9cea9119dc7e77f/.agents/skills/invest-debug/SKILL.md), no commit `080e1bae1b194fdc3b9ffd5af9cea9119dc7e77f` do invest-dashboard.
