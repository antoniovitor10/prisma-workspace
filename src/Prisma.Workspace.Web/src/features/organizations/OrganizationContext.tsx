import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, Plus } from 'lucide-react';
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from 'react';
import styled from 'styled-components';
import { previewMode } from '../../preview';
import { api } from '../../services/api';
import { OrganizationStateContext, type OrganizationSummary } from './OrganizationState';

const previewOrganization: OrganizationSummary = {
  id: '11111111-1111-4111-8111-111111111111',
  name: 'Prisma Demo',
  slug: 'prisma-demo',
  isActive: true,
  locale: 'pt-BR',
  timeZone: 'America/Sao_Paulo',
  weekStartDay: 1,
  role: 1,
  isAdministrator: true,
};

const Center = styled.div`
  display: grid;
  min-height: 100vh;
  place-items: center;
  padding: 24px;
  background: ${({ theme }) => theme.color.bg};
`;

const Card = styled.form`
  display: grid;
  width: min(460px, 100%);
  gap: 14px;
  padding: 28px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.md};

  header { display: flex; align-items: center; gap: 12px; }
  h1 { font-size: 20px; }
  p { color: ${({ theme }) => theme.color.textMuted}; font-size: 14px; line-height: 1.55; }
  label { display: grid; gap: 6px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; font-weight: 750; }
  input { min-height: 40px; padding: 0 11px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; }
  button { display: flex; min-height: 40px; align-items: center; justify-content: center; gap: 7px; border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.brand}; color: white; font-weight: 800; }
  small { color: ${({ theme }) => theme.color.danger}; }
`;

function OrganizationOnboarding({ onCreated }: { onCreated: (organization: OrganizationSummary) => void }) {
  const [name, setName] = useState('');
  const create = useMutation({
    mutationFn: () => api.createOrganization({ name }),
    onSuccess: onCreated,
  });
  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (name.trim()) create.mutate();
  };

  return <Center><Card onSubmit={submit}>
    <header><Building2 size={24}/><div><h1>Crie sua organização</h1><p>Este será o ambiente isolado da sua equipe.</p></div></header>
    <label>Nome da organização<input autoFocus required maxLength={160} value={name} onChange={event => setName(event.target.value)} placeholder="Ex.: Acme Tecnologia"/></label>
    {create.error && <small>{create.error instanceof Error ? create.error.message : 'Não foi possível criar a organização.'}</small>}
    <button disabled={create.isPending}><Plus size={16}/>{create.isPending ? 'Criando...' : 'Criar ambiente'}</button>
  </Card></Center>;
}

export function OrganizationProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [activeId, setActiveId] = useState(() => api.getOrganizationId());
  const invitationToken = new URLSearchParams(window.location.search).get('invite')
    ?? localStorage.getItem('pendingInvite');
  const invitationAttempted = useRef(false);
  const query = useQuery<OrganizationSummary[]>({
    queryKey: ['organizations'],
    queryFn: () => api.getOrganizations(),
    enabled: !previewMode,
    staleTime: 60_000,
  });
  const organizations = useMemo(
    () => previewMode ? [previewOrganization] : (query.data ?? []),
    [query.data],
  );
  const acceptInvitation = useMutation({
    mutationFn: (token: string) => api.acceptOrganizationInvitation(token),
    onSuccess: async data => {
      localStorage.removeItem('pendingInvite');
      api.setOrganizationId(data.organizationId);
      setActiveId(data.organizationId);
      const url = new URL(window.location.href);
      url.searchParams.delete('invite');
      window.history.replaceState({}, '', `${url.pathname}${url.search}${url.hash}`);
      await queryClient.invalidateQueries({ queryKey: ['organizations'] });
    },
    onError: (caught) => {
      const message = caught instanceof Error ? caught.message : '';
      // Sessão morta (token de usuário que não existe mais): derruba a sessão e
      // PRESERVA o convite — após o login/cadastro correto o aceite acontece.
      if (/usuário não encontrado/i.test(message)) {
        api.clearToken();
        api.clearOrganizationId();
        window.location.replace(`/?invite=${encodeURIComponent(invitationToken ?? '')}`);
        return;
      }
      // Convite de outro e-mail: preserva o convite pra pessoa trocar de conta.
      if (/outro endereço/i.test(message)) return;
      // Expirado/inválido de verdade: aí sim descarta.
      localStorage.removeItem('pendingInvite');
    },
  });

  const switchOrganization = useCallback((organizationId: string) => {
    void queryClient.cancelQueries();
    api.setOrganizationId(organizationId);
    setActiveId(organizationId);
    queryClient.removeQueries({
      predicate: item => item.queryKey[0] !== 'organizations',
    });
  }, [queryClient]);

  useEffect(() => {
    if (!organizations.length) return;
    if (!activeId || !organizations.some(item => item.id === activeId))
      switchOrganization(organizations[0].id);
  }, [activeId, organizations, switchOrganization]);

  useEffect(() => {
    if (!previewMode && invitationToken && query.isSuccess && !invitationAttempted.current) {
      invitationAttempted.current = true;
      acceptInvitation.mutate(invitationToken);
    }
  }, [acceptInvitation, invitationToken, query.isSuccess]);

  if (!previewMode && query.isLoading)
    return <Center><p>Preparando seu ambiente...</p></Center>;
  if (!previewMode && query.isError)
    return <Center><Card as="div"><h1>Não foi possível carregar as organizações</h1><p>{query.error instanceof Error ? query.error.message : 'Tente novamente.'}</p><button onClick={() => query.refetch()}>Tentar novamente</button></Card></Center>;
  if (invitationToken && (acceptInvitation.isPending || !invitationAttempted.current))
    return <Center><p>Aceitando convite...</p></Center>;
  if (invitationToken && acceptInvitation.isError) {
    const message = acceptInvitation.error instanceof Error ? acceptInvitation.error.message : 'O convite expirou ou não pertence a este usuário.';
    const contaErrada = /outro endereço/i.test(message);
    return <Center><Card as="div">
      <h1>{contaErrada ? 'Convite de outra conta' : 'Convite indisponível'}</h1>
      <p>{contaErrada
        ? 'Este convite foi enviado para outro e-mail. Saia desta conta e entre (ou cadastre-se) com o e-mail que recebeu o convite.'
        : message}</p>
      {contaErrada && <button onClick={() => {
        api.clearToken(); api.clearOrganizationId();
        window.location.replace(`/?invite=${encodeURIComponent(invitationToken)}`);
      }}>Sair e aceitar com o e-mail convidado</button>}
      <button onClick={() => {
        localStorage.removeItem('pendingInvite');
        const url = new URL(window.location.href); url.searchParams.delete('invite'); window.location.assign(url.toString());
      }} style={contaErrada ? { marginTop: 8, background: 'transparent', color: '#64748B', border: '1px solid #CBD5E1' } : undefined}>
        {contaErrada ? 'Continuar com a conta atual' : 'Continuar'}</button>
    </Card></Center>;
  }
  if (!organizations.length)
    return <OrganizationOnboarding onCreated={organization => {
      queryClient.setQueryData<OrganizationSummary[]>(['organizations'], current => [...(current ?? []), organization]);
      switchOrganization(organization.id);
    }}/>;

  const current = organizations.find(item => item.id === activeId);
  if (!current)
    return <Center><p>Selecionando organização...</p></Center>;

  const value = { organizations, current, switchOrganization };
  return <OrganizationStateContext.Provider value={value}>{children}</OrganizationStateContext.Provider>;
}
