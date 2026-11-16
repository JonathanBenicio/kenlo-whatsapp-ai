import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import {
  ArrowDownUp, ArrowRight, BedDouble, Building2, Check, ChevronDown,
  CircleAlert, CircleHelp, House, LayoutDashboard, LoaderCircle,
  MapPin, MessageCircle, MoreHorizontal, RefreshCw, Search, Send,
  ShieldCheck, SlidersHorizontal, Sparkles, UserRound, UsersRound,
  CarFront, X,
} from 'lucide-react';

type Property = {
  id: string; code: string; title: string; city: string; neighborhood: string;
  price: number; bedrooms: number; parking: number; features: string[];
  listingUrl: string | null; updatedAt: string;
};
type Message = { id: string; author: 'customer' | 'assistant' | 'broker'; text: string; createdAt: string };
type Conversation = { id: string; contactName: string; status: 'automated' | 'human'; lastMessageAt: string; messages: Message[] };
type Filters = { city: string; neighborhood: string; maxPrice: string; bedrooms: string; parking: string };
const initialFilters: Filters = { city: '', neighborhood: '', maxPrice: '', bedrooms: '', parking: '' };

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, { ...init, headers: { 'Content-Type': 'application/json', ...init?.headers } });
  if (!response.ok) throw new Error(`Não foi possível concluir a operação (${response.status}).`);
  return response.json() as Promise<T>;
}
const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 });
const time = (date: string) => {
  const value = new Date(date);
  return Number.isNaN(value.getTime()) ? '—' : new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' }).format(value);
};
const dateLabel = (date: string) => {
  const value = new Date(date);
  return Number.isNaN(value.getTime()) ? 'data não informada' : new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short' }).format(value).replace('.', '');
};
const initials = (name: string) => name.trim().split(/\s+/).slice(0, 2).map(word => word[0]?.toUpperCase() ?? '').join('');

function PropertyArt({ variant }: { variant: number }) {
  return <div className={`property-art art-${variant % 3}`} aria-hidden="true">
    <span className="art-sun" /><span className="art-hill hill-back" /><span className="art-hill hill-front" />
    <span className="building building-back"><i /><i /><i /></span>
    <span className="building building-main"><i /><i /><i /><i /><i /><i /></span>
    <span className="art-tree tree-one" /><span className="art-tree tree-two" />
  </div>;
}

function App() {
  const [properties, setProperties] = useState<Property[]>([]);
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selectedProperty, setSelectedProperty] = useState<string | null>(null);
  const [filters, setFilters] = useState<Filters>(initialFilters);
  const [appliedFilters, setAppliedFilters] = useState<Filters>(initialFilters);
  const [message, setMessage] = useState('');
  const [propertyLoading, setPropertyLoading] = useState(true);
  const [conversationLoading, setConversationLoading] = useState(true);
  const [propertyError, setPropertyError] = useState('');
  const [conversationError, setConversationError] = useState('');
  const [actionError, setActionError] = useState('');
  const [busy, setBusy] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const pendingMessage = useRef<{ text: string; conversationId: string; messageId: string } | null>(null);
  const conversation = conversations.find(item => item.id === selectedId) ?? null;

  useEffect(() => {
    const controller = new AbortController();
    const query = new URLSearchParams();
    Object.entries(appliedFilters).forEach(([key, value]) => { if (value.trim()) query.set(key, value.trim()); });
    setPropertyLoading(true); setPropertyError('');
    request<Property[]>(`/api/properties${query.size ? `?${query}` : ''}`, { signal: controller.signal })
      .then(setProperties).catch(error => { if (!controller.signal.aborted) setPropertyError(error instanceof Error ? error.message : 'Erro ao carregar imóveis.'); })
      .finally(() => { if (!controller.signal.aborted) setPropertyLoading(false); });
    return () => controller.abort();
  }, [appliedFilters, refreshKey]);

  useEffect(() => {
    const controller = new AbortController();
    setConversationLoading(true); setConversationError('');
    request<Conversation[]>('/api/conversations', { signal: controller.signal })
      .then(data => { setConversations(data); setSelectedId(current => data.some(item => item.id === current) ? current : data[0]?.id ?? null); })
      .catch(error => { if (!controller.signal.aborted) setConversationError(error instanceof Error ? error.message : 'Erro ao carregar conversas.'); })
      .finally(() => { if (!controller.signal.aborted) setConversationLoading(false); });
    return () => controller.abort();
  }, [refreshKey]);

  const updateConversation = useCallback((updated: Conversation) => {
    setConversations(current => current.map(item => item.id === updated.id ? updated : item));
  }, []);

  async function changeMode(action: 'takeover' | 'resume') {
    if (!conversation) return;
    setBusy(true); setActionError('');
    try { updateConversation(await request<Conversation>(`/api/conversations/${encodeURIComponent(conversation.id)}/${action}`, { method: 'POST' })); }
    catch (error) { setActionError(error instanceof Error ? error.message : 'Erro ao alterar atendimento.'); }
    finally { setBusy(false); }
  }

  async function sendMessage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const text = message.trim();
    if (!conversation || !text || busy) return;
    if (!pendingMessage.current || pendingMessage.current.text !== text || pendingMessage.current.conversationId !== conversation.id) {
      pendingMessage.current = { conversationId: conversation.id, messageId: crypto.randomUUID(), text };
    }
    setBusy(true); setActionError('');
    try {
      const result = await request<Conversation>('/api/simulation/messages', { method: 'POST', body: JSON.stringify(pendingMessage.current) });
      updateConversation(result); setMessage(''); pendingMessage.current = null;
    } catch (error) { setActionError(error instanceof Error ? error.message : 'Erro ao enviar mensagem simulada.'); }
    finally { setBusy(false); }
  }

  function updateFilter(key: keyof Filters, value: string) { setFilters(current => ({ ...current, [key]: value })); }
  function applyFilters(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setAppliedFilters({ ...filters }); }
  const humanCount = conversations.filter(item => item.status === 'human').length;
  const autoCount = conversations.filter(item => item.status === 'automated').length;
  const totalMessages = conversations.reduce((sum, item) => sum + item.messages.length, 0);
  const firstName = conversation?.contactName.trim().split(/\s+/)[0] ?? '';

  return <div className="app-shell">
    <aside className="sidebar" aria-label="Navegação principal">
      <a href="#inicio" className="brand" aria-label="Central Imobiliária, início"><span className="brand-mark"><Building2 size={21} strokeWidth={2.2} /></span><span className="brand-text">central<span>.</span><small>IMOBILIÁRIA</small></span></a>
      <div className="workspace"><span className="workspace-icon"><House size={17} /></span><span><strong>Minha imobiliária</strong><small>Ambiente de teste</small></span><ChevronDown size={15} /></div>
      <p className="nav-heading">ESPAÇO DE TRABALHO</p>
      <nav className="navigation">
        <a href="#inicio" className="nav-link active"><LayoutDashboard size={18} />Visão geral</a>
        <a href="#imoveis" className="nav-link"><Building2 size={18} />Imóveis</a>
        <a href="#atendimentos" className="nav-link"><MessageCircle size={18} />Atendimentos <span className="nav-number">{conversations.length}</span></a>
      </nav>
      <div className="sidebar-bottom">
        <div className="sandbox-box"><div className="sandbox-icon"><Sparkles size={16} /></div><strong>Explore com tranquilidade</strong><p>Este é um ambiente de demonstração com dados fictícios.</p><span><ShieldCheck size={14} /> Nenhuma mensagem real</span></div>
        <div className="sidebar-profile"><span className="avatar profile-avatar"><UserRound size={17}/></span><span><strong>Equipe imobiliária</strong><small>Acesso local</small></span><MoreHorizontal size={18}/></div>
      </div>
    </aside>

    <main id="inicio" className="main-content">
      <header className="topbar"><div className="breadcrumb">Área de trabalho <span>/</span> <strong>Visão geral</strong></div><div className="topbar-right"><span className="demo-pill"><span />Demonstração</span><span className="topbar-divider"/><span className="small-avatar"><UserRound size={16}/></span><span className="topbar-name">Equipe imobiliária</span></div></header>
      <div className="main-inner">
        <div className="intro-row"><div><div className="eyebrow"><span /> SEU ESPAÇO DE TRABALHO</div><h1>Olá, equipe <span>✳</span></h1><p>Acompanhe seus imóveis e atendimentos em um só lugar.</p></div><button className="button button-outline refresh" onClick={() => setRefreshKey(value => value + 1)}><RefreshCw size={16}/> Atualizar dados</button></div>
        <div className="notice" role="note"><span className="notice-icon"><Sparkles size={16}/></span><div><strong>Você está explorando uma demonstração.</strong> Imóveis, contatos e mensagens são fictícios. Nenhum dado será enviado ao Kenlo ou WhatsApp.</div><span className="notice-label">AMBIENTE DE TESTE</span></div>

        <section className="metrics" aria-label="Resumo do ambiente de demonstração">
          <article className="metric"><div className="metric-top"><span>Imóveis encontrados</span><span className="metric-icon mint"><Building2 size={17}/></span></div><strong>{propertyLoading || propertyError ? '—' : properties.length.toString().padStart(2, '0')}</strong><small>{Object.values(appliedFilters).some(Boolean) ? 'Com os filtros aplicados' : 'No catálogo de demonstração'}</small></article>
          <article className="metric"><div className="metric-top"><span>Conversas</span><span className="metric-icon lilac"><MessageCircle size={17}/></span></div><strong>{conversationLoading || conversationError ? '—' : conversations.length.toString().padStart(2, '0')}</strong><small>Atendimentos fictícios</small></article>
          <article className="metric"><div className="metric-top"><span>Em atendimento humano</span><span className="metric-icon peach"><UsersRound size={17}/></span></div><strong>{conversationLoading || conversationError ? '—' : humanCount.toString().padStart(2, '0')}</strong><small>Conversas assumidas</small></article>
          <article className="metric"><div className="metric-top"><span>Em automação</span><span className="metric-icon blue"><Sparkles size={17}/></span></div><strong>{conversationLoading || conversationError ? '—' : autoCount.toString().padStart(2, '0')}</strong><small>{conversationError ? 'Histórico indisponível' : `${totalMessages} mensagens no histórico`}</small></article>
        </section>

        <div className="content-grid">
          <section className="panel catalog-panel" id="imoveis" aria-labelledby="properties-heading">
            <div className="section-heading"><div><div className="section-kicker"><span className="section-dot"/> CATÁLOGO</div><h2 id="properties-heading">Encontre o imóvel ideal</h2><p>Explore as opções disponíveis na demonstração.</p></div><span className="section-icon"><Building2 size={19}/></span></div>
            <form className="filters" onSubmit={applyFilters}>
              <div className="filter-row"><label className="input-wrap location-field"><MapPin size={16}/><span className="sr-only">Cidade</span><input placeholder="Cidade" value={filters.city} onChange={event => updateFilter('city', event.target.value)} /></label><label className="input-wrap"><Search size={16}/><span className="sr-only">Bairro</span><input placeholder="Bairro" value={filters.neighborhood} onChange={event => updateFilter('neighborhood', event.target.value)} /></label><button className="search-button" type="submit"><Search size={16}/> Buscar</button></div>
              <div className="filter-chips"><span className="filter-caption"><SlidersHorizontal size={15}/> Filtros</span><label className="chip"><span>Até R$</span><input aria-label="Preço máximo em reais" type="number" min="0" step="1000" placeholder="sem limite" value={filters.maxPrice} onChange={event => updateFilter('maxPrice', event.target.value)}/></label><label className="chip"><BedDouble size={15}/><span className="sr-only">Mínimo de quartos</span><select value={filters.bedrooms} onChange={event => updateFilter('bedrooms', event.target.value)}><option value="">Quartos</option><option value="1">1+ quarto</option><option value="2">2+ quartos</option><option value="3">3+ quartos</option><option value="4">4+ quartos</option></select></label><label className="chip"><CarFront size={15}/><span className="sr-only">Mínimo de vagas</span><select value={filters.parking} onChange={event => updateFilter('parking', event.target.value)}><option value="">Vagas</option><option value="1">1+ vaga</option><option value="2">2+ vagas</option><option value="3">3+ vagas</option></select></label>{Object.values(appliedFilters).some(Boolean) && <button type="button" className="clear-button" onClick={() => { setFilters(initialFilters); setAppliedFilters(initialFilters); }}>Limpar <X size={13}/></button>}</div>
            </form>
            <div className="result-heading"><div><strong>Imóveis encontrados</strong><span>{propertyLoading ? 'Carregando' : propertyError ? 'Indisponível' : `${properties.length} ${properties.length === 1 ? 'resultado' : 'resultados'}`}</span></div><div className="result-sort"><ArrowDownUp size={14}/> Catálogo fictício</div></div>
            {propertyLoading ? <div className="state-box"><LoaderCircle className="spin" size={20}/> Carregando imóveis...</div> : propertyError ? <div className="state-box error" role="alert"><CircleAlert size={20}/><span>{propertyError}</span></div> : properties.length === 0 ? <div className="state-box empty"><Search size={22}/><strong>Nenhum imóvel encontrado</strong><span>Tente ajustar os filtros da busca.</span></div> : <div className="property-list">{properties.map((property, index) => <article className="property-card" key={property.id}>
              <PropertyArt variant={index}/><div className="property-info"><div className="property-topline"><span className="code">CÓD. {property.code}</span><span className="property-tag">DEMONSTRAÇÃO</span></div><h3>{property.title}</h3><p className="location"><MapPin size={13}/>{[property.neighborhood, property.city].filter(Boolean).join(', ')}</p><div className="property-facts"><span><BedDouble size={15}/> {property.bedrooms} {property.bedrooms === 1 ? 'quarto' : 'quartos'}</span><span><CarFront size={15}/> {property.parking} {property.parking === 1 ? 'vaga' : 'vagas'}</span></div><div className="property-bottom"><div><span className="price-label">VALOR</span><strong>{money.format(property.price)}</strong></div><button type="button" className="detail-link" aria-expanded={selectedProperty === property.id} onClick={() => setSelectedProperty(current => current === property.id ? null : property.id)}>{selectedProperty === property.id ? 'Fechar' : 'Ver detalhes'} <ArrowRight size={15}/></button></div>{selectedProperty === property.id && <div className="property-details"><span>Atualizado em {dateLabel(property.updatedAt)}</span>{property.features.length > 0 && <span>{property.features.join(' · ')}</span>}<span>Imóvel fictício. Disponibilidade não verificada.</span></div>}</div></article>)}</div>}
            <div className="catalog-foot"><span><CircleHelp size={14}/> Os resultados são apenas para teste.</span><span>Atualização manual <ArrowRight size={13}/></span></div>
          </section>

          <section className="panel conversations-panel" id="atendimentos" aria-labelledby="conversation-heading">
            <div className="section-heading conversation-header"><div><div className="section-kicker"><span className="section-dot purple-dot"/> CONVERSAS</div><h2 id="conversation-heading">Seus atendimentos</h2><p>Acompanhe e participe das conversas.</p></div><span className="section-icon conversation-icon"><MessageCircle size={19}/></span></div>
            {conversationLoading ? <div className="state-box"><LoaderCircle className="spin" size={20}/> Carregando conversas...</div> : conversationError ? <div className="state-box error" role="alert"><CircleAlert size={20}/><span>{conversationError}</span></div> : conversations.length === 0 ? <div className="state-box empty"><MessageCircle size={22}/><strong>Nenhuma conversa</strong><span>As conversas de demonstração aparecerão aqui.</span></div> : <>
              <div className="conversation-selector"><span>CONVERSA SELECIONADA</span><label className="select-wrap"><span className="sr-only">Selecionar conversa</span><select value={selectedId ?? ''} onChange={event => { setSelectedId(event.target.value); setActionError(''); setMessage(''); }}><option value="" disabled>Selecione uma conversa</option>{conversations.map(item => <option key={item.id} value={item.id}>{item.contactName}</option>)}</select><ChevronDown size={16}/></label></div>
              {conversation && <><div className="chat-contact"><span className="avatar contact-avatar">{initials(conversation.contactName)}</span><div><strong>{conversation.contactName}</strong><small><span className="online-dot"/> Contato fictício</small></div><span className={`status-badge ${conversation.status === 'human' ? 'status-human' : ''}`}>{conversation.status === 'human' ? <UserRound size={13}/> : <Sparkles size={13}/>} {conversation.status === 'human' ? 'Humano' : 'Automação'}</span></div>
                <div className="chat-body" aria-live="polite"><div className="chat-date"><span/> HISTÓRICO DE TESTE <span/></div>{conversation.messages.length === 0 ? <p className="chat-empty">Esta conversa ainda não tem mensagens.</p> : conversation.messages.map(item => <div key={item.id} className={`message-row ${item.author === 'customer' ? 'incoming' : 'outgoing'}`}><div className="message-bubble"><p>{item.text}</p><div>{item.author === 'assistant' && <Sparkles size={11}/>} {item.author === 'broker' && <UserRound size={11}/>} <span>{time(item.createdAt)}</span>{item.author !== 'customer' && <Check size={12}/>}</div></div></div>)}</div>
                <div className="chat-actions"><div className="chat-mode"><span className={`mode-indicator ${conversation.status === 'human' ? 'human-indicator' : ''}`}/><div><strong>{conversation.status === 'human' ? 'Atendimento assumido' : 'Automação ativa'}</strong><small>{conversation.status === 'human' ? 'Respostas automáticas pausadas' : 'Você pode assumir quando quiser'}</small></div></div><button type="button" className="takeover-button" disabled={busy} onClick={() => void changeMode(conversation.status === 'human' ? 'resume' : 'takeover')}>{conversation.status === 'human' ? 'Retomar auto.' : 'Assumir'}</button></div>
                {actionError && <p className="action-error" role="alert"><CircleAlert size={14}/>{actionError}</p>}
                <form className="message-compose" onSubmit={event => void sendMessage(event)}><label className="sr-only" htmlFor="message-input">Mensagem fictícia do cliente</label><input id="message-input" value={message} onChange={event => setMessage(event.target.value)} maxLength={2000} placeholder={`Simule uma mensagem de ${firstName}...`} disabled={busy}/><button type="submit" title="Enviar mensagem fictícia" aria-label="Enviar mensagem fictícia" disabled={busy || !message.trim()}><Send size={17}/></button></form><p className="compose-caption">O texto acima simula uma mensagem recebida do cliente.</p>
              </>}
            </>}
          </section>
        </div>
        <footer className="footer"><span>© Central Imobiliária <b>·</b> Ambiente local de demonstração</span><span><ShieldCheck size={14}/> Dados 100% fictícios</span></footer>
      </div>
    </main>
  </div>;
}

export default App;
