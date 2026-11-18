\set ON_ERROR_STOP on
begin;

-- All payloads below are fictional and rolled back at the end.
insert into public.properties (code, title, city, neighborhood, price, bedrooms, parking)
values ('SYN-001', 'Imóvel fictício', 'Campinas', 'Centro', 500000, 2, 1)
on conflict (code) do update set title = excluded.title, price = excluded.price;
insert into public.properties (code, title, city, neighborhood, price, bedrooms, parking)
values ('SYN-001', 'Imóvel fictício atualizado', 'Campinas', 'Centro', 510000, 2, 1)
on conflict (code) do update set title = excluded.title, price = excluded.price;

insert into public.conversations (id, contact_name)
values ('11111111-1111-4111-8111-111111111111', 'Contato fictício');
insert into public.messages (conversation_id, external_id, author, body)
values ('11111111-1111-4111-8111-111111111111', 'event-test-1', 'customer', 'Olá')
on conflict (conversation_id, external_id) do nothing;
insert into public.messages (conversation_id, external_id, author, body)
values ('11111111-1111-4111-8111-111111111111', 'event-test-1', 'customer', 'Olá')
on conflict (conversation_id, external_id) do nothing;
-- Multiple assistant messages need not carry an external provider ID.
insert into public.messages (conversation_id, author, body)
values ('11111111-1111-4111-8111-111111111111', 'assistant', 'Resposta fictícia 1'),
       ('11111111-1111-4111-8111-111111111111', 'assistant', 'Resposta fictícia 2');

do $$
declare t text;
begin
  foreach t in array array['properties','import_runs','leads','conversations','messages','crm_tasks'] loop
    if not exists (
      select 1 from pg_class c join pg_namespace n on n.oid = c.relnamespace
      where n.nspname = 'public' and c.relname = t and c.relrowsecurity
    ) then raise exception 'RLS não habilitada em %', t; end if;
    if has_table_privilege('anon', 'public.' || t, 'SELECT, INSERT, UPDATE, DELETE')
       or has_table_privilege('authenticated', 'public.' || t, 'SELECT, INSERT, UPDATE, DELETE') then
      raise exception 'Tabela exposta indevidamente: %', t;
    end if;
    if not has_table_privilege('service_role', 'public.' || t, 'SELECT') then
      raise exception 'Acesso de backend ausente: %', t;
    end if;
  end loop;
  if (select count(*) from public.properties where code = 'SYN-001') <> 1
     or (select price from public.properties where code = 'SYN-001') <> 510000 then
    raise exception 'Upsert do catálogo não é idempotente';
  end if;
  if (select count(*) from public.messages
      where conversation_id = '11111111-1111-4111-8111-111111111111'
        and external_id = 'event-test-1') <> 1 then
    raise exception 'Evento duplicado gerou mais de uma mensagem';
  end if;
  if (select count(*) from public.messages
      where conversation_id = '11111111-1111-4111-8111-111111111111'
        and external_id is null) <> 2 then
    raise exception 'Mensagens sem id externo não foram preservadas';
  end if;
end $$;

-- Temporary SELECT/INSERT grants check that RLS still denies access if grants
-- are accidentally introduced later. The transaction rolls the grants back.
grant select on public.properties, public.import_runs, public.leads, public.conversations,
  public.messages, public.crm_tasks to anon, authenticated;
grant insert on public.conversations to anon, authenticated;
set local role anon;
do $$ begin
  if exists (select 1 from public.properties) or exists (select 1 from public.conversations)
     or exists (select 1 from public.messages) or exists (select 1 from public.import_runs)
     or exists (select 1 from public.leads) or exists (select 1 from public.crm_tasks) then
    raise exception 'RLS permitiu leitura anônima';
  end if;
  begin
    insert into public.conversations (id, contact_name)
    values ('22222222-2222-4222-8222-222222222222', 'Não deve ser gravado');
    raise exception 'RLS permitiu gravação anônima';
  exception when insufficient_privilege then null;
  end;
end $$;
reset role;
set local role authenticated;
do $$ begin
  if exists (select 1 from public.properties) or exists (select 1 from public.conversations)
     or exists (select 1 from public.messages) or exists (select 1 from public.import_runs)
     or exists (select 1 from public.leads) or exists (select 1 from public.crm_tasks) then
    raise exception 'RLS permitiu leitura de usuário sem política';
  end if;
  begin
    insert into public.conversations (id, contact_name)
    values ('33333333-3333-4333-8333-333333333333', 'Não deve ser gravado');
    raise exception 'RLS permitiu gravação de usuário sem política';
  exception when insufficient_privilege then null;
  end;
end $$;
reset role;
rollback;
