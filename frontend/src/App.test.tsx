// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';

const property = { id: '1', code: 'I-01', title: 'Apartamento de teste', city: 'Campinas', neighborhood: 'Centro', price: 500000, bedrooms: 3, parking: 2, features: ['Varanda'], listingUrl: null, updatedAt: '2026-01-01T10:00:00Z' };
const conversation = { id: 'c-1', contactName: 'Marina Fictícia', status: 'automated', lastMessageAt: '2026-01-01T10:00:00Z', messages: [{ id: 'm-1', author: 'customer', text: 'Quero um apartamento.', createdAt: '2026-01-01T10:00:00Z' }] };
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
    if (url.startsWith('/api/properties')) return json([property]);
    if (url === '/api/conversations') return json([conversation]);
    if (url.endsWith('/takeover') && init?.method === 'POST') return json({ ...conversation, status: 'human' });
    if (url === '/api/simulation/messages') return json({ ...conversation, messages: [...conversation.messages, { id: 'm-2', author: 'customer', text: 'Olá', createdAt: '2026-01-01T10:01:00Z' }] });
    return json({}, 404);
  }));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('Painel de demonstração', () => {
  it('carrega imóveis e envia filtros estruturados ao catálogo', async () => {
    render(<App />);
    expect(await screen.findByText('Apartamento de teste')).toBeInTheDocument();
    expect(await screen.findByRole('option', { name: 'Marina Fictícia' })).toBeInTheDocument();
    fireEvent.change(screen.getByRole('textbox', { name: 'Cidade' }), { target: { value: 'Campinas' } });
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Preço máximo em reais' }), { target: { value: '700000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }));
    await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/properties?city=Campinas&maxPrice=700000', expect.any(Object)));
  });

  it('assume uma conversa e mostra que as respostas automáticas foram pausadas', async () => {
    render(<App />);
    const section = await screen.findByRole('region', { name: 'Seus atendimentos' });
    fireEvent.click(within(section).getByRole('button', { name: 'Assumir' }));
    expect(await screen.findByText('Respostas automáticas pausadas')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retomar auto.' })).toBeInTheDocument();
  });

  it('envia mensagem fictícia identificada à conversa selecionada', async () => {
    render(<App />);
    const input = await screen.findByRole('textbox', { name: 'Mensagem fictícia do cliente' });
    fireEvent.change(input, { target: { value: 'Olá' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar mensagem fictícia' }));
    expect(await screen.findByText('Olá')).toBeInTheDocument();
    const call = vi.mocked(fetch).mock.calls.find(([url]) => url === '/api/simulation/messages');
    expect(call).toBeDefined();
    expect(JSON.parse(String(call?.[1]?.body))).toEqual(expect.objectContaining({ conversationId: 'c-1', messageId: expect.any(String), text: 'Olá' }));
    expect(input).toHaveValue('');
  });

  it('não apresenta contagens como zero quando a API está indisponível', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json({}, 503)));
    render(<App />);
    expect(await screen.findAllByRole('alert')).toHaveLength(2);
    const summary = screen.getByRole('region', { name: 'Resumo do ambiente de demonstração' });
    expect(within(summary).getAllByText('—')).toHaveLength(4);
    expect(within(summary).getByText('Histórico indisponível')).toBeInTheDocument();
    expect(screen.getByText('Indisponível')).toBeInTheDocument();
  });
});
