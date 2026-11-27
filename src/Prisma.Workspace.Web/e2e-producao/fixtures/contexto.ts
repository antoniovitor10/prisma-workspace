import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** Prefixo de toda tarefa criada pela suíte de produção. A limpeza de sobras só toca tarefas com ele. */
export const PREFIXO = '[e2e-prod]';
/** Organização onde a suíte pode agir. Achada pela API pelo nome (ou slug), nunca fixada por id. */
export const NOME_ORGANIZACAO = 'pessoal';
/** Único projeto onde a suíte cria, move e arquiva. A suíte nunca cria organização nem projeto. */
export const NOME_PROJETO_PADRAO = 'Teste de produção (agente)';

const pastaAtual = dirname(fileURLToPath(import.meta.url));
export const PASTA_AUTH = join(pastaAtual, '..', '.auth');
/** storageState com o token e a organização "pessoal" (arquivo ignorado pelo git; o agente não abre). */
export const ARQUIVO_SESSAO = join(PASTA_AUTH, 'sessao.json');
/** Ids descobertos na preparação (organização, projeto, quadros) e a contagem de sobras. Sem segredo. */
export const ARQUIVO_CONTEXTO = join(PASTA_AUTH, 'contexto.json');

/** Organização como a API devolve em `GET /api/organizations` e `/api/organizations/current`. */
export type Organizacao = { id: string; name: string; slug: string; isActive: boolean };

export type ContextoProducao = {
  enderecoBase: string;
  organizacaoId: string;
  projetoId: string;
  projetoNome: string;
  quadroIds: string[];
  sobrasArquivadas: number;
  preparadoEm: string;
};

/**
 * Nome do projeto de teste. `PROD_E2E_PROJETO` existe só para provar a parada (CA-5):
 * a preparação recusa a sobrescrita se um projeto com esse nome existir, então ela
 * nunca serve para agir em outro projeto.
 */
export function nomeDoProjeto(): { nome: string; sobrescrito: boolean } {
  const sobrescrito = (process.env.PROD_E2E_PROJETO ?? '').trim();
  return sobrescrito ? { nome: sobrescrito, sobrescrito: true } : { nome: NOME_PROJETO_PADRAO, sobrescrito: false };
}

export function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim().toLowerCase();
}

export function gravarJson(arquivo: string, conteudo: unknown): void {
  mkdirSync(dirname(arquivo), { recursive: true });
  writeFileSync(arquivo, JSON.stringify(conteudo, null, 2));
}

export function lerContexto(): ContextoProducao {
  if (!existsSync(ARQUIVO_CONTEXTO)) throw new Error('Contexto da preparação ausente: rode a suíte pelo comando npm run e2e:producao.');
  return JSON.parse(readFileSync(ARQUIVO_CONTEXTO, 'utf8')) as ContextoProducao;
}

/** Token e organização salvos pelo login. Lidos só pelo código; nunca impressos. */
export function lerSessao(): { token: string; organizacaoId: string } {
  if (!existsSync(ARQUIVO_SESSAO)) throw new Error('Sessão de produção ausente: o login pela API não rodou.');
  const sessao = JSON.parse(readFileSync(ARQUIVO_SESSAO, 'utf8')) as {
    origins: Array<{ localStorage: Array<{ name: string; value: string }> }>;
  };
  const itens = sessao.origins.flatMap(origem => origem.localStorage);
  const token = itens.find(item => item.name === 'prisma_workspace_token')?.value;
  const organizacaoId = itens.find(item => item.name === 'prisma_workspace_organization')?.value;
  if (!token || !organizacaoId) throw new Error('Sessão de produção incompleta (token ou organização).');
  return { token, organizacaoId };
}

type Chamada = { endereco: string; token: string; organizacaoId?: string };

/**
 * Chamada à API de produção a partir do Node (login e preparação). A mensagem de
 * erro traz só método, caminho e status: nunca o corpo nem cabeçalhos.
 */
export async function chamarApi<T>(chamada: Chamada, metodo: 'GET' | 'DELETE', caminho: string): Promise<T> {
  const resposta = await fetch(`${chamada.endereco}${caminho}`, {
    method: metodo,
    headers: {
      Authorization: `Bearer ${chamada.token}`,
      ...(chamada.organizacaoId ? { 'X-Organization-Id': chamada.organizacaoId } : {}),
    },
  });
  if (!resposta.ok) throw new Error(`${metodo} ${caminho} respondeu ${resposta.status}.`);
  if (resposta.status === 204) return undefined as T;
  const texto = await resposta.text();
  return (texto ? JSON.parse(texto) : undefined) as T;
}
