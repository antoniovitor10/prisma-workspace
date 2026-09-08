import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppThemeProvider } from '../styles/ThemeMode';
import { api, SetupApiError } from '../services/api';
import { Setup } from './Setup';

function renderSetup(onExit = vi.fn()) {
  render(<AppThemeProvider><Setup onExit={onExit} /></AppThemeProvider>);
  return onExit;
}

describe('Setup', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('exibe o formulário somente quando o setup está disponível', async () => {
    vi.spyOn(api, 'getSetupStatus').mockResolvedValue({ initialized: false, setupAvailable: true });
    renderSetup();

    expect(await screen.findByRole('heading', { name: 'Prepare seu Prisma WorkSpace' })).toBeVisible();
    expect(screen.getByLabelText(/^Código de instalação/)).toHaveAttribute('type', 'password');
    expect(screen.getByRole('button', { name: /Concluir configuração/ })).toBeEnabled();
  });

  it('envia o contrato aprovado e descarta o token após sucesso', async () => {
    vi.spyOn(api, 'getSetupStatus').mockResolvedValue({ initialized: false, setupAvailable: true });
    const completeSetup = vi.spyOn(api, 'completeSetup').mockResolvedValue({ initialized: true });
    const onExit = renderSetup();

    await screen.findByRole('heading', { name: 'Prepare seu Prisma WorkSpace' });
    fireEvent.change(screen.getByLabelText(/^Código de instalação/), { target: { value: 'segredo-temporario' } });
    fireEvent.change(screen.getByLabelText('Nome completo'), { target: { value: 'Ana Silva' } });
    fireEvent.change(screen.getByLabelText('E-mail'), { target: { value: 'ana@example.com' } });
    fireEvent.change(screen.getByLabelText(/^Senha/), { target: { value: 'SenhaForte#123' } });
    fireEvent.change(screen.getByLabelText('Nome da organização'), { target: { value: 'Equipe Prisma' } });
    fireEvent.change(screen.getByLabelText(/^Identificador \(slug\)/), { target: { value: 'Equipe Prisma!' } });
    fireEvent.click(screen.getByRole('button', { name: /Concluir configuração/ }));

    await waitFor(() => expect(completeSetup).toHaveBeenCalledWith('segredo-temporario', {
      administratorName: 'Ana Silva',
      administratorEmail: 'ana@example.com',
      administratorPassword: 'SenhaForte#123',
      organizationName: 'Equipe Prisma',
      organizationSlug: 'equipeprisma',
    }));
    expect(onExit).toHaveBeenCalledWith(true);
    expect(screen.queryByDisplayValue('segredo-temporario')).not.toBeInTheDocument();
  });

  it('orienta sem revelar detalhes quando o setup fica indisponível', async () => {
    vi.spyOn(api, 'getSetupStatus').mockResolvedValue({ initialized: false, setupAvailable: true });
    vi.spyOn(api, 'completeSetup').mockRejectedValue(new SetupApiError(403, 'setup_unavailable', 'detalhe interno'));
    renderSetup();

    await screen.findByRole('heading', { name: 'Prepare seu Prisma WorkSpace' });
    fireEvent.change(screen.getByLabelText(/^Código de instalação/), { target: { value: 'token-invalido' } });
    fireEvent.change(screen.getByLabelText('Nome completo'), { target: { value: 'Ana Silva' } });
    fireEvent.change(screen.getByLabelText('E-mail'), { target: { value: 'ana@example.com' } });
    fireEvent.change(screen.getByLabelText(/^Senha/), { target: { value: 'SenhaForte#123' } });
    fireEvent.change(screen.getByLabelText('Nome da organização'), { target: { value: 'Equipe Prisma' } });
    fireEvent.change(screen.getByLabelText(/^Identificador \(slug\)/), { target: { value: 'equipe-prisma' } });
    fireEvent.click(screen.getByRole('button', { name: /Concluir configuração/ }));

    expect(await screen.findByRole('heading', { name: 'Configuração indisponível' })).toBeVisible();
    expect(screen.queryByText('detalhe interno')).not.toBeInTheDocument();
    expect(screen.queryByDisplayValue('token-invalido')).not.toBeInTheDocument();
  });
});
