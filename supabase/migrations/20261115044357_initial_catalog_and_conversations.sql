-- MVP single agency: all user-facing access is denied until an authorization
-- model is defined. Only a trusted server-side Supabase service role has access.
create table public.properties (
  id uuid primary key default gen_random_uuid(),
  code text not null unique,
  title text not null,
  city text not null,
  neighborhood text not null,
  price numeric(14, 2) not null check (price >= 0),
  bedrooms integer not null check (bedrooms >= 0),
  parking integer not null check (parking >= 0),
  features text[] not null default array[]::text[],
  listing_url text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create index properties_city_price_idx on public.properties (city, price);

create table public.import_runs (
  id uuid primary key default gen_random_uuid(),
  source_kind text not null check (source_kind in ('feed', 'manual', 'synthetic')),
  status text not null check (status in ('staged', 'completed', 'failed')),
  source_hash text,
  created_at timestamptz not null default now(),
  completed_at timestamptz
);

create table public.leads (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  phone text,
  source text not null,
  external_id text,
  created_at timestamptz not null default now(),
  unique (source, external_id)
);

create table public.conversations (
  id uuid primary key default gen_random_uuid(),
  lead_id uuid references public.leads (id),
  contact_name text not null,
  status text not null default 'automated' check (status in ('automated', 'human')),
  last_message_at timestamptz not null default now(),
  created_at timestamptz not null default now()
);

create index conversations_last_message_idx on public.conversations (last_message_at desc);

create table public.messages (
  id uuid primary key default gen_random_uuid(),
  conversation_id uuid not null references public.conversations (id) on delete cascade,
  external_id text,
  author text not null check (author in ('customer', 'assistant', 'broker')),
  body text not null,
  created_at timestamptz not null default now(),
  unique (conversation_id, external_id)
);

create index messages_conversation_created_idx on public.messages (conversation_id, created_at, id);

create table public.crm_tasks (
  id uuid primary key default gen_random_uuid(),
  conversation_id uuid references public.conversations (id),
  task_type text not null,
  status text not null default 'pending' check (status in ('pending', 'processing', 'needs_review', 'completed', 'failed')),
  idempotency_key text not null unique,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create index crm_tasks_status_created_idx on public.crm_tasks (status, created_at);

alter table public.properties enable row level security;
alter table public.import_runs enable row level security;
alter table public.leads enable row level security;
alter table public.conversations enable row level security;
alter table public.messages enable row level security;
alter table public.crm_tasks enable row level security;

-- Explicitly revoke inherited/default exposure on both existing and fresh
-- projects. Service credentials must remain exclusively on the backend.
revoke all on public.properties, public.import_runs, public.leads, public.conversations,
  public.messages, public.crm_tasks from public, anon, authenticated;
grant select, insert, update, delete on public.properties, public.import_runs, public.leads,
  public.conversations, public.messages, public.crm_tasks to service_role;
