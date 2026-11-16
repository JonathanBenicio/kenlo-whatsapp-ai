# Kenlo + WhatsApp: plano de implementação

Protótipo local de uma camada de integração para Kenlo Prime, com catálogo, conversas e transferência para um corretor. O ambiente usa **dados fictícios e integrações simuladas**; ele não acessa Kenlo, WhatsApp ou Codex reais.

- [Plano de implementação](docs/plano.md)
- [Issues e acompanhamento](https://github.com/JonathanBenicio/kenlo-whatsapp-ai/issues)

O escopo e as integrações dependem de validação na conta Kenlo, na Meta e na plataforma OpenAI. Não inclua credenciais, contatos reais ou exportações de dados neste repositório.

## Iniciar a demonstração

Pré-requisitos: Docker e Docker Compose. Na raiz do repositório:

```sh
docker compose up --build -d
```

O painel está em <http://127.0.0.1:5173>, a API em <http://127.0.0.1:8080> e o n8n em <http://127.0.0.1:5678>. No primeiro acesso ao n8n, crie um usuário local de teste. Para interromper os serviços sem excluir os volumes, execute `docker compose down`.

Por padrão, a API usa armazenamento em memória: os imóveis e conversas fictícios voltam ao estado inicial quando a API reinicia. O n8n mantém seus dados em volumes próprios. O uso opcional de PostgreSQL local e o seed de demonstração estão descritos em [Banco e testes locais](supabase/README.md).

As portas são restritas à própria máquina e o ambiente não possui autenticação para dados reais. As senhas de demonstração do Compose são públicas e não podem ser reutilizadas em produção. Não envie informações de clientes, tokens ou arquivos reais do Kenlo para este ambiente.

O catálogo e o assistente usam conteúdo sintético. O XML de exemplo possui um formato próprio de teste e **não representa o formato do Kenlo**. A integração real com o Codex depende da elegibilidade do uso do plano ChatGPT; a Meta e o Kenlo dependem de acesso, configuração e validação específicos. O Microsoft Agent Framework não integra esta versão.

- [Banco e testes locais](supabase/README.md)
- [Fluxo demonstrativo do n8n](n8n/README.md)

## Verificações

Os mesmos testes principais também estão definidos no GitHub Actions. Para executá-los localmente, use o SDK .NET 10 e Node.js 24:

```sh
dotnet test backend/tests/Kenlo.Api.Tests.csproj --configuration Release
cd frontend
npm ci
npm run test
npm run build
```

O SDK .NET também pode ser executado em um container, como mostrado em [CI](.github/workflows/ci.yml). Os testes SQL têm instruções próprias em [Banco e testes locais](supabase/README.md).

## Skills do projeto

As instruções em `.agents/skills/` foram adaptadas de skills do repositório `invest-dashboard`. Elas apoiam descoberta de funcionalidades, diagnóstico de problemas e revisão de mudanças; cada arquivo identifica sua origem.
