import { test as setup } from '@playwright/test';
import { enderecoDaProducao } from '../credenciais.mjs';
import {
  ARQUIVO_CONTEXTO, NOME_ORGANIZACAO, PREFIXO,
  chamarApi, gravarJson, lerSessao, nomeDoProjeto, normalizar,
  type ContextoProducao, type Organizacao,
} from './contexto';

type Projeto = { id: string; name: string; isArchived: boolean; boards: Array<{ id: string; name: string }> };
type Tarefa = { id: string; title: string; boardId: string; isArchived: boolean };

/**
 * Trava e limpeza antes de qualquer teste com navegador (CA-5, CA-6).
 *
 * Só lê e arquiva: nunca cria organização, projeto nem tarefa. Se a organização
 * ativa não for a "pessoal", se a conta enxergar mais de um projeto não arquivado,
 * ou o projeto de teste não for achado (ou for ambíguo), para aqui e os testes de
 * tela não rodam.
 */
setup('trava no projeto de teste e arquiva sobras [e2e-prod]', async () => {
  const testInfo = setup.info();
  const endereco = enderecoDaProducao();
  const { token, organizacaoId } = lerSessao();
  const api = { endereco, token, organizacaoId };

  const atual = await chamarApi<Organizacao>(api, 'GET', '/api/organizations/current');
  if (atual.id !== organizacaoId
    || (normalizar(atual.name) !== NOME_ORGANIZACAO && normalizar(atual.slug) !== NOME_ORGANIZACAO))
    throw new Error(`A organização ativa não é a "${NOME_ORGANIZACAO}". Suíte parada antes de criar qualquer coisa.`);

  const { nome, sobrescrito } = nomeDoProjeto();
  const projetos = await chamarApi<Projeto[]>(api, 'GET', '/api/projects');
  // A conta de teste é membro só do projeto de teste: se enxerga outro, um erro na trava poderia tocá-lo.
  const ativos = projetos.filter(projeto => !projeto.isArchived);
  if (ativos.length > 1)
    throw new Error(`A conta de teste enxerga outros projetos (${ativos.length} não arquivados); deixe-a membro só do projeto de teste. Suíte parada antes de criar qualquer coisa.`);
  const achados = projetos.filter(projeto => !projeto.isArchived && projeto.name.trim() === nome);
  if (sobrescrito && achados.length > 0)
    throw new Error('PROD_E2E_PROJETO só serve para provar a parada com um nome que não existe. Suíte parada.');
  if (achados.length === 0)
    throw new Error(`Projeto "${nome}" não encontrado na organização "${NOME_ORGANIZACAO}". Suíte parada antes de criar qualquer coisa; ela nunca cria projeto.`);
  if (achados.length > 1)
    throw new Error(`Há ${achados.length} projetos ativos chamados "${nome}". Suíte parada: o projeto de teste precisa ser único.`);

  const projeto = await chamarApi<Projeto>(api, 'GET', `/api/projects/${achados[0].id}`);
  if (!projeto.boards.length) throw new Error(`O projeto "${nome}" não tem quadro. Suíte parada; ela nunca cria quadro.`);
  const quadroIds = projeto.boards.map(quadro => quadro.id);

  // Sobras: tarefas abertas (não arquivadas) com o prefixo, deixadas por uma execução que caiu.
  const abertas = await chamarApi<Tarefa[]>(api, 'GET', `/api/WorkItems/project/${projeto.id}`);
  const sobras = abertas.filter(tarefa => !tarefa.isArchived && tarefa.title.startsWith(PREFIXO));
  for (const sobra of sobras) {
    if (!quadroIds.includes(sobra.boardId)) throw new Error('Sobra fora dos quadros do projeto de teste. Suíte parada.');
    await chamarApi<void>(api, 'DELETE', `/api/WorkItems/${sobra.id}`);
  }
  console.log(`[e2e-prod] Sobras com o prefixo ${PREFIXO} arquivadas antes de testar: ${sobras.length}`);
  testInfo.annotations.push({ type: 'sobras-arquivadas', description: String(sobras.length) });

  const contexto: ContextoProducao = {
    enderecoBase: endereco,
    organizacaoId,
    projetoId: projeto.id,
    projetoNome: projeto.name,
    quadroIds,
    sobrasArquivadas: sobras.length,
    preparadoEm: new Date().toISOString(),
  };
  gravarJson(ARQUIVO_CONTEXTO, contexto);
});
