import { useCallback, useContext, useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useLocation } from 'react-router-dom';
import styled from 'styled-components';
import { api } from '../../services/api';
import { OrganizationStateContext } from '../organizations/OrganizationState';
import { useAiStatus, readAiEvents, type AiSource } from './aiClient';

type Conversation = { id: string; title: string };
type Message = { role: string; content: string; sourcesJson?: string; sources?: AiSource[] };


const Launcher = styled.button`
  position: fixed; right: 22px; bottom: 22px; z-index: 80; min-width: 54px; min-height: 48px;
  padding: 10px 18px; border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.brandControlBackground}; color: white; font-weight: 750;
  &:focus-visible { outline: 3px solid ${({ theme }) => theme.color.accentBlueAccessible}; outline-offset: 3px; }
`;
const Panel = styled.aside`
  position: fixed; right: 16px; top: 88px; bottom: 84px; z-index: 85; width: min(430px, calc(100vw - 32px));
  display: flex; flex-direction: column; border-radius: ${({ theme }) => theme.radius.lg};
  box-shadow: ${({ theme }) => theme.shadow.lg}; background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
  header { padding: 18px; display: flex; align-items: center; justify-content: space-between; gap: 12px; }
  h2 { font-size: 18px; margin: 0; } button { min-height: 42px; padding: 7px 12px; border-radius: ${({ theme }) => theme.radius.md}; }
  button:focus-visible, select:focus-visible, textarea:focus-visible { outline: 2px solid ${({ theme }) => theme.color.brandAccessible}; outline-offset: 2px; }
  button:disabled { opacity: .55; cursor: default; }
  nav { display: flex; gap: 6px; padding: 0 16px 12px; flex-wrap: wrap; border-bottom: 1px solid ${({ theme }) => theme.color.border}; }
  select { width: 100%; min-height: 42px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; padding: 6px; background: ${({ theme }) => theme.color.surface}; }
  form { display: grid; gap: 10px; padding: 16px; border-top: 1px solid ${({ theme }) => theme.color.border}; }
  textarea { resize: vertical; min-height: 72px; max-height: 160px; padding: 10px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; }
  form button { background: ${({ theme }) => theme.color.brandControlBackground}; color: white; justify-self: end; }
  small { color: ${({ theme }) => theme.color.textMutedAccessible}; font-size: 12px; }
  @media(max-width: 768px) { inset: 0; width: 100%; border-radius: 0; height: 100dvh; }
`;
const Feed = styled.div`
  flex: 1; overflow-y: auto; overscroll-behavior: contain; padding: 16px;
  article { margin-bottom: 22px; } strong { font-size: 12px; color: ${({ theme }) => theme.color.textMutedAccessible}; }
  p { margin-top: 7px; font-size: 14px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
  a { color: ${({ theme }) => theme.color.brandAccessible}; text-decoration: underline; text-underline-offset: 3px; }
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
  const abort = useRef<AbortController | null>(null); const launcher = useRef<HTMLButtonElement>(null); const input = useRef<HTMLTextAreaElement>(null); const feed = useRef<HTMLDivElement>(null);
  const panel = useRef<HTMLElement>(null); const tenantId = organization?.current.id ?? api.getOrganizationId();
  const currentTenant = useRef(tenantId); currentTenant.current = tenantId;
  const selection = useRef(0);
  const close = useCallback(() => { setOpen(false); launcher.current?.focus(); }, []);
  const refresh = useCallback(async () => { const requestedTenant = tenantId; const data = await api.request('/api/ai/conversations'); if (currentTenant.current === requestedTenant) setConversations(data); }, [tenantId]);
  useEffect(() => {
    abort.current?.abort(); selection.current++; setBusy(false); setMessages([]); setConversations([]); setId(null); setOpen(false);
    return () => { abort.current?.abort(); };
  }, [tenantId]);
  useEffect(() => {
    if (!open) return;
    void refresh().catch(e => setError(e.message)); input.current?.focus();
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close();
      if (event.key === 'Tab' && window.innerWidth <= 768 && panel.current) {
        const elements = [...panel.current.querySelectorAll<HTMLElement>('a[href],button:not([disabled]),textarea:not([disabled]),select:not([disabled])')];
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
    setError(''); setId(value || null);
    if (!value) { setMessages([]); return; }
    try { const data = await api.request(`/api/ai/conversations/${value}`); if (currentTenant.current === requestedTenant && selection.current === version) setMessages(data.messages.map((m: Message) => ({ ...m, sources: JSON.parse(m.sourcesJson ?? '[]') }))); }
    catch (e) { if (currentTenant.current === requestedTenant && selection.current === version) setError((e as Error).message); }
  };
  const submit = async (event: FormEvent) => {
    event.preventDefault(); if (!text.trim() || busy) return;
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
    {open && <Panel ref={panel} aria-label="Assistente Prisma"><header><h2>Assistente Prisma</h2><button onClick={close} aria-label="Fechar assistente">Fechar</button></header>
      <nav><small>Seu histórico é privado. Consulte projetos, tarefas e prazos.</small>
        <select aria-label="Conversa" value={id ?? ''} disabled={busy} onChange={e => void select(e.target.value)}><option value="">Nova conversa</option>{conversations.map(c => <option key={c.id} value={c.id}>{c.title}</option>)}</select>
        <button disabled={busy} onClick={() => void select('')}>Nova conversa</button>
        {id && <><button disabled={busy} onClick={async () => { const title = window.prompt('Título da conversa'); if (!title?.trim()) return; try { await api.request(`/api/ai/conversations/${id}`, { method: 'PATCH', body: JSON.stringify({ title }) }); await refresh(); } catch (e) { setError((e as Error).message); } }}>Renomear</button>
          <button disabled={busy} onClick={async () => { if (!window.confirm('Excluir esta conversa e suas mensagens?')) return; try { await api.request(`/api/ai/conversations/${id}`, { method: 'DELETE' }); await select(''); await refresh(); } catch (e) { setError((e as Error).message); } }}>Excluir</button></>}
      </nav>
      <Feed ref={feed}>{messages.length === 0 && <p>O que você quer saber?<br/>Experimente: “O que está atrasado neste projeto?”</p>}
        {messages.map((m, i) => <article key={i}><strong>{m.role === 'user' ? 'Você' : 'Prisma'}</strong><p><AiAnswer text={m.content} sources={m.sources}/></p></article>)}
        {busy && <p role="status">{step}</p>}{error && <p role="alert">{error}</p>}</Feed>
      <form onSubmit={submit}><label htmlFor="ai-question">Pergunte ao workspace</label><textarea id="ai-question" ref={input} value={text} disabled={busy} maxLength={8000} onChange={e => setText(e.target.value)}/>
        <small>{status.data.remainingTokens === null ? 'Consumo medido pela instalação.' : `${status.data.remainingTokens} tokens disponíveis hoje.`}</small>
        {busy ? <button type="button" onClick={() => { abort.current?.abort(); if (id) void api.request(`/api/ai/conversations/${id}/messages/current`, { method: 'DELETE' }).catch(() => {}); }}>Interromper</button> : <button disabled={!text.trim()}>Enviar pergunta</button>}
      </form>
    </Panel>}</>;
}
