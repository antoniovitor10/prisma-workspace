import { useQuery } from '@tanstack/react-query';
import { methodApi } from './client';

/**
 * HU-0007 (CA-8) no quadro: uma leitura por projeto (sem N+1) da identidade do método da pessoa logada.
 * Só a sessão da tela com Editar, Comentar e Mudar status vê o botão; a API confere de novo por tarefa (403).
 */
export function useCanOpenPullRequests(projectId: string | null | undefined, enabled: boolean): boolean {
  const identity = useQuery({
    queryKey: ['method-git', 'identity', projectId ?? ''],
    queryFn: () => methodApi.identity(projectId!),
    enabled: Boolean(projectId) && enabled,
    retry: false,
    staleTime: 60_000,
  });
  const data = identity.data;
  return Boolean(data && data.authenticationKind === 'uiSession'
    && ['Edit', 'Comment', 'ChangeStatus'].every(capability => data.capabilities.includes(capability)));
}
