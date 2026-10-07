import { useCallback, useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import styled from 'styled-components';
import { api } from '../../services/api';
import { useAiStatus } from './aiClient';
import { AiConnectionForm, type AiConnection } from './AiConnectionForm';

type Usage = { key: string; calls: number; inputTokens: number; outputTokens: number; estimatedCost: number | null };
const Section = styled.section`
  margin: 22px 0; padding: 24px; border-top: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
  h2 { font-size: 20px; margin: 0 0 8px; } h3 { font-size: 17px; margin: 0; } h4 { font-size: 14px; margin: 0; }
  p, small { font-size: 13px; line-height: 1.6; color: ${({ theme }) => theme.color.textMutedAccessible}; }
  p { margin: 8px 0; max-width: 75ch; } small { display: block; } strong { overflow-wrap: anywhere; }
  form, .fields { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; margin: 20px 0; }
  form > h3 { margin-bottom: 4px; } label { display: grid; gap: 6px; font-size: 13px; }
  input, select { width: 100%; min-width: 0; min-height: 44px; padding: 10px 12px; color: ${({ theme }) => theme.color.text}; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.surface}; }
  input::placeholder { color: ${({ theme }) => theme.color.textMutedAccessible}; } input { caret-color: ${({ theme }) => theme.color.brandAccessible}; }
  button, .login-link { min-height: 44px; padding: 10px 16px; border: 1px solid transparent; border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.brandControlBackground}; color: white; cursor: pointer; font-size: 14px; }
  button:hover:not(:disabled) { filter: brightness(.94); } button:disabled { opacity: .55; cursor: not-allowed; }
  button.secondary { background: transparent; color: ${({ theme }) => theme.color.text}; border-color: ${({ theme }) => theme.color.border}; }
  input:focus-visible, select:focus-visible, button:focus-visible, a:focus-visible, summary:focus-visible { outline: 2px solid ${({ theme }) => theme.color.brandAccessible}; outline-offset: 3px; }
  .wide { grid-column: 1 / -1; } .actions { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
  .section-heading, .connection-heading, .catalog-heading { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; }
  .section-heading { margin: 28px 0 12px; } .connection { border-bottom: 1px solid ${({ theme }) => theme.color.border}; padding: 18px 0; }
  .connection p { margin: 5px 0 12px; } .connection-state { font-size: 12px; color: ${({ theme }) => theme.color.textMutedAccessible}; }
  .check { display: flex; align-items: flex-start; gap: 10px; line-height: 1.6; } .check input { width: 18px; min-height: 18px; margin: 2px 0 0; flex-shrink: 0; accent-color: ${({ theme }) => theme.color.brandControlBackground}; }
  details { margin: 16px 0; } summary { cursor: pointer; padding: 12px 0; font-size: 14px; font-weight: 600; min-height: 44px; }
  .advanced { border-top: 1px solid ${({ theme }) => theme.color.border}; margin: 0; } .advanced .fields { margin: 8px 0; }
  .more-actions { margin: 8px 0 0; } .more-actions summary { font-size: 13px; font-weight: 400; }
  .cli-login { padding: 18px 0; border-top: 1px solid ${({ theme }) => theme.color.border}; border-bottom: 1px solid ${({ theme }) => theme.color.border}; }
  .cli-login .check { margin: 16px 0; } .cli-login label:not(.check) { max-width: 560px; margin: 14px 0; } .login-link { display: inline-block; margin: 8px 12px 8px 0; text-decoration: underline; text-underline-offset: 3px; }
  .model-selection { display: grid; gap: 8px; } .model-selection p { margin: 0; } .method { align-self: end; padding: 10px 0; }
  .table-scroll { overflow-x: auto; } table { width: 100%; border-collapse: collapse; font-size: 13px; margin-top: 14px; } th, td { text-align: left; padding: 10px 8px; overflow-wrap: anywhere; font-variant-numeric: tabular-nums; }
  [role=alert] { color: ${({ theme }) => theme.color.text}; font-weight: 600; }
  @media(max-width: 640px) { padding: 20px 8px; form, .fields { grid-template-columns: 1fr; } .catalog-heading button { width: auto; } .actions button { flex: 1 1 auto; } table { font-size: 12px; } }
`;

export function AiSettings() {
  const status = useAiStatus(); const cache = useQueryClient(); const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<AiConnection | null>(null); const [adding, setAdding] = useState(false); const [groupBy, setGroupBy] = useState('day');
  const administrator = status.data?.isPlatformAdministrator === true; const orgAdmin = status.data?.canAdministerOrganization === true;
  const connections = useQuery<AiConnection[]>({ queryKey: ['ai-connections'], queryFn: () => api.request('/api/admin/ai/connections'), enabled: administrator });
  const organization = useQuery<{ enabled: boolean; dailyTokenLimit: number | null; userTokenLimit: number | null }>({ queryKey: ['ai-org', api.getOrganizationId()], queryFn: () => api.request('/api/organizations/current/ai'), enabled: orgAdmin });
  const settings = useQuery<{ dailyTokenLimit: number; organizationTokenLimit: number; userTokenLimit: number; timeZone: string }>({ queryKey: ['ai-installation'], queryFn: () => api.request('/api/admin/ai/settings'), enabled: administrator });
  const usage = useQuery<Usage[]>({ queryKey: ['ai-usage', api.getOrganizationId(), groupBy, administrator], queryFn: () => api.request(`${administrator ? '/api/admin/ai' : '/api/organizations/current/ai'}/usage?groupBy=${groupBy}`), enabled: administrator || orgAdmin });
  const [orgForm, setOrgForm] = useState({ enabled: false, dailyTokenLimit: '', userTokenLimit: '' });
  const [limitForm, setLimitForm] = useState({ dailyTokenLimit: '0', organizationTokenLimit: '0', userTokenLimit: '0', timeZone: 'America/Sao_Paulo' });
  useEffect(() => { if (organization.data) setOrgForm({ enabled: organization.data.enabled, dailyTokenLimit: organization.data.dailyTokenLimit?.toString() ?? '', userTokenLimit: organization.data.userTokenLimit?.toString() ?? '' }); }, [organization.data]);
  useEffect(() => { if (settings.data) setLimitForm({ dailyTokenLimit: String(settings.data.dailyTokenLimit), organizationTokenLimit: String(settings.data.organizationTokenLimit), userTokenLimit: String(settings.data.userTokenLimit), timeZone: settings.data.timeZone }); }, [settings.data]);
  const refresh = useCallback(async () => { await cache.invalidateQueries({ predicate: q => String(q.queryKey[0]).startsWith('ai-') }); }, [cache]);
  const action = useCallback(async (run: () => Promise<unknown>) => {
    setBusy(true); setError(''); setNotice('');
    try { await run(); await refresh(); setNotice('Alteração salva.'); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }, [refresh]);
  useEffect(() => {
    const query = new URLSearchParams(window.location.search); const code = query.get('code'); const state = query.get('aiState'); const id = query.get('aiConnection');
    if (!administrator || !code || !state || !id) return;
    window.history.replaceState({}, '', window.location.pathname);
    void action(() => api.request(`/api/admin/ai/connections/${id}/oauth/complete`, { method: 'POST', body: JSON.stringify({ code, state }) }));
  }, [administrator, action]);
  if (!administrator && !orgAdmin) return null;
  const showForm = adding || editing !== null || connections.data?.length === 0;
  const connectionState = (c: AiConnection) => c.type === 'CliSubscription' && !['OpenAI', 'Anthropic', 'Gemini'].includes(c.provider) ? 'Configuração inválida — edite a conexão' : c.isActive ? 'Ativa' : c.testSucceeded ? 'Pronta para ativar' : c.testMessage ? 'Teste falhou' : c.type === 'OAuth' && !c.hasSecret ? 'Sem autenticação' : c.type === 'CliSubscription' ? 'Confira o login ao editar' : 'Pronta para testar';
  return <Section aria-label="Configurações de IA"><h2>Assistente de IA</h2><p>Conecte um provedor, escolha o modelo e teste. Depois, habilite o assistente nesta organização.</p>
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {administrator && <><div className="section-heading"><h3>Conexões da instalação</h3>{!showForm && <button onClick={() => setAdding(true)}>Adicionar conexão</button>}</div>
      {connections.isPending && <p role="status">Carregando conexões…</p>}{connections.isError && <p role="alert">Não foi possível carregar as conexões. <button className="secondary" onClick={() => void connections.refetch()}>Tentar novamente</button></p>}
      {connections.data?.length === 0 && <p>Nenhum provedor conectado. Configure abaixo para começar.</p>}
      {connections.data?.map(c => <div className="connection" key={c.id}><div className="connection-heading"><strong>{c.name}{c.isActive ? ' — ativa' : ''}</strong><span className="connection-state">{connectionState(c)}</span></div>
        <p>{c.provider} · {c.model}{c.type === 'CliSubscription' ? ' · Experimental' : ''}{c.hasSecret ? ` · credencial salva${c.secretSuffix ? ' (final ' + c.secretSuffix + ')' : ''}` : ''}</p>
        {c.testMessage && <p>{c.testMessage}{c.latencyMs !== null ? ` (${c.latencyMs} ms)` : ''}</p>}
        <div className="actions">{c.type === 'OAuth' && !c.hasSecret ? <button disabled={busy} onClick={() => void action(async () => { const result = await api.request(`/api/admin/ai/connections/${c.id}/oauth/start`, { method: 'POST' }); window.location.assign(result.url); })}>Entrar com OpenRouter</button> : !c.isActive && <button disabled={busy} onClick={() => void action(() => api.request(`/api/admin/ai/connections/${c.id}/${c.testSucceeded ? 'activate' : 'test'}`, { method: 'POST' }))}>{c.testSucceeded ? 'Ativar' : 'Testar'} {c.name}</button>}
          <button className="secondary" disabled={busy} onClick={() => { setAdding(false); setEditing(c); }}>Editar {c.name}</button></div>
        <details className="more-actions"><summary>Mais ações de {c.name}</summary><div className="actions"><button className="secondary" disabled={busy} onClick={() => void action(() => api.request(`/api/admin/ai/connections/${c.id}/test`, { method: 'POST' }))}>Testar novamente {c.name}</button>
          {c.type === 'OAuth' && c.hasSecret && <button className="secondary" disabled={busy} onClick={() => void action(async () => { const auth = await api.request(`/api/admin/ai/connections/${c.id}/oauth/start`, { method: 'POST' }); window.location.assign(auth.url); })}>Autenticar novamente no OpenRouter</button>}
          <button className="secondary" disabled={busy} onClick={() => { if (window.confirm(`Excluir conexão ${c.name}?`)) void action(() => api.request(`/api/admin/ai/connections/${c.id}`, { method: 'DELETE' })); }}>Excluir {c.name}</button></div></details>
      </div>)}
      {showForm && <AiConnectionForm key={editing?.id ?? 'new'} connection={editing ?? undefined} onAuthenticationStarted={refresh} onCancel={() => { setAdding(false); setEditing(null); }} onSaved={async () => { setAdding(false); setEditing(null); await refresh(); setNotice('Conexão salva. Teste antes de ativar.'); }}/>}</>}
    {orgAdmin && <><div className="section-heading"><h3>IA nesta organização</h3></div><form aria-label="IA da organização" onSubmit={e => { e.preventDefault(); void action(() => api.request('/api/organizations/current/ai', { method: 'PUT', body: JSON.stringify({ enabled: orgForm.enabled, dailyTokenLimit: orgForm.dailyTokenLimit === '' ? null : Number(orgForm.dailyTokenLimit), userTokenLimit: orgForm.userTokenLimit === '' ? null : Number(orgForm.userTokenLimit) }) })); }}>
      <label className="wide check"><input type="checkbox" checked={orgForm.enabled} onChange={e => setOrgForm(v => ({ ...v, enabled: e.target.checked }))}/><span>Habilitar IA nesta organização</span></label>
      <details className="wide advanced"><summary>Cotas desta organização</summary><div className="fields"><label>Cota diária da organização<input type="number" min="0" value={orgForm.dailyTokenLimit} placeholder="Usar padrão da instalação" onChange={e => setOrgForm(v => ({ ...v, dailyTokenLimit: e.target.value }))}/></label><label>Cota diária por usuário<input type="number" min="0" value={orgForm.userTokenLimit} placeholder="Usar padrão da instalação" onChange={e => setOrgForm(v => ({ ...v, userTokenLimit: e.target.value }))}/></label><small className="wide">Em tokens. Campo vazio usa o padrão da instalação; zero significa sem limite.</small></div></details>
      <div className="wide actions"><button disabled={busy}>Salvar IA da organização</button></div>
    </form></>}
    {administrator && <details className="advanced"><summary>Limites da instalação</summary><form onSubmit={e => { e.preventDefault(); void action(() => api.request('/api/admin/ai/settings', { method: 'PUT', body: JSON.stringify({ ...limitForm, dailyTokenLimit: Number(limitForm.dailyTokenLimit), organizationTokenLimit: Number(limitForm.organizationTokenLimit), userTokenLimit: Number(limitForm.userTokenLimit) }) })); }}>
      {(['dailyTokenLimit', 'organizationTokenLimit', 'userTokenLimit'] as const).map((key, i) => <label key={key}>{['Teto diário da instalação', 'Cota padrão por organização', 'Cota padrão por usuário'][i]}<input type="number" min="0" required value={limitForm[key]} onChange={e => setLimitForm(v => ({ ...v, [key]: e.target.value }))}/></label>)}
      <label>Fuso das cotas<input required value={limitForm.timeZone} onChange={e => setLimitForm(v => ({ ...v, timeZone: e.target.value }))}/></label><small className="wide">Limites em tokens; zero significa sem limite. Renovação à meia-noite neste fuso.</small><div className="wide actions"><button disabled={busy}>Salvar limites da instalação</button></div>
    </form></details>}
    <details className="advanced"><summary>Consumo dos últimos 30 dias</summary><label>Agrupar por<select value={groupBy} onChange={e => setGroupBy(e.target.value)}><option value="day">Dia</option><option value="user">Usuário</option>{administrator && <><option value="organization">Organização</option><option value="connection">Conexão</option></>}</select></label>
      {usage.isError ? <p role="alert">Não foi possível carregar o consumo.</p> : usage.data?.length ? <div className="table-scroll"><table><thead><tr><th>Grupo</th><th>Chamadas</th><th>Tokens entrada / saída</th><th>Custo estimado</th></tr></thead><tbody>{usage.data.map(row => <tr key={row.key}><td>{row.key}</td><td>{row.calls}</td><td>{row.inputTokens} / {row.outputTokens}</td><td>{row.estimatedCost?.toFixed(6) ?? 'Não informado'}</td></tr>)}</tbody></table></div> : <p>Nenhuma chamada registrada neste período.</p>}
      <small>Consumo sem conteúdo das conversas. Os preços são os informados na conexão.</small>
    </details>
  </Section>;
}
