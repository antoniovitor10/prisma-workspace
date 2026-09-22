import { test, expect } from './fixtures/test';

/**
 * Regressão: o convite de membro descartava o resultado de SendAsync. Sem SMTP
 * configurado o e-mail não saía, mas a tela mostrava sucesso e um link sem dizer
 * que ele precisava ser compartilhado. Quem foi convidado se cadastrou sem o
 * parâmetro `?invite=`, ficou com zero organizações e caiu na criação de um
 * ambiente novo, sem acesso ao que já existia.
 */
test('convite informa se o e-mail saiu e sempre entrega o link', async ({ page, authenticatedGoto }) => {
  await authenticatedGoto('/settings');

  const email = `qa.convite.${Date.now()}@exemplo.invalid`;
  await page.getByPlaceholder('pessoa@empresa.com').fill(email);
  await page.getByRole('button', { name: 'Gerar convite' }).click();

  // O link precisa aparecer em qualquer cenário: é o caminho de recuperação
  // quando o e-mail não pode ser entregue.
  const link = page.locator('code', { hasText: 'invite=' });
  await expect(link).toBeVisible();
  await expect(link).toContainText(/[?&]invite=[0-9a-f]{16,}/);

  // E a tela precisa dizer explicitamente o que aconteceu com o e-mail.
  const aviso = page.getByRole('status');
  await expect(aviso).toBeVisible();
  await expect(aviso).toHaveText(/enviado por e-mail|não foi possível enviar o e-mail/i);
});
