import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { NewOrganizationDialog } from './NewOrganizationDialog';
import { OrganizationProvider } from './OrganizationContext';

vi.mock('../../preview', () => ({ previewMode: false }));

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

function mount() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(['organizations', 'creation'], { canCreate: true });
  const onCreated = vi.fn();
  const onClose = vi.fn();
  render(<QueryClientProvider client={client}><ThemeProvider theme={theme}>
    <button>Fora do diálogo</button>
    <NewOrganizationDialog onCreated={onCreated} onClose={onClose} />
  </ThemeProvider></QueryClientProvider>);
  return { client, onCreated, onClose };
}

describe('NewOrganizationDialog', () => {
  it('invalida elegibilidade de criação após salvar a organização', async () => {
    const created = { id: 'new-org', name: 'Pessoal' };
    vi.spyOn(api, 'createOrganization').mockResolvedValue(created);
    const { client, onCreated } = mount();
    fireEvent.change(screen.getByLabelText('Nome da organização'), { target: { value: 'Pessoal' } });
    fireEvent.click(screen.getByRole('button', { name: 'Criar organização' }));
    await waitFor(() => expect(onCreated).toHaveBeenCalledWith(created));
    expect(client.getQueryState(['organizations', 'creation'])?.isInvalidated).toBe(true);
    client.clear();
  });

  it('invalida elegibilidade após o onboarding da primeira organização', async () => {
    localStorage.clear();
    vi.spyOn(api, 'getOrganizationId').mockReturnValue(null);
    vi.spyOn(api, 'setOrganizationId').mockImplementation(() => {});
    vi.spyOn(api, 'getOrganizations').mockResolvedValue([]);
    const eligibility = vi.spyOn(api, 'getOrganizationCreation')
      .mockResolvedValueOnce({ canCreate: true }).mockResolvedValue({ canCreate: false });
    vi.spyOn(api, 'createOrganization').mockResolvedValue({ id: 'first-org', name: 'Primeira' });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(['organizations', 'creation'], { canCreate: true });
    render(<QueryClientProvider client={client}><ThemeProvider theme={theme}>
      <OrganizationProvider><p>Ambiente criado</p></OrganizationProvider>
    </ThemeProvider></QueryClientProvider>);
    fireEvent.change(await screen.findByLabelText('Nome da organização'), { target: { value: 'Primeira' } });
    await waitFor(() => expect(client.isFetching({ queryKey: ['organizations', 'creation'] })).toBe(0));
    fireEvent.click(screen.getByRole('button', { name: 'Criar ambiente' }));
    await screen.findByText('Ambiente criado');
    expect(eligibility).toHaveBeenCalledTimes(2);
    expect(client.getQueryData(['organizations', 'creation'])).toEqual({ canCreate: false });
    client.clear();
  });

  it('orienta a pessoa sem organização ativa quando a criação está recusada', async () => {
    localStorage.clear();
    vi.spyOn(api, 'getOrganizationId').mockReturnValue(null);
    vi.spyOn(api, 'getOrganizations').mockResolvedValue([]);
    vi.spyOn(api, 'getOrganizationCreation').mockResolvedValue({ canCreate: false });
    const create = vi.spyOn(api, 'createOrganization');
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<QueryClientProvider client={client}><ThemeProvider theme={theme}>
      <OrganizationProvider><p>Ambiente criado</p></OrganizationProvider>
    </ThemeProvider></QueryClientProvider>);
    await screen.findByRole('heading', { name: 'Acesso à organização indisponível' });
    expect(screen.queryByRole('button', { name: 'Criar ambiente' })).not.toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
    client.clear();
  });

  it('mantém foco dentro do diálogo e permite fechar com Escape', async () => {
    const { client, onClose } = mount();
    const input = screen.getByLabelText('Nome da organização');
    await waitFor(() => expect(input).toHaveFocus());
    screen.getByText('Fora do diálogo').focus();
    expect(screen.getByRole('dialog')).toContainElement(document.activeElement as HTMLElement);
    fireEvent.keyDown(document.activeElement!, { key: 'Escape' });
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
    client.clear();
  });
});
