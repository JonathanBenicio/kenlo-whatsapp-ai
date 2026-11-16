# n8n — demonstração local

O Compose inicia n8n em `http://127.0.0.1:5678`, com banco PostgreSQL e volumes próprios. O banco do n8n não é o banco do Supabase. A API e o painel locais ficam em `http://127.0.0.1:8080` e `http://127.0.0.1:5173`.

Esta configuração, incluindo credenciais e chave de criptografia fictícias, é exclusiva para testes locais e não serve para produção. No primeiro acesso, configure o usuário local do n8n. Nunca insira dados de clientes ou credenciais reais nesta demonstração.

Importe `n8n/workflows/conversa-simulada.json` pela interface do n8n (menu de workflows, **Import from File**) e execute-o manualmente. O fluxo envia uma única mensagem fictícia à rota de simulação da API pela rede interna do Compose; não chama WhatsApp, Kenlo, Codex nem serviços externos. O identificador sintético do contato pode ser visualizado no painel.

Os dados persistem nos volumes `n8n-data` e `n8n-db-data`. Para interromper sem excluir dados, use `docker compose down`. A configuração e os volumes deste projeto não são um procedimento de backup ou implantação de produção. O Supabase local é inicializado separadamente pelo CLI, conforme a documentação do projeto.
