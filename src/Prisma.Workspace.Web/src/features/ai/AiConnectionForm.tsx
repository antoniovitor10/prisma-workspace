import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { api } from '../../services/api';
const request = async <T,>(url: string, init?: RequestInit): Promise<T> => api.request(url, init);

export type AiConnection = { id: string; name: string; type: string; provider: string; baseUrl: string | null; model: string; inputPrice: number | null; outputPrice: number | null; isActive: boolean; testSucceeded: boolean; testMessage: string | null; hasSecret: boolean; secretSuffix: string | null; latencyMs: number | null };
type Model = { id: string; name: string; inputPrice?: number | null; outputPrice?: number | null };
type Catalog = { models: Model[]; state: string; message: string; manualAllowed?: boolean };
type CliState = { available: boolean; authenticated: boolean; state: string; message: string; sessionId?: string; url?: string | null; deviceCode?: string | null; requiresCode?: boolean };
const providers = [{ id: 'OpenRouter', name: 'OpenRouter' }, { id: 'OpenAI', name: 'OpenAI / ChatGPT' }, { id: 'Anthropic', name: 'Anthropic / Claude' }, { id: 'Gemini', name: 'Google Gemini' }, { id: 'Custom', name: 'Endpoint personalizado' }];
const methods = (provider: string) => provider === 'Custom' ? ['OpenAiCompatible'] : provider === 'OpenRouter' ? ['OAuth', 'ApiKey'] : ['OpenAI', 'Anthropic'].includes(provider) ? ['ApiKey', 'CliSubscription'] : ['ApiKey'];
const methodNames: Record<string, string> = { OAuth: 'Login com OpenRouter', ApiKey: 'Chave de API', CliSubscription: 'Assinatura via CLI (experimental)', OpenAiCompatible: 'Endpoint compatível com OpenAI' };
export const cliRisk = 'Experimental. Usar uma assinatura pessoal nesta integração, especialmente por várias pessoas, pode violar os termos do provedor. O provedor pode bloquear ou encerrar sua conta e você pode perder o acesso à conta e à assinatura. O Prisma não garante que esse uso seja permitido. Para uso de equipe, prefira chave de API ou uma autorização do provedor que cubra a integração.';
const pending = (state?: string) => ['starting', 'waiting', 'completing'].includes(state ?? '');
const safeLoginUrl = (value?: string | null) => {
  if (!value) return null;
  try { const url = new URL(value); return url.protocol === 'https:' && !url.username && !url.password && ['auth.openai.com', 'claude.com', 'claude.ai', 'console.anthropic.com', 'platform.claude.com'].includes(url.hostname) ? value : null; } catch { return null; }
};

export function AiConnectionForm({ connection, onSaved, onCancel, onAuthenticationStarted }: { connection?: AiConnection; onSaved: () => Promise<void>; onCancel: () => void; onAuthenticationStarted?: () => Promise<void> }) {
  const [form, setForm] = useState(() => ({ name: connection?.name ?? '', provider: connection?.provider ?? 'OpenRouter', type: connection?.type ?? 'OAuth', model: connection?.model ?? '', baseUrl: connection?.type === 'CliSubscription' ? '' : connection?.baseUrl ?? '', secret: '', inputPrice: connection?.inputPrice?.toString() ?? '', outputPrice: connection?.outputPrice?.toString() ?? '' }));
  const [catalog, setCatalog] = useState<Catalog | null>(null); const [loading, setLoading] = useState(false); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const [search, setSearch] = useState(''); const [manual, setManual] = useState(false); const [risk, setRisk] = useState(false); const [cli, setCli] = useState<CliState | null>(null); const [code, setCode] = useState('');
  const generation = useRef(0); const loadAbort = useRef<AbortController | null>(null); const activeLogin = useRef<{ provider: string; sessionId: string } | null>(null);
  const validMethod = methods(form.provider).includes(form.type); const savedCredential = connection?.hasSecret && connection.provider === form.provider && connection.type === form.type && (connection.baseUrl ?? '').replace(/\/$/, '') === form.baseUrl.replace(/\/$/, '');
  const isCli = form.type === 'CliSubscription'; const isOAuth = form.type === 'OAuth';
  const providerName = providers.find(p => p.id === form.provider)?.name ?? form.provider;
  const needsKey = !isCli && !isOAuth && form.provider !== 'Custom' && !form.secret && !savedCredential;
  const catalogPrerequisite = !validMethod ? 'Corrija a forma de conexão acima para escolher um modelo.'
    : isCli && !cli?.authenticated ? `Entre com ${form.provider === 'OpenAI' ? 'ChatGPT' : 'Claude'} acima. Os modelos aparecem após concluir o login.`
    : needsKey ? `Cole a chave de API do ${providerName} acima. Os modelos serão carregados automaticamente.`
    : form.provider === 'Custom' && !form.baseUrl ? 'Informe a URL base acima para carregar os modelos.' : null;
  const loadModels = useCallback(async () => {
    loadAbort.current?.abort(); const controller = new AbortController(); loadAbort.current = controller;
    const current = generation.current; setLoading(true); setError('');
    try {
      const result = await request<Catalog>('/api/admin/ai/models', { method: 'POST', signal: controller.signal, body: JSON.stringify({ provider: form.provider, type: form.type, baseUrl: form.baseUrl || null, secret: form.secret || null, connectionId: connection?.id }) });
      if (current === generation.current && !controller.signal.aborted) { setCatalog(result); setManual(false); }
    } catch (e) { if (current === generation.current && !controller.signal.aborted) setError((e as Error).message); }
    finally { if (current === generation.current && !controller.signal.aborted) setLoading(false); }
  }, [form.provider, form.type, form.baseUrl, form.secret, connection?.id]);
  const modelsLoader = useRef(loadModels);
  useEffect(() => { modelsLoader.current = loadModels; }, [loadModels]);
  const cancelLogin = useCallback(() => {
    const previous = activeLogin.current; activeLogin.current = null;
    if (previous) void api.request(`/api/admin/ai/cli/${previous.provider}/login/${previous.sessionId}`, { method: 'DELETE' }).catch(() => undefined);
  }, []);
  useEffect(() => () => { generation.current++; loadAbort.current?.abort(); cancelLogin(); }, [cancelLogin]);
  useEffect(() => {
    if (catalogPrerequisite) return;
    const timer = window.setTimeout(() => void modelsLoader.current(), 350);
    return () => window.clearTimeout(timer);
    // Aguarda uma pausa na edição e cancela consultas de credenciais anteriores.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form.provider, form.type, form.baseUrl, form.secret, catalogPrerequisite]);
  useEffect(() => {
    if (!isCli || !validMethod) return;
    const controller = new AbortController();
    void request<CliState>(`/api/admin/ai/cli/${form.provider}/status`, { signal: controller.signal }).then(result => { if (!controller.signal.aborted) setCli(result); }).catch(e => { if (!controller.signal.aborted) setError((e as Error).message); });
    return () => controller.abort();
  }, [isCli, validMethod, form.provider]);
  useEffect(() => {
    if (!cli?.sessionId || !pending(cli.state)) return;
    const controller = new AbortController(); const current = generation.current;
    const timer = window.setInterval(() => {
      void request<CliState>(`/api/admin/ai/cli/${form.provider}/login/${cli.sessionId}`, { signal: controller.signal }).then(result => {
        if (current !== generation.current || controller.signal.aborted) return;
        setCli(result);
        if (!pending(result.state)) { activeLogin.current = null; setCode(''); }
        if (result.authenticated) void loadModels();
      }).catch(e => { if (!controller.signal.aborted && current === generation.current) { setError((e as Error).message); cancelLogin(); setCli(previous => previous ? { ...previous, state: 'failed' } : null); } });
    }, 2000);
    return () => { window.clearInterval(timer); controller.abort(); };
  }, [cli?.sessionId, cli?.state, form.provider, loadModels, cancelLogin]);
  const changeTarget = (change: Partial<typeof form>) => {
    if (Object.entries(change).every(([key, value]) => form[key as keyof typeof form] === value)) return;
    generation.current++; loadAbort.current?.abort(); cancelLogin(); setCatalog(null); setSearch(''); setManual(false); setCli(null); setCode(''); setRisk(false); setError(''); setLoading(false);
    setForm(v => ({ ...v, ...change, model: '', secret: '', inputPrice: '', outputPrice: '' }));
  };
  const startLogin = async () => {
    if (!risk) return; setBusy(true); setError(''); const current = generation.current; const provider = form.provider;
    try {
      const result = await request<CliState>(`/api/admin/ai/cli/${provider}/login`, { method: 'POST', body: JSON.stringify({ acceptedRisk: true }) });
      if (current !== generation.current) { if (result.sessionId) void api.request(`/api/admin/ai/cli/${provider}/login/${result.sessionId}`, { method: 'DELETE' }).catch(() => undefined); return; }
      if (result.sessionId) { activeLogin.current = { provider, sessionId: result.sessionId }; void onAuthenticationStarted?.(); } setCli(result);
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  };
  const submit = async (event: FormEvent) => {
    event.preventDefault(); setBusy(true); setError('');
    try {
      const result = await request<AiConnection>(`/api/admin/ai/connections${connection ? '/' + connection.id : ''}`, { method: connection ? 'PUT' : 'POST', body: JSON.stringify({ ...form, name: form.name.trim() || `${providers.find(p => p.id === form.provider)?.name} · ${form.model}`, baseUrl: form.baseUrl || null, inputPrice: form.inputPrice ? Number(form.inputPrice) : null, outputPrice: form.outputPrice ? Number(form.outputPrice) : null }) });
      if (isOAuth && !savedCredential) { const auth = await request<{ url: string }>(`/api/admin/ai/connections/${result.id}/oauth/start`, { method: 'POST' }); window.location.assign(auth.url); }
      await onSaved();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  };
  const visibleModels = (catalog?.models ?? []).filter(m => `${m.name} ${m.id}`.toLocaleLowerCase().includes(search.toLocaleLowerCase()));
  const loginUrl = safeLoginUrl(cli?.url);
  return <form onSubmit={e => void submit(e)} aria-label="Cadastro de conexão IA">
    <h3 className="wide">{connection ? 'Editar conexão' : 'Conectar um provedor'}</h3>
    <label>Provedor<select aria-label="Provedor" value={form.provider} disabled={busy} onChange={e => { if (e.target.value !== form.provider) changeTarget({ provider: e.target.value, type: methods(e.target.value)[0], baseUrl: '' }); }}>{providers.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
    {(methods(form.provider).length > 1 || !validMethod) ? <label>Como conectar<select aria-label="Como conectar" value={form.type} disabled={busy} onChange={e => changeTarget({ type: e.target.value, baseUrl: '' })}>{!validMethod && <option value={form.type} disabled>Combinação antiga inválida — escolha outra</option>}{methods(form.provider).map(m => <option key={m} value={m}>{methodNames[m]}</option>)}</select></label> : <p className="method">{methodNames[form.type]}</p>}
    {!validMethod && <div className="wide" role="alert"><p>{providerName} não oferece esta forma de conexão. Escolha uma opção válida em “Como conectar”.</p><button type="button" className="secondary" disabled={busy} onClick={() => changeTarget({ type: methods(form.provider)[0], baseUrl: '' })}>Usar {methodNames[methods(form.provider)[0]].replace(/^./, letter => letter.toLocaleLowerCase())} {form.provider === 'Custom' ? '' : `com ${providerName}`}</button></div>}
    {form.provider === 'Custom' && <label className="wide">URL base<input aria-label="URL base" type="url" required placeholder="https://seu-servidor/v1" value={form.baseUrl} onChange={e => changeTarget({ baseUrl: e.target.value })}/><small>Endereço acessível pelo servidor do Prisma.</small></label>}
    {!isCli && !isOAuth && <label className="wide">Chave de API<input aria-label="Chave de API" type="password" autoComplete="new-password" value={form.secret} placeholder={savedCredential ? 'Credencial salva; deixe vazio para manter' : form.provider === 'Custom' ? 'Opcional, conforme o endpoint' : 'Cole a chave do provedor'} required={form.type === 'ApiKey' && !savedCredential} onChange={e => { generation.current++; loadAbort.current?.abort(); setLoading(false); setCatalog(null); setForm(v => ({ ...v, secret: e.target.value, model: '' })); }}/><small>{savedCredential ? 'A credencial salva será usada somente neste provedor e endereço.' : 'Sua chave é criptografada no servidor e não volta para o navegador.'}</small></label>}
    {isOAuth && <p className="wide">{savedCredential ? 'Login OpenRouter conectado. Você pode escolher outro modelo ou autenticar novamente após salvar.' : 'Escolha o modelo e entre com sua conta no OpenRouter. Você autoriza o acesso no site do provedor.'}</p>}
    {isCli && validMethod && <div className="wide cli-login"><h4>Autenticação da assinatura</h4><p>{cliRisk}</p><p>Esta conta será usada pela instalação, por todas as organizações que habilitarem a IA.</p>
      <label className="check"><input type="checkbox" checked={risk} onChange={e => setRisk(e.target.checked)}/><span>Entendo o risco de bloqueio ou perda da conta e quero continuar</span></label>
      <p role="status">{cli?.message ?? 'Verificando a disponibilidade da conexão CLI…'}</p>
      {cli?.authenticated && <p><strong>Conta autenticada</strong></p>}
      {pending(cli?.state) ? <><p>Conclua o login iniciado por você nesta tela. A tentativa expira em até 10 minutos.</p>{loginUrl && <a className="login-link" href={loginUrl} target="_blank" rel="noopener noreferrer" referrerPolicy="no-referrer">Abrir login oficial do provedor</a>}{cli?.deviceCode && <p>Código temporário: <strong>{cli.deviceCode}</strong></p>}
        {cli?.requiresCode && <label>Código retornado pelo Claude<input aria-label="Código retornado pelo Claude" type="password" autoComplete="off" value={code} onChange={e => setCode(e.target.value)}/><button type="button" disabled={!code || busy} onClick={() => { setBusy(true); void request<CliState>(`/api/admin/ai/cli/${form.provider}/login/${cli.sessionId}/complete`, { method: 'POST', body: JSON.stringify({ code }) }).then(result => { setCli(result); setCode(''); if (result.authenticated) void loadModels(); }).catch(e => setError((e as Error).message)).finally(() => setBusy(false)); }}>Confirmar código</button></label>}
        <button className="secondary" type="button" disabled={busy} onClick={() => { cancelLogin(); setCli(previous => previous ? { ...previous, state: 'cancelled', url: null, deviceCode: null, requiresCode: false, message: 'Tentativa cancelada. Você pode iniciar novamente.' } : null); setCode(''); }}>Cancelar login</button>
      </> : <button className={cli?.authenticated ? 'secondary' : undefined} type="button" disabled={busy || !risk || !cli?.available} onClick={() => void startLogin()}>{cli?.authenticated ? `Trocar conta ${form.provider === 'OpenAI' ? 'do ChatGPT' : 'do Claude'}` : form.provider === 'OpenAI' ? 'Entrar com ChatGPT' : 'Entrar com Claude'}</button>}
    </div>}
    <div className="wide model-selection">
      <div className="catalog-heading"><h4>Modelos disponíveis</h4><button className="secondary" type="button" disabled={loading || busy || !!catalogPrerequisite} onClick={() => void loadModels()}>{loading ? 'Carregando modelos…' : 'Atualizar modelos'}</button></div>
      <p id="ai-model-status" role="status">{catalogPrerequisite ?? (loading ? 'Carregando os modelos do provedor…' : catalog?.message ?? 'Preparando o catálogo de modelos…')}</p>
      {(catalog?.models?.length ?? 0) > 10 && <label>Buscar modelo<input type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Nome ou identificador"/></label>}
      {!manual && <label>Modelo<select aria-label="Modelo" aria-describedby="ai-model-status" required value={form.model} disabled={loading || !catalog?.models?.length || !!catalogPrerequisite} onChange={e => { const model = catalog?.models.find(m => m.id === e.target.value); setForm(v => ({ ...v, model: e.target.value, inputPrice: model?.inputPrice?.toString() ?? v.inputPrice, outputPrice: model?.outputPrice?.toString() ?? v.outputPrice })); }}><option value="">{!validMethod ? 'Corrija a forma de conexão' : isCli && !cli?.authenticated ? 'Conclua o login para escolher' : needsKey ? 'Informe a chave de API para escolher' : loading ? 'Carregando modelos…' : catalog?.models?.length ? 'Selecione um modelo' : 'Aguardando catálogo de modelos'}</option>{connection && form.model && !catalog?.models?.some(m => m.id === form.model) && <option value={form.model}>{form.model} (modelo salvo; confira o catálogo)</option>}{visibleModels.map(m => <option value={m.id} key={m.id}>{m.name === m.id ? m.id : `${m.name} — ${m.id}`}</option>)}</select></label>}
      {form.model && <small>Identificador: {form.model}. O teste confirma o acesso da sua conta antes de ativar.</small>}
    </div>
    <details className="wide advanced"><summary>Opções avançadas</summary><div className="fields">
      <label className="wide">Nome da conexão<input maxLength={160} value={form.name} placeholder="Sugerido a partir do provedor e modelo" onChange={e => setForm(v => ({ ...v, name: e.target.value }))}/></label>
      {!isCli && !isOAuth && form.provider !== 'Custom' && <label className="wide">URL base personalizada<input type="url" value={form.baseUrl} placeholder="Usar endereço oficial do provedor" onChange={e => changeTarget({ baseUrl: e.target.value })}/><small>Alterar o endereço exige informar a credencial novamente.</small></label>}
      {catalog?.manualAllowed && <><label className="check wide"><input type="checkbox" checked={manual} onChange={e => { setManual(e.target.checked); setForm(v => ({ ...v, model: '' })); }}/><span>Informar identificador manualmente</span></label>{manual && <label className="wide">Identificador do modelo<input required maxLength={200} value={form.model} onChange={e => setForm(v => ({ ...v, model: e.target.value }))}/><small>Use somente se o endpoint não publicar um catálogo. O teste é obrigatório.</small></label>}</>}
      <label>Preço por milhão de tokens de entrada<input type="number" min="0" step="any" value={form.inputPrice} onChange={e => setForm(v => ({ ...v, inputPrice: e.target.value }))}/></label><label>Preço por milhão de tokens de saída<input type="number" min="0" step="any" value={form.outputPrice} onChange={e => setForm(v => ({ ...v, outputPrice: e.target.value }))}/></label><small className="wide">Preços opcionais para estimar consumo. O OpenRouter informa preços pelo catálogo quando disponíveis.</small>
    </div></details>
    {error && <p className="wide" role="alert">{error}</p>}
    <div className="wide actions"><button disabled={busy || loading || !form.model || !validMethod || isCli && !cli?.authenticated}>{busy ? 'Aguarde…' : isOAuth && !savedCredential ? 'Salvar e entrar com OpenRouter' : 'Salvar conexão'}</button><button className="secondary" type="button" disabled={busy} onClick={onCancel}>Cancelar</button></div>
  </form>;
}
