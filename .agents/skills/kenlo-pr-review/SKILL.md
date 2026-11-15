---
name: kenlo-pr-review
description: Revisar alterações e pull requests quanto a correção, segurança, regressões e testes relevantes.
---

# Revisão de mudança

Identifique objetivo, diff e arquivos afetados. Compare o comportamento com os contratos e consumidores, procurando regressões, problemas de autorização, integridade de dados, consultas ineficientes e lacunas de teste relevantes. Execute verificações proporcionais quando forem viáveis e seguras.

Reporte primeiro achados acionáveis com severidade, impacto, condição de ocorrência e localização; diferencie fatos observados de hipóteses e indique testes ou cenários não verificados. Não publique comentários nem altere o PR sem pedido do usuário.

Quando a descrição ou as evidências do PR fizerem parte do escopo da revisão, consulte os modelos existentes no repositório, se houver, como referências. Ausência de um campo do modelo só é achado se tiver impacto concreto no trabalho revisado.

## Origem

Adaptada de [invest-pr-review](https://github.com/JonathanBenicio/invest-dashboard/blob/080e1bae1b194fdc3b9ffd5af9cea9119dc7e77f/.agents/skills/invest-pr-review/SKILL.md), no commit `080e1bae1b194fdc3b9ffd5af9cea9119dc7e77f` do invest-dashboard.
