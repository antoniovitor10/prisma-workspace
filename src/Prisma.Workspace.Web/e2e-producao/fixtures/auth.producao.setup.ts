import { test as setup } from '@playwright/test';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { carregarCredenciais, enderecoDaProducao, mensagemDeCredenciaisAusentes } from '../credenciais.mjs';
import { ARQUIVO_SESSAO, NOME_ORGANIZACAO, chamarApi, gravarJson, normalizar, type Organizacao } from './contexto';

const raizWeb = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

/**
 * Login pela API de produção, sem navegador (CA-2, CA-3).
 *
 * - Usa `fetch` do Node, não o `request` do Playwright: nada do login entra em
 *   trace, vídeo, print ou passo do relatório.
 * - Nenhuma mensagem traz corpo de resposta, e-mail ou senha; só status e nomes.
 * - A organização "pessoal" é achada pela API, nunca fixada por id.
 */
setup('login pela API e organização "pessoal"', async () => {
  const estado = carregarCredenciais(raizWeb);
  const parada = mensagemDeCredenciaisAusentes(estado);
  if (parada) throw new Error(parada);

  const endereco = enderecoDaProducao();
  const resposta = await fetch(`${endereco}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: process.env.PROD_E2E_EMAIL, password: process.env.PROD_E2E_PASSWORD }),
  });
  if (!resposta.ok) throw new Error(`Login na produção recusado pela API (status ${resposta.status}). Confira a conta de teste.`);
  const { accessToken } = await resposta.json() as { accessToken?: string };
  if (!accessToken) throw new Error('Login na produção não devolveu o token de acesso.');

  const organizacoes = await chamarApi<Organizacao[]>({ endereco, token: accessToken }, 'GET', '/api/organizations');
  const pessoais = organizacoes.filter(org => org.isActive
    && (normalizar(org.name) === NOME_ORGANIZACAO || normalizar(org.slug) === NOME_ORGANIZACAO));
  if (pessoais.length !== 1)
    throw new Error(`A conta de teste precisa enxergar exatamente uma organização "${NOME_ORGANIZACAO}" ativa; achou ${pessoais.length}. Nada foi criado.`);

  gravarJson(ARQUIVO_SESSAO, {
    cookies: [],
    origins: [{
      origin: new URL(endereco).origin,
      localStorage: [
        { name: 'prisma_workspace_token', value: accessToken },
        { name: 'prisma_workspace_organization', value: pessoais[0].id },
      ],
    }],
  });
});
