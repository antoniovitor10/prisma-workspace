import { useQuery } from '@tanstack/react-query';
import { api } from '../../services/api';

export type AiChoice = { connectionId: string | null; model: string | null };
type Option = { id: string; name: string; provider: string; type: string; model: string; isDefault: boolean };
type Catalog = { models: { id: string; name: string }[]; state: string; message: string };

export function AiConversationControls({ tenantId, choice, disabled, dirty, onConnection, onModel }: {
  tenantId: string | null | undefined; choice: AiChoice; disabled: boolean; dirty: boolean;
  onConnection: (id: string | null) => void; onModel: (model: string) => void;
}) {
  const options = useQuery<{ connections: Option[] }>({ queryKey: ['ai-chat-options', tenantId], queryFn: ({ signal }) => api.request('/api/ai/options', { signal: AbortSignal.any([signal, AbortSignal.timeout(20000)]) }), retry: false });
  const catalog = useQuery<Catalog>({ queryKey: ['ai-chat-models', tenantId, choice.connectionId], enabled: !!choice.connectionId,
    queryFn: ({ signal }) => api.request(`/api/ai/options/${choice.connectionId}/models?refresh=true`, { signal: AbortSignal.any([signal, AbortSignal.timeout(20000)]) }), retry: false });
  const connections = options.data?.connections ?? [];
  const connection = connections.find(c => c.id === choice.connectionId);
  const defaultConnection = connections.find(c => c.isDefault);
  const missing = !!choice.connectionId && options.isSuccess && !connection;
  const validModel = catalog.data?.models.some(m => m.id === choice.model);
  const providers: Record<string, string> = { OpenAI: 'OpenAI / ChatGPT', Gemini: 'Google Gemini', Anthropic: 'Claude', OpenRouter: 'OpenRouter', Custom: 'Endpoint personalizado' };
  return <div className="assistant-selectors">
    <label>Provedor da conversa<select aria-label="Provedor da conversa" disabled={disabled || options.isPending} value={choice.connectionId ?? ''} onChange={e => onConnection(e.target.value || null)}>
      <option value="">Usar padrão da instalação</option>
      {missing && <option value={choice.connectionId!}>Conexão salva indisponível</option>}
      {connections.map(c => <option key={c.id} value={c.id}>{providers[c.provider] ?? c.provider} · {c.name}</option>)}
    </select></label>
    {choice.connectionId ? <label>Modelo da conversa<select aria-label="Modelo da conversa" value={choice.model ?? ''} disabled={disabled || missing || catalog.isPending || catalog.data?.state !== 'ready'} onChange={e => onModel(e.target.value)}>
      <option value="">{catalog.isPending ? 'Carregando modelos…' : 'Escolha um modelo'}</option>
      {choice.model && !validModel && <option value={choice.model}>{choice.model} (salvo; confira disponibilidade)</option>}
      {catalog.data?.models.map(m => <option key={m.id} value={m.id}>{m.name === m.id ? m.id : `${m.name} — ${m.id}`}</option>)}
    </select></label> : <p className="default-model">{defaultConnection ? `${providers[defaultConnection.provider] ?? defaultConnection.provider} · ${defaultConnection.model}` : 'A conexão padrão será usada.'}</p>}
    <button className="text-action" type="button" disabled={disabled || options.isFetching || !!choice.connectionId && catalog.isFetching} onClick={() => { void options.refetch(); if (choice.connectionId) void catalog.refetch(); }}>Atualizar conexões</button>
    {options.isError && <p className="selector-notice" role="alert">Não foi possível carregar as conexões. Tente atualizar.</p>}
    {missing && <p className="selector-notice" role="alert">Esta conexão não está mais disponível. Escolha outra ou o padrão da instalação.</p>}
    {choice.connectionId && !missing && (catalog.isError || catalog.data?.state !== 'ready' && !catalog.isPending) && <p className="selector-notice" role="alert">{catalog.data?.message ?? 'Não foi possível carregar os modelos. Tente atualizar.'}</p>}
    {choice.connectionId && catalog.data?.state === 'ready' && choice.model && !validModel && <p className="selector-notice" role="alert">O modelo salvo não aparece no catálogo. Escolha outro antes de perguntar.</p>}
    {dirty && <small className="selector-notice">Escolha um modelo para confirmar o assistente desta conversa.</small>}
  </div>;
}
