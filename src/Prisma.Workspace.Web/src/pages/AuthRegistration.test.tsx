import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppThemeProvider } from '../styles/ThemeMode';
import { api } from '../services/api';
import { Auth } from './Auth';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });
beforeEach(() => { localStorage.clear(); window.history.replaceState({}, '', '/login'); });
function show() {
  vi.spyOn(api, 'getSetupStatus').mockResolvedValue({ initialized: true, setupAvailable: false });
  render(<AppThemeProvider><Auth /></AppThemeProvider>);
  fireEvent.click(screen.getByRole('button', { name: 'registre-se' }));
}
describe('Cadastro público com nome completo', () => {
  it('orienta nome e sobrenome e impede envio com nome incompleto', async () => {
    const register = vi.spyOn(api, 'register');
    show();
    const name = screen.getByRole('textbox', { name: 'Nome completo' });
    expect(name).toBeRequired();
    expect(name).toHaveAttribute('autocomplete', 'name');
    fireEvent.change(name, { target: { value: 'Ana' } });
    fireEvent.blur(name);
    expect(name).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('Informe seu nome e sobrenome.');
    fireEvent.submit(name.closest('form')!);
    await waitFor(() => expect(screen.getAllByText('Informe seu nome e sobrenome.').length).toBeGreaterThan(0));
    expect(register).not.toHaveBeenCalled();
  });
  it('envia o nome preenchido e preserva o fluxo de confirmação de e-mail', async () => {
    const register = vi.spyOn(api, 'register').mockResolvedValue({ requiresEmailConfirmation: true });
    show();
    const name = screen.getByRole('textbox', { name: 'Nome completo' });
    fireEvent.change(name, { target: { value: 'Ana Alves' } });
    fireEvent.change(screen.getByPlaceholderText('seu.nome@empresa.com'), { target: { value: 'ana@example.test' } });
    fireEvent.change(screen.getByPlaceholderText('Digite sua senha'), { target: { value: 'Local-test-password-123!' } });
    fireEvent.submit(name.closest('form')!);
    await waitFor(() => expect(register).toHaveBeenCalledWith('ana@example.test', 'Local-test-password-123!', 'Ana Alves'));
    expect(await screen.findByText('Cadastro criado. Confirme o e-mail enviado antes de entrar.')).toBeVisible();
  });
});
