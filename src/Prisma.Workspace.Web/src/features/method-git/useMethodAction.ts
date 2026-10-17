import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '../../services/api';

export function methodKey(projectId: string) { return ['method-git', api.getOrganizationId(), projectId]; }

export function useMethodAction(projectId: string) {
  const client = useQueryClient();
  const [pending, setPending] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  async function run<T>(key: string, operation: () => Promise<T>, message = ''): Promise<{ value: T } | undefined> {
    setPending(key); setError(''); setSuccess('');
    try {
      const value = await operation();
      await client.invalidateQueries({ queryKey: methodKey(projectId) });
      setSuccess(message);
      return { value };
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Não foi possível concluir a operação. Tente novamente.');
      return undefined;
    } finally { setPending(null); }
  }
  return { pending, error, success, run };
}
