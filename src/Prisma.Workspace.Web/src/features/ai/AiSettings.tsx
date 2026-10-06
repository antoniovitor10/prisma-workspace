import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import styled from 'styled-components';
import { api } from '../../services/api';
import { useAiStatus } from './aiClient';

type Connection = { id: string; name: string; type: string; provider: string; baseUrl: string | null; model: string; inputPrice: number | null; outputPrice: number | null; isActive: boolean; testSucceeded: boolean; testMessage: string | null; hasSecret: boolean; secretSuffix: string | null; latencyMs: number | null };
type Usage = { key: string; calls: number; inputTokens: number; outputTokens: number; estimatedCost: number | null };
const Section = styled.section`
  margin: 22px 0; padding: 22px; border-top: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
  h2 { font-size: 19px; margin-bottom: 10px; } h3 { font-size: 16px; margin: 26px 0 12px; }
  p, small { font-size: 13px; line-height: 1.6; color: ${({ theme }) => theme.color.textMutedAccessible}; }
  form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; margin: 16px 0; }
  label { display: grid; gap: 6px; font-size: 13px; } input, select { min-width: 0; min-height: 42px; padding: 8px 10px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.surface}; }
  button { min-height: 42px; padding: 8px 13px; border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.brandControlBackground}; color: white; }
  button:disabled { opacity: .55; } input:focus-visible, select:focus-visible, button:focus-visible { outline: 2px solid ${({ theme }) => theme.color.brandAccessible}; outline-offset: 2px; }
  .wide { grid-column: 1/-1; } .actions { display: flex; flex-wrap: wrap; gap: 8px; }
  .connection { border-bottom: 1px solid ${({ theme }) => theme.color.border}; padding: 16px 0; display: grid; gap: 10px; }
  table { width: 100%; border-collapse: collapse; font-size: 13px; margin-top: 14px; } th, td { text-align: left; padding: 8px 6px; overflow-wrap: anywhere; font-variant-numeric: tabular-nums; }
  @media(max-width: 640px) { padding: 16px 8px; form { grid-template-columns: 1fr; } table { font-size: 11px; } }
`;
const initial = { name: '', type: 'OpenAiCompatible', provider: 'Custom', baseUrl: '', model: '', secret: '', inputPrice: '', outputPrice: '' };

export function AiSettings() {
  const status = useAiStatus(); const cache = useQueryClient(); const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [busy, setBusy] = useState(false);
  const [form, setForm] = useState(initial); const [editId, setEditId] = useState<string | null>(null); const [groupBy, setGroupBy] = useState('day');
  const administrator = status.data?.isPlatformAdministrator === true; const orgAdmin = status.data?.canAdministerOrganization === true;
  const connections = useQuery<Connection[]>({ queryKey: ['ai-connections'], queryFn: () => api.request('/api/admin/ai/connections'), enabled: administrator });
  const organization = useQuery<{ enabled: boolean; dailyTokenLimit: number | null; userTokenLimit: number | null }>({ queryKey: ['ai-org', api.getOrganizationId()], queryFn: () => api.request('/api/organizations/current/ai'), enabled: orgAdmin });
  const settings = useQuery<{ dailyTokenLimit: number; organizationTokenLimit: number; userTokenLimit: number; timeZone: string }>({ queryKey: ['ai-installation'], queryFn: () => api.request('/api/admin/ai/settings'), enabled: administrator });
  const usage = useQuery<Usage[]>({ queryKey: ['ai-usage', api.getOrganizationId(), groupBy, administrator], queryFn: () => api.request(`${administrator ? '/api/admin/ai' : '/api/organizations/current/ai'}/usage?groupBy=${groupBy}`), enabled: administrator || orgAdmin });
  const [orgForm, setOrgForm] = useState({ enabled: false, dailyTokenLimit: '', userTokenLimit: '' });
  const [limitForm, setLimitForm] = useState({ dailyTokenLimit: '0', organizationTokenLimit: '0', userTokenLimit: '0', timeZone: 'America/Sao_Paulo' });
  useEffect(() => { if (organization.data) setOrgForm({ enabled: organization.data.enabled, dailyTokenLimit: organization.data.dailyTokenLimit?.toString() ?? '', userTokenLimit: organization.data.userTokenLimit?.toString() ?? '' }); }, [organization.data]);
  useEffect(() => { if (settings.data) setLimitForm({ dailyTokenLimit: String(settings.data.dailyTokenLimit), organizationTokenLimit: String(settings.data.organizationTokenLimit), userTokenLimit: String(settings.data.userTokenLimit), timeZone: settings.data.timeZone }); }, [settings.data]);
  const action = useCallback(async (run: () => Promise<unknown>) => {
    setBusy(true); setError(''); setNotice('');
    try { await run(); await cache.invalidateQueries({ predicate: q => String(q.queryKey[0]).startsWith('ai-') }); setNotice('Alteração salva.'); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }, [cache]);
  useEffect(() => {
    const query = new URLSearchParams(window.location.search); const code = query.get('code'); const state = query.get('aiState'); const id = query.get('aiConnection');
    if (!administrator || !code || !state || !id) return;
    window.history.replaceState({}, '', window.location.pathname);
    void action(() => api.request(`/api/admin/ai/connections/${id}/oauth/complete`, { method: 'POST', body: JSON.stringify({ code, state }) }));
  }, [administrator, action]);
  if (!administrator && !orgAdmin) return null;
  const submit = (event: FormEvent) => {
    event.preventDefault(); void action(async () => {
      await api.request(`/api/admin/ai/connections${editId ? '/' + editId : ''}`, { method: editId ? 'PUT' : 'POST', body: JSON.stringify({ ...form, inputPrice: form.inputPrice ? Number(form.inputPrice) : null, outputPrice: form.outputPrice ? Number(form.outputPrice) : null }) });
      setForm(initial); setEditId(null);
    });
  };
  return <Section aria-label="Configurações de IA"><h2>Assistente de IA</h2><p>Conecte um modelo da instalação e escolha se esta organização pode usá-lo. O chat consulta dados com as permissões de cada pessoa.</p>
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {orgAdmin && <form onSubmit={e => { e.preventDefault(); void action(() => api.request('/api/organizations/current/ai', { method: 'PUT', body: JSON.stringify({ enabled: orgForm.enabled, dailyTokenLimit: orgForm.dailyTokenLimit === '' ? null : Number(orgForm.dailyTokenLimit), userTokenLimit: orgForm.userTokenLimit === '' ? null : Number(orgForm.userTokenLimit) }) })); }}>
      <label className="wide"><span><input type="checkbox" checked={orgForm.enabled} onChange={e => setOrgForm(v => ({ ...v, enabled: e.target.checked }))}/> Habilitar IA nesta organização</span></label>
      <label>Cota diária da organização<input type="number" min="0" value={orgForm.dailyTokenLimit} placeholder="Usar padrão da instalação" onChange={e => setOrgForm(v => ({ ...v, dailyTokenLimit: e.target.value }))}/></label>
      <label>Cota diária por usuário<input type="number" min="0" value={orgForm.userTokenLimit} placeholder="Usar padrão da instalação" onChange={e => setOrgForm(v => ({ ...v, userTokenLimit: e.target.value }))}/></label>
      <small className="wide">Em tokens. Campo vazio usa o padrão da instalação; zero significa sem limite.</small><button disabled={busy}>Salvar IA da organização</button>
    </form>}
    {administrator && <><h3>Conexões da instalação</h3>{connections.data?.map(c => <div className="connection" key={c.id}><strong>{c.name}{c.isActive ? ' — ativa' : ''}</strong>
      <p>{c.provider} · {c.model}{c.type === 'CliSubscription' ? ' · Experimental' : ''}{c.hasSecret ? ` · credencial salva${c.secretSuffix ? ' (final ' + c.secretSuffix + ')' : ''}` : ''}</p>
      {c.testMessage && <p>{c.testMessage}{c.latencyMs !== null ? ` (${c.latencyMs} ms)` : ''}</p>}
      <div className="actions"><button disabled={busy} onClick={() => void action(() => api.request(`/api/admin/ai/connections/${c.id}/test`, { method: 'POST' }))}>Testar {c.name}</button>
        <button disabled={busy || !c.testSucceeded || c.isActive} onClick={() => void action(() => api.request(`/api/admin/ai/connections/${c.id}/activate`, { method: 'POST' }))}>Ativar {c.name}</button>
        <button disabled={busy} onClick={() => { setEditId(c.id); setForm({ name: c.name, type: c.type, provider: c.provider, baseUrl: c.baseUrl ?? '', model: c.model, secret: '', inputPrice: c.inputPrice?.toString() ?? '', outputPrice: c.outputPrice?.toString() ?? '' }); }}>Editar {c.name}</button>
        {c.type === 'OAuth' && <button disabled={busy} onClick={() => void action(async () => { const result = await api.request(`/api/admin/ai/connections/${c.id}/oauth/start`, { method: 'POST' }); window.location.assign(result.url); })}>Conectar com OpenRouter</button>}
        <button disabled={busy} onClick={() => { if (window.confirm(`Excluir conexão ${c.name}?`)) void action(() => api.request(`/api/admin/ai/connections/${c.id}`, { method: 'DELETE' })); }}>Excluir {c.name}</button></div></div>)}
      <form onSubmit={submit} aria-label="Cadastro de conexão IA">
        <label>Nome da conexão<input required maxLength={160} value={form.name} onChange={e => setForm(v => ({ ...v, name: e.target.value }))}/></label>
        <label>Tipo de conexão<select value={form.type} onChange={e => setForm(v => ({ ...v, type: e.target.value, provider: e.target.value === 'OAuth' ? 'OpenRouter' : v.provider }))}><option value="OpenAiCompatible">Endpoint compatível com OpenAI</option><option value="ApiKey">Chave de API</option><option value="OAuth">Login OpenRouter</option><option value="CliSubscription">Assinatura via CLI (Experimental)</option></select></label>
        <label>Provedor<select value={form.provider} disabled={form.type === 'OAuth'} onChange={e => setForm(v => ({ ...v, provider: e.target.value }))}>{['Custom', 'OpenAI', 'Anthropic', 'Gemini', 'OpenRouter'].map(p => <option key={p}>{p}</option>)}</select></label>
        <label>Modelo<input required maxLength={200} value={form.model} onChange={e => setForm(v => ({ ...v, model: e.target.value }))}/></label>
        {form.type !== 'CliSubscription' && <label className="wide">URL base<input type="url" required={form.type === 'OpenAiCompatible' || form.provider === 'Custom'} placeholder="http://localhost:11434/v1" value={form.baseUrl} onChange={e => setForm(v => ({ ...v, baseUrl: e.target.value }))}/></label>}
        {form.type !== 'OAuth' && form.type !== 'CliSubscription' && <><label className="wide">Chave de API<input type="password" autoComplete="new-password" value={form.secret} onChange={e => setForm(v => ({ ...v, secret: e.target.value }))}/></label><small className="wide">Ao editar, deixe vazio para preservar a credencial atual.</small></>}
        <label>Preço por milhão de tokens de entrada<input type="number" min="0" step="any" value={form.inputPrice} onChange={e => setForm(v => ({ ...v, inputPrice: e.target.value }))}/></label>
        <label>Preço por milhão de tokens de saída<input type="number" min="0" step="any" value={form.outputPrice} onChange={e => setForm(v => ({ ...v, outputPrice: e.target.value }))}/></label>
        {form.type === 'CliSubscription' && <p className="wide">Experimental. Assinaturas de Codex, Claude e Antigravity são de uso pessoal; usar uma conta para vários usuários pode violar os termos do provedor. Exige a ponte opcional configurada pelo instalador.</p>}
        <div className="wide actions"><button disabled={busy}>{editId ? 'Salvar conexão' : 'Cadastrar conexão'}</button>{editId && <button type="button" onClick={() => { setEditId(null); setForm(initial); }}>Cancelar edição</button>}</div>
      </form>
      <h3>Limites da instalação</h3><form onSubmit={e => { e.preventDefault(); void action(() => api.request('/api/admin/ai/settings', { method: 'PUT', body: JSON.stringify({ ...limitForm, dailyTokenLimit: Number(limitForm.dailyTokenLimit), organizationTokenLimit: Number(limitForm.organizationTokenLimit), userTokenLimit: Number(limitForm.userTokenLimit) }) })); }}>
        {(['dailyTokenLimit', 'organizationTokenLimit', 'userTokenLimit'] as const).map((key, i) => <label key={key}>{['Teto diário da instalação', 'Cota padrão por organização', 'Cota padrão por usuário'][i]}<input type="number" min="0" required value={limitForm[key]} onChange={e => setLimitForm(v => ({ ...v, [key]: e.target.value }))}/></label>)}
        <label>Fuso das cotas<input required value={limitForm.timeZone} onChange={e => setLimitForm(v => ({ ...v, timeZone: e.target.value }))}/></label><small className="wide">Limites em tokens; zero significa sem limite. As cotas renovam à meia-noite neste fuso.</small><button disabled={busy}>Salvar limites da instalação</button>
      </form></>}
    <h3>Consumo dos últimos 30 dias</h3><label>Agrupar por<select value={groupBy} onChange={e => setGroupBy(e.target.value)}><option value="day">Dia</option><option value="user">Usuário</option>{administrator && <><option value="organization">Organização</option><option value="connection">Conexão</option></>}</select></label>
    {usage.data?.length ? <table><thead><tr><th>Grupo</th><th>Chamadas</th><th>Tokens entrada / saída</th><th>Custo estimado</th></tr></thead><tbody>{usage.data.map(row => <tr key={row.key}><td>{row.key}</td><td>{row.calls}</td><td>{row.inputTokens} / {row.outputTokens}</td><td>{row.estimatedCost?.toFixed(6) ?? 'Não informado'}</td></tr>)}</tbody></table> : <p>Nenhuma chamada registrada neste período.</p>}
    <small>Este painel mostra consumo, sem conteúdo das conversas. Os preços são os informados na conexão.</small>
  </Section>;
}
