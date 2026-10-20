import { useCallback, useRef, type SetStateAction } from 'react';
import { useSearchParams } from 'react-router-dom';
import { readBacklogUrl, writeBacklogUrl, type BacklogUrlState } from './BacklogUrlState';

// A URL é a fonte de leitura, inclusive no voltar/avançar. O ref compõe alterações
// do mesmo evento, pois setSearchParams não enfileira callbacks como setState.
export function useBacklogUrlState() {
  const [params, setParams] = useSearchParams();
  const pending = useRef({ observed: params.toString(), params });
  if (pending.current.observed !== params.toString()) pending.current = { observed: params.toString(), params };
  const state = readBacklogUrl(params);
  const updateParams = useCallback((change: (current: URLSearchParams) => URLSearchParams) => {
    const next = change(new URLSearchParams(pending.current.params));
    pending.current.params = next;
    setParams(next, { replace: true, preventScrollReset: true });
  }, [setParams]);
  const update = useCallback(<K extends keyof BacklogUrlState>(field: K, value: SetStateAction<BacklogUrlState[K]>) => {
    updateParams(params => {
      const current = readBacklogUrl(params);
      const nextValue = typeof value === 'function'
        ? (value as (previous: BacklogUrlState[K]) => BacklogUrlState[K])(current[field])
        : value;
      return writeBacklogUrl(params, { ...current, [field]: nextValue });
    });
  }, [updateParams]);
  return { state, update, params, updateParams };
}
