-- SOMENTE para banco local de demonstração. Conteúdo fictício.
-- Não aplicar em projeto Supabase remoto. Inserções são idempotentes e não
-- sobrescrevem registros existentes.
insert into public.properties (id, code, title, city, neighborhood, price, bedrooms, parking, features, listing_url, updated_at)
values
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1', 'DEMO-001', 'Apartamento fictício com varanda', 'Campinas', 'Taquaral', 650000, 3, 2, array['Varanda'], 'https://example.invalid/imoveis/DEMO-001', '2026-01-01T12:00:00Z'),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2', 'DEMO-002', 'Casa fictícia com piscina', 'Campinas', 'Cambuí', 890000, 3, 2, array['Piscina'], 'https://example.invalid/imoveis/DEMO-002', '2026-01-01T12:00:00Z'),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3', 'DEMO-003', 'Apartamento fictício compacto', 'Jundiaí', 'Centro', 450000, 2, 1, array[]::text[], 'https://example.invalid/imoveis/DEMO-003', '2026-01-01T12:00:00Z')
on conflict do nothing;

insert into public.conversations (id, contact_name, status, last_message_at)
values ('11111111-1111-1111-1111-111111111111', 'Cliente de demonstração', 'automated', '2026-01-01T12:00:00Z')
on conflict do nothing;

insert into public.messages (id, conversation_id, author, body, created_at)
values ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1', '11111111-1111-1111-1111-111111111111', 'customer', '[Simulação] Olá, procuro um apartamento.', '2026-01-01T12:00:00Z')
on conflict do nothing;
