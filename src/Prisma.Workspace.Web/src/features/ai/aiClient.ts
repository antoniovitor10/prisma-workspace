import { useContext } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../services/api';
import { OrganizationStateContext } from '../organizations/OrganizationState';

export type AiStatus = { enabled: boolean; isPlatformAdministrator: boolean; canAdministerOrganization: boolean; remainingTokens: number | null };
export type AiSource = { reference: string; title: string; path: string };

export function useAiStatus() {
  const organization = useContext(OrganizationStateContext);
  return useQuery<AiStatus>({ queryKey: ['ai-status', organization?.current.id ?? api.getOrganizationId()],
    queryFn: () => api.request('/api/ai/status'), retry: false, refetchInterval: 10_000 });
}

export async function readAiEvents(response: Response, onEvent: (event: string, data: Record<string, unknown>) => void) {
  if (!response.ok) { const error = await response.json().catch(() => ({})); throw new Error(error.detail ?? 'Não foi possível enviar a pergunta.'); }
  if (!response.body) throw new Error('Resposta sem conteúdo.');
  const reader = response.body.getReader(); const decoder = new TextDecoder(); let buffer = '';
  try {
    while (true) {
      const { value, done } = await reader.read(); buffer += decoder.decode(value, { stream: !done }).replace(/\r\n/g, '\n');
      let boundary: number;
      while ((boundary = buffer.indexOf('\n\n')) >= 0) {
        const event = buffer.slice(0, boundary); buffer = buffer.slice(boundary + 2);
        const name = event.split('\n').find(line => line.startsWith('event:'))?.slice(6).trim();
        const data = event.split('\n').filter(line => line.startsWith('data:')).map(line => line.slice(5).trim()).join('\n');
        if (name && data) onEvent(name, JSON.parse(data));
      }
      if (done) break;
    }
  } finally { reader.releaseLock(); }
}
