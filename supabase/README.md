# Banco local

A migration cria o modelo inicial para uma imobiliária. As tabelas expostas têm RLS habilitada, sem políticas de acesso para `anon` ou `authenticated`, e seus privilégios são revogados. Apenas o backend deve usar credenciais privilegiadas; nunca as coloque no navegador. O modelo de usuários e as políticas efetivas de acesso devem ser definidos antes de expor os dados reais.

Use a CLI Supabase **3.1.0** e Docker. Na raiz do projeto:

```sh
npx --yes supabase@3.1.0 start
npx --yes supabase@3.1.0 db reset
```

Na CLI 3.1.0, `db reset` usa o banco **local** por padrão: ele o recria, apaga seus dados e aplica todas as migrations. Não use `--remote` nem aponte esses comandos a um projeto hospedado sem autorização. A versão do PostgreSQL no `config.toml` deve ser conferida contra o projeto Cloud antes de qualquer implantação futura.

Para executar os testes SQL no banco local já iniciado, usando a imagem do próprio banco Supabase:

```sh
docker exec -i supabase_db_kenlo-whatsapp-ai psql -U postgres -d postgres -v ON_ERROR_STOP=1 < supabase/tests/database.sql
```

Os testes inserem apenas dados fictícios em uma transação revertida no final. Conferem a deduplicação de eventos, o upsert do catálogo e a negação de acesso aos papéis `anon` e `authenticated`, inclusive quando um GRANT temporário é concedido. A ausência de políticas é intencional nesta fase. Os testes não verificam configuração, backups ou dados no Supabase Cloud.

Para preparar **apenas o banco local de demonstração** com os mesmos dados sintéticos do modo em memória, execute explicitamente:

```sh
docker exec -i supabase_db_kenlo-whatsapp-ai psql -U postgres -d postgres -v ON_ERROR_STOP=1 < supabase/seed.demo.sql
```

O seed é idempotente e não substitui linhas existentes. O carregamento automático está desativado para evitar inserções involuntárias em outros ambientes.

A API utiliza memória por padrão. Para experimentar a persistência, configure `STORE_MODE=postgres` e `DATABASE_URL` no ambiente do servidor com uma *connection string* Npgsql para esse PostgreSQL local. O Compose repassa essas variáveis e disponibiliza `host.docker.internal` ao container; a conectividade depende da configuração Docker da máquina. A API de demonstração aceita somente hosts locais e não aplica migrations automaticamente. A senha de conexão direta ao PostgreSQL não é a chave da Data API; mantenha ambas fora do repositório e do painel.
