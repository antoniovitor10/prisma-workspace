// Leitura das credenciais da suíte de produção.
//
// Regra: só o desenvolvedor abre e preenche o arquivo. Este módulo carrega o
// arquivo no process.env e só informa NOMES de variáveis; nunca devolve nem
// imprime valores.
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';

export const ARQUIVO_PADRAO = '.env.producao.local';
export const URL_PADRAO = 'https://prisma.nordevs.com.br';
export const VARIAVEIS_OBRIGATORIAS = ['PROD_E2E_EMAIL', 'PROD_E2E_PASSWORD'];
/** Com valor `1`, aceita em `PROD_E2E_BASE_URL` outra URL https além de `URL_PADRAO`. */
export const VARIAVEL_PERMITIR_OUTRO_HOST = 'PROD_E2E_PERMITIR_OUTRO_HOST';
const VARIAVEIS_DO_ARQUIVO = [...VARIAVEIS_OBRIGATORIAS, 'PROD_E2E_BASE_URL'];

/** Caminho do arquivo de credenciais. `PROD_E2E_ENV_FILE` aponta outro arquivo (usado para provar a parada sem credencial). */
export function caminhoDasCredenciais(raizWeb) {
  return resolve(raizWeb, process.env.PROD_E2E_ENV_FILE || ARQUIVO_PADRAO);
}

/**
 * Carrega o arquivo de credenciais no process.env. O arquivo é a única fonte:
 * valores herdados de outro terminal são descartados antes.
 * Devolve só o caminho, se o arquivo existe e os NOMES das variáveis que faltam.
 */
export function carregarCredenciais(raizWeb) {
  const arquivo = caminhoDasCredenciais(raizWeb);
  for (const nome of VARIAVEIS_DO_ARQUIVO) delete process.env[nome];
  const existe = existsSync(arquivo);
  if (existe) process.loadEnvFile(arquivo);
  const faltando = VARIAVEIS_OBRIGATORIAS.filter(nome => !(process.env[nome] ?? '').trim());
  const invalidas = [];
  const base = (process.env.PROD_E2E_BASE_URL ?? '').trim();
  if (base && !enderecoAceito(base)) invalidas.push('PROD_E2E_BASE_URL');
  return { arquivo, existe, faltando, invalidas };
}

/**
 * `PROD_E2E_BASE_URL` só pode ser a produção (`URL_PADRAO`). Outra URL https só passa
 * com `PROD_E2E_PERMITIR_OUTRO_HOST=1`: um valor trocado por engano não leva a senha a outro host.
 */
function enderecoAceito(base) {
  let protocolo = '';
  try { protocolo = new URL(base).protocol; } catch { return false; }
  if (protocolo !== 'https:') return false;
  if (base.replace(/\/+$/, '') === URL_PADRAO) return true;
  return (process.env[VARIAVEL_PERMITIR_OUTRO_HOST] ?? '').trim() === '1';
}

/** Mensagem de parada: cita o arquivo e os nomes, nunca os valores. Devolve null quando está tudo certo. */
export function mensagemDeCredenciaisAusentes(estado) {
  if (estado.existe && estado.faltando.length === 0 && estado.invalidas.length === 0) return null;
  const linhas = ['Suíte de produção parada antes de abrir o navegador.'];
  if (!estado.existe) linhas.push(`Arquivo de credenciais não encontrado: ${estado.arquivo}`);
  if (estado.faltando.length) linhas.push(`Variável ausente ou vazia: ${estado.faltando.join(', ')}`);
  if (estado.invalidas.length) linhas.push(`Variável inválida (precisa ser ${URL_PADRAO}; outra URL https só com ${VARIAVEL_PERMITIR_OUTRO_HOST}=1): ${estado.invalidas.join(', ')}`);
  linhas.push('O desenvolvedor preenche o arquivo (veja e2e-producao/README.md). O agente não abre esse arquivo.');
  return linhas.join('\n');
}

/** Endereço da produção: `PROD_E2E_BASE_URL` do arquivo, ou o padrão, sem barra no fim. */
export function enderecoDaProducao() {
  return ((process.env.PROD_E2E_BASE_URL ?? '').trim() || URL_PADRAO).replace(/\/+$/, '');
}
