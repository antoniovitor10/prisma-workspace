import { useMutation } from '@tanstack/react-query';
import { Building2 } from 'lucide-react';
import { type FormEvent, useState } from 'react';
import styled from 'styled-components';
import { api } from '../../services/api';
import type { OrganizationSummary } from './OrganizationState';

const Overlay = styled.div`
  position: fixed;
  inset: 0;
  z-index: 200;
  display: grid;
  place-items: center;
  padding: 16px;
  background: rgba(15, 23, 42, 0.45);
`;

const Card = styled.form`
  display: grid;
  width: min(420px, 100%);
  gap: 14px;
  padding: 24px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.lg};

  header { display: flex; align-items: center; gap: 10px; }
  h2 { font-size: 18px; }
  p { color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; line-height: 1.5; }
  label { display: grid; gap: 6px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; font-weight: 750; }
  input { min-height: 40px; padding: 0 11px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; }
  small { color: ${({ theme }) => theme.color.danger}; }
`;

const Actions = styled.div`
  display: flex;
  justify-content: flex-end;
  gap: 8px;
`;

const Button = styled.button<{ $primary?: boolean }>`
  min-height: 38px;
  padding: 0 14px;
  border: 1px solid ${({ theme, $primary }) => ($primary ? theme.color.brand : theme.color.border)};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme, $primary }) => ($primary ? theme.color.brand : 'transparent')};
  color: ${({ theme, $primary }) => ($primary ? theme.color.onBrand : theme.color.text)};
  font-weight: 800;
  &:disabled { opacity: 0.6; }
`;

/** Criação de organização adicional; só aparece para quem o servidor permite (administrador da plataforma). */
export function NewOrganizationDialog({ onClose, onCreated }: {
  onClose: () => void;
  onCreated: (organization: OrganizationSummary) => void;
}) {
  const [name, setName] = useState('');
  const create = useMutation({
    mutationFn: () => api.createOrganization({ name: name.trim() }) as Promise<OrganizationSummary>,
    onSuccess: onCreated,
  });
  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (name.trim()) create.mutate();
  };

  return <Overlay onMouseDown={event => { if (event.target === event.currentTarget) onClose(); }}>
    <Card role="dialog" aria-modal="true" aria-labelledby="new-organization-title" onSubmit={submit}
      onKeyDown={event => { if (event.key === 'Escape') onClose(); }}>
      <header><Building2 size={20} /><h2 id="new-organization-title">Nova organização</h2></header>
      <p>Ambiente separado, com projetos e membros próprios. Ninguém das outras organizações enxerga o que estiver nela.</p>
      <label>Nome da organização
        <input autoFocus required maxLength={160} value={name} onChange={event => setName(event.target.value)}
          placeholder="Ex.: Pessoal" />
      </label>
      {create.error && <small role="alert">{create.error instanceof Error ? create.error.message : 'Não foi possível criar a organização.'}</small>}
      <Actions>
        <Button type="button" onClick={onClose} disabled={create.isPending}>Cancelar</Button>
        <Button $primary type="submit" disabled={create.isPending || !name.trim()}>
          {create.isPending ? 'Criando...' : 'Criar organização'}
        </Button>
      </Actions>
    </Card>
  </Overlay>;
}
