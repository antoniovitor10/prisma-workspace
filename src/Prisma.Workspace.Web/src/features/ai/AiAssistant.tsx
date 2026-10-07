import { useCallback, useContext, useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useLocation } from 'react-router-dom';
import styled from 'styled-components';
import { api } from '../../services/api';
import { OrganizationStateContext } from '../organizations/OrganizationState';
import { useAiStatus, readAiEvents, type AiSource } from './aiClient';
import { AiConversationControls, type AiChoice } from './AiConversationControls';

type Conversation = { id: string; title: string };
type Message = { role: string; content: string; sourcesJson?: string; sources?: AiSource[] };


const Launcher = styled.button`
  position: fixed; right: 22px; bottom: 22px; z-index: 80; min-width: 54px; min-height: 48px;
  padding: 10px 18px; border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.brandControlBackground}; color: white; font-weight: 750;
  &:focus-visible { outline: 3px solid ${({ theme }) => theme.color.accentBlueAccessible}; outline-offset: 3px; }
`;
const Panel = styled.aside<{ $expanded: boolean }>`
  position: fixed; right: 16px; top: 88px; bottom: ${({ $expanded }) => $expanded ? '16px' : '84px'}; z-index: 85; width: min(${({ $expanded }) => $expanded ? '1100px' : '480px'}, calc(100vw - 32px));
  display: flex; flex-direction: column; border-radius: ${({ theme }) => theme.radius.lg};
  box-shadow: ${({ theme }) => theme.shadow.lg}; background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
  header { padding: 20px; display: flex; align-items: center; justify-content: space-between; gap: 12px; border-bottom: 1px solid ${({ theme }) => theme.color.border}; }
  header .header-actions { display: flex; gap: 6px; } .expand-toggle { display: inline-block; }
  .chat-workspace { display: grid; grid-template-columns: ${({ $expanded }) => $expanded ? '230px minmax(0,1fr)' : 'minmax(0,1fr)'}; flex: 1; min-height: 0; }
  .conversation-main { display: flex; flex-direction: column; min-width: 0; min-height: 0; }
  .context { padding: 12px 20px; margin: 0; font-size: 12px; color: ${({ theme }) => theme.color.textMutedAccessible}; border-bottom: 1px solid ${({ theme }) => theme.color.border}; }
  h2 { font-size: 18px; margin: 0; } button { min-height: 42px; padding: 7px 12px; border-radius: ${({ theme }) => theme.radius.md}; }
  button:focus-visible, select:focus-visible, textarea:focus-visible { outline: 2px solid ${({ theme }) => theme.color.brandAccessible}; outline-offset: 2px; }
  button:disabled { opacity: .55; cursor: default; }
  nav { display: flex; gap: 8px; padding: 16px; flex-wrap: wrap; border-bottom: 1px solid ${({ theme }) => theme.color.border}; align-content: start; ${({ $expanded, theme }) => $expanded && `border-bottom: 0; border-right: 1px solid ${theme.color.border}; overflow-y: auto;`} }
  nav small { width: 100%; } .history-actions { display: flex; gap: 6px; flex-wrap: wrap; }
  .history-toggle { display: none; } .history-fields { display: flex; flex-wrap: wrap; gap: 8px; width: 100%; }
  select { width: 100%; min-height: 42px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; padding: 6px; background: ${({ theme }) => theme.color.surface}; }
  form { display: grid; gap: 10px; padding: 16px 20px; border-top: 1px solid ${({ theme }) => theme.color.border}; max-height: 55%; overflow-y: auto; }
  .compose-row { display: grid; grid-template-columns: minmax(0,1fr) auto; gap: 10px; align-items: end; } .compose-row button { min-height: 46px; }
  textarea { resize: vertical; min-height: 72px; max-height: 160px; padding: 10px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; }
  form button { background: ${({ theme }) => theme.color.brandControlBackground}; color: white; justify-self: end; }
  small { color: ${({ theme }) => theme.color.textMutedAccessible}; font-size: 12px; }
  .assistant-selectors { display: grid; grid-template-columns: minmax(0,1fr) minmax(0,1fr); gap: 10px; align-items: end; } .assistant-selectors label { min-width: 0; display: grid; gap: 6px; font-size: 12px; }
  .assistant-selectors select { color: ${({ theme }) => theme.color.text}; font-size: 13px; } .default-model { font-size: 12px; margin: 0; overflow-wrap: anywhere; align-self: center; }
  .assistant-selectors .text-action { justify-self: start; padding: 0; border: 0; background: transparent; color: ${({ theme }) => theme.color.brandAccessible}; font-size: 12px; text-decoration: underline; text-underline-offset: 3px; }
  .selector-notice { grid-column: 1 / -1; margin: 0; font-size: 12px; } select, textarea { color: ${({ theme }) => theme.color.text}; } textarea { background: ${({ theme }) => theme.color.surface}; caret-color: ${({ theme }) => theme.color.brandAccessible}; }
  button { color: ${({ theme }) => theme.color.text}; background: ${({ theme }) => theme.color.surface}; border: 1px solid ${({ theme }) => theme.color.border}; } button:hover:not(:disabled) { border-color: ${({ theme }) => theme.color.brandAccessible}; }
  @media(max-width: 768px) { inset: 0; width: 100%; border-radius: 0; height: 100dvh; .chat-workspace { grid-template-columns: minmax(0,1fr); } nav { border-right: 0; border-bottom: 1px solid ${({ theme }) => theme.color.border}; max-height: 220px; overflow-y: auto; padding: 8px 16px; } .expand-toggle { display: none; } header { padding: 16px; } .history-toggle { display: block; } .history-fields[data-collapsed=true] { display: none; } }
  @media(max-width: 480px) { .assistant-selectors { grid-template-columns: minmax(0,1fr); } }
`;
const Feed = styled.div`
  flex: 1; overflow-y: auto; overscroll-behavior: contain; padding: 16px;
  article { margin-bottom: 22px; } strong { font-size: 12px; color: ${({ theme }) => theme.color.textMutedAccessible}; }
  p { margin-top: 7px; font-size: 14px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
  a { color: ${({ theme }) => theme.color.brandAccessible}; text-decoration: underline; text-underline-offset: 3px; }
  .welcome { max-width: 65ch; margin: 20px auto; padding: 16px 0; } .welcome h3 { margin: 0 0 12px; font-size: 25px; font-weight: 700; letter-spacing: -.025em; } .welcome p { color: ${({ theme }) => theme.color.textMutedAccessible}; }
  article { max-width: 70ch; margin-left: auto; margin-right: auto; } article[data-role=user] { padding: 12px 16px; background: ${({ theme }) => theme.color.bg}; border-radius: ${({ theme }) => theme.radius.md}; }
`;

export function AiAnswer({ text, sources = [] }: { text: string; sources?: AiSource[] }) {
  return <>{text.split(/(\[(?:T:\d+|P:[\w-]+)\])/g).map((part, i) => {
    const source = sources.find(s => `[${s.reference}]` === part);
    return source ? <Link key={i} to={source.path} title={source.title}>{part}</Link> : part;
  })}</>;
}


export function AiAssistant() {
  const status = useAiStatus(); const location = useLocation(); const organization = useContext(OrganizationStateContext);
  const [open, setOpen] = useState(false); const [conversations, setConversations] = useState<Conversation[]>([]);
  const [id, setId] = useState<string | null>(null); const [messages, setMessages] = useState<Message[]>([]);
  const [text, setText] = useState(''); const [busy, setBusy] = useState(false); const [step, setStep] = useState(''); const [error, setError] = useState('');
  const [expanded, setExpanded] = useState(false); const [choice, setChoice] = useState<AiChoice>({ connectionId: null, model: null });
  const [historyVisible, setHistoryVisible] = useState(false);
  const [choiceDirty, setChoiceDirty] = useState(false); const [savingChoice, setSavingChoice] = useState(false); const [loadingConversation, setLoadingConversation] = useState(false);
  const abort = useRef<AbortController | null>(null); const launcher = useRef<HTMLButtonElement>(null); const input = useRef<HTMLTextAreaElement>(null); const feed = useRef<HTMLDivElement>(null);
  const panel = useRef<HTMLElement>(null); const tenantId = organization?.current.id ?? api.getOrganizationId();
  const currentTenant = useRef(tenantId); currentTenant.current = tenantId;
  const selection = useRef(0);
  const close = useCallback(() => { setOpen(false); launcher.current?.focus(); }, []);
  const refresh = useCallback(async () => { const requestedTenant = tenantId; const data = await api.request('/api/ai/conversations'); if (currentTenant.current === requestedTenant) setConversations(data); }, [tenantId]);
  useEffect(() => {
    abort.current?.abort(); selection.current++; setBusy(false); setMessages([]); setConversations([]); setId(null); setOpen(false); setChoice({ connectionId: null, model: null }); setChoiceDirty(false); setSavingChoice(false); setLoadingConversation(false);
    return () => { abort.current?.abort(); };
  }, [tenantId]);
  useEffect(() => {
    if (!open) return;
    void refresh().catch(e => setError(e.message)); input.current?.focus();
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close();
      if (event.key === 'Tab' && window.innerWidth <= 768 && panel.current) {
        const elements = [...panel.current.querySelectorAll<HTMLElement>('a[href],button:not([disabled]),textarea:not([disabled]),select:not([disabled])')].filter(element => element.getClientRects().length > 0);
        const first = elements[0], last = elements.at(-1);
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
      }
    };
    window.addEventListener('keydown', escape); return () => window.removeEventListener('keydown', escape);
  }, [open, refresh, close]);
  useEffect(() => { feed.current?.scrollTo?.({ top: feed.current.scrollHeight }); }, [messages, step]);
  useEffect(() => { if (!status.data?.enabled) { abort.current?.abort(); setOpen(false); } }, [status.data?.enabled]);
  if (!status.data?.enabled) return null;
  const select = async (value: string) => {
    const requestedTenant = tenantId; const version = ++selection.current;
    setError(''); setId(value || null); setChoiceDirty(false); setChoice({ connectionId: null, model: null });
    if (!value) { setMessages([]); return; }
    setLoadingConversation(true);
    try { const data = await api.request(`/api/ai/conversations/${value}`); if (currentTenant.current === requestedTenant && selection.current === version) { setMessages(data.messages.map((m: Message) => ({ ...m, sources: JSON.parse(m.sourcesJson ?? '[]') }))); setChoice({ connectionId: data.selectedConnectionId ?? null, model: data.selectedModel ?? null }); if (data.selectionAvailable === false) setError('A conexão desta conversa não está mais disponível. Escolha outra ou use o padrão da instalação.'); } }
    catch (e) { if (currentTenant.current === requestedTenant && selection.current === version) setError((e as Error).message); }
    finally { if (currentTenant.current === requestedTenant && selection.current === version) setLoadingConversation(false); }
  };
  const saveChoice = async (next: AiChoice) => {
    const requestedTenant = tenantId; const version = selection.current; setChoice(next); setChoiceDirty(true); setSavingChoice(true); setError('');
    const controller = new AbortController(); abort.current = controller;
    try {
      if (!id && !next.connectionId) { setChoiceDirty(false); return; }
      const conversation = id ?? (await api.request('/api/ai/conversations', { method: 'POST', signal: controller.signal })).id;
      if (controller.signal.aborted || currentTenant.current !== requestedTenant || selection.current !== version) return;
      setId(conversation);
      await api.request(`/api/ai/conversations/${conversation}/selection`, { method: 'PATCH', signal: controller.signal, body: JSON.stringify(next) });
      if (!controller.signal.aborted && currentTenant.current === requestedTenant && selection.current === version) { setChoiceDirty(false); await refresh(); }
    } catch (e) { if (!controller.signal.aborted && currentTenant.current === requestedTenant && selection.current === version) setError((e as Error).message); }
    finally { if (currentTenant.current === requestedTenant && selection.current === version) setSavingChoice(false); if (abort.current === controller) abort.current = null; }
  };
  const submit = async (event: FormEvent) => {
    event.preventDefault(); if (!text.trim() || busy || choiceDirty || savingChoice || loadingConversation) return;
    setBusy(true); setError(''); setStep('Preparando resposta'); const question = text.trim(); setText('');
    const controller = new AbortController(); abort.current = controller;
    try {
      const conversation = id ?? (await api.request('/api/ai/conversations', { method: 'POST' })).id;
      if (controller.signal.aborted) return;
      setId(conversation);
      setMessages(m => [...m, { role: 'user', content: question }, { role: 'assistant', content: '' }]);
      const projectId = location.pathname.match(/\/projects\/([\da-f-]{36})/i)?.[1];
      const boardId = location.pathname.match(/\/boards\/([\da-f-]{36})/i)?.[1];
      const workItemId = new URLSearchParams(location.search).get('item') ?? undefined;
      const response = await fetch(`${api.baseUrl}/api/ai/conversations/${conversation}/messages`, { method: 'POST', credentials: 'include', headers: api.headers(),
        body: JSON.stringify({ text: question, context: { projectId, boardId, workItemId } }), signal: controller.signal });
      await readAiEvents(response, (name, data) => {
        if (controller.signal.aborted) return;
        if (name === 'delta') setMessages(m => m.map((v, i) => i === m.length - 1 ? { ...v, content: v.content + String(data.text ?? '') } : v));
        if (name === 'sources') setMessages(m => m.map((v, i) => i === m.length - 1 ? { ...v, sources: data as unknown as AiSource[] } : v));
        if (name === 'step') setStep(String(data.message ?? 'Consultando'));
        if (name === 'error') throw new Error(String(data.detail ?? 'Não foi possível gerar a resposta.'));
      });
      await refresh(); void status.refetch();
    } catch (e) { if (!controller.signal.aborted) setError((e as Error).message); }
    finally { if (abort.current === controller) { setBusy(false); setStep(''); abort.current = null; } }
  };
  return <><Launcher ref={launcher} onClick={() => setOpen(v => !v)} aria-label="Abrir assistente Prisma" aria-expanded={open}>IA</Launcher>
    {open && <Panel $expanded={expanded} ref={panel} aria-label="Assistente Prisma"><header><h2>Assistente Prisma</h2><div className="header-actions"><button className="expand-toggle" aria-expanded={expanded} onClick={() => setExpanded(v => !v)}>{expanded ? 'Recolher' : 'Expandir'}</button><button onClick={close} aria-label="Fechar assistente">Fechar</button></div></header>
      <div className="chat-workspace"><nav aria-label="Histórico de conversas"><button className="history-toggle" aria-expanded={historyVisible} onClick={() => setHistoryVisible(v => !v)}>Histórico</button><div className="history-fields" data-collapsed={!historyVisible}><small>Seu histórico é privado.</small>
        <select aria-label="Conversa" value={id ?? ''} disabled={busy || savingChoice || loadingConversation} onChange={e => void select(e.target.value)}><option value="">Nova conversa</option>{conversations.map(c => <option key={c.id} value={c.id}>{c.title}</option>)}</select>
        <div className="history-actions"><button disabled={busy || savingChoice || loadingConversation} onClick={() => void select('')}>Nova conversa</button>
        {id && <><button disabled={busy || savingChoice || loadingConversation} onClick={async () => { const title = window.prompt('Título da conversa'); if (!title?.trim()) return; try { await api.request(`/api/ai/conversations/${id}`, { method: 'PATCH', body: JSON.stringify({ title }) }); await refresh(); } catch (e) { setError((e as Error).message); } }}>Renomear</button>
          <button disabled={busy || savingChoice || loadingConversation} onClick={async () => { if (!window.confirm('Excluir esta conversa e suas mensagens?')) return; try { await api.request(`/api/ai/conversations/${id}`, { method: 'DELETE' }); await select(''); await refresh(); } catch (e) { setError((e as Error).message); } }}>Excluir</button></>}
        </div></div></nav><div className="conversation-main"><p className="context">Contexto: {location.pathname.includes('/projects/') ? 'projeto atual' : location.pathname.includes('/boards/') ? 'quadro atual' : 'sua organização'}. Somente dados que você pode acessar.</p>
      <Feed ref={feed}>{loadingConversation ? <p role="status">Carregando conversa…</p> : messages.length === 0 && <div className="welcome"><h3>Converse sobre seu trabalho.</h3><p>Consulte projetos, tarefas e prazos com o contexto desta tela.</p><p>Experimente: “O que está atrasado neste projeto?”</p></div>}
        {messages.map((m, i) => <article key={i} data-role={m.role}><strong>{m.role === 'user' ? 'Você' : 'Prisma'}</strong><p><AiAnswer text={m.content} sources={m.sources}/></p></article>)}
        {busy && <p role="status">{step}</p>}{error && <p role="alert">{error}</p>}</Feed>
      <form onSubmit={submit}><label htmlFor="ai-question">Pergunte ao workspace</label><div className="compose-row"><textarea id="ai-question" ref={input} value={text} disabled={busy || savingChoice || loadingConversation} maxLength={8000} placeholder="O que precisa da sua atenção?" onChange={e => setText(e.target.value)}/>
        {busy ? <button type="button" onClick={() => { abort.current?.abort(); if (id) void api.request(`/api/ai/conversations/${id}/messages/current`, { method: 'DELETE' }).catch(() => {}); }}>Interromper</button> : <button disabled={!text.trim() || choiceDirty || savingChoice || loadingConversation}>Enviar pergunta</button>}</div>
        <AiConversationControls tenantId={tenantId} choice={choice} dirty={choiceDirty} disabled={busy || savingChoice || loadingConversation}
          onConnection={value => { if (!value) void saveChoice({ connectionId: null, model: null }); else { setChoice({ connectionId: value, model: null }); setChoiceDirty(true); setError(''); } }}
          onModel={value => { if (value) void saveChoice({ ...choice, model: value }); }}/>
        {savingChoice && <small role="status">Salvando assistente da conversa…</small>}
        <small>{status.data.remainingTokens === null ? 'Consumo medido pela instalação.' : `${status.data.remainingTokens} tokens disponíveis hoje.`}</small>
      </form></div></div>
    </Panel>}</>;
}
