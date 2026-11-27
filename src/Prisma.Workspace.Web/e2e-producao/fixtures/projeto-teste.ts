import { expect, test as base, type Page, type Request, type TestInfo } from '@playwright/test';
import { NOME_ORGANIZACAO, PREFIXO, lerContexto, type ContextoProducao } from './contexto';

/**
 * Fixture dos testes de tela em produção.
 *
 * Trava de escrita (CA-5): toda requisição à origem da produção é interceptada.
 * Leitura (GET, HEAD, OPTIONS) passa. Escrita fora de `/api/` só passa na lista
 * fechada `ESCRITAS_FORA_DA_API` (negociação do SignalR). Escrita em `/api/` passa
 * por outra lista fechada. Só é permitido criar tarefa com o prefixo num quadro do projeto de
 * teste e mover, editar ou arquivar tarefas que a própria suíte criou. Qualquer
 * outra escrita (criar organização ou projeto, mudar configuração, tocar outra
 * tarefa) é abortada antes de sair do navegador e reprova o teste.
 */

type TarefaAberta = { id: string; title: string; boardId: string; stageId: string | null; isArchived: boolean };

export type Producao = {
  contexto: ContextoProducao;
  /** Título de tarefa de teste: prefixo, data e hora e a tela (desktop ou celular). */
  tituloDeTeste: () => string;
  /** GET autenticado na API de produção, pela sessão do navegador. */
  apiGet: <T>(caminho: string) => Promise<T>;
  /** Tarefas abertas com o prefixo no projeto de teste. */
  tarefasAbertasComPrefixo: () => Promise<TarefaAberta[]>;
  /** Abre /projects, confere a organização ativa e entra no projeto de teste pelo cartão. */
  abrirProjeto: () => Promise<void>;
  /** Print de etapa, anexado ao relatório e copiado para a pasta de evidências. */
  print: (nome: string) => Promise<void>;
};

type Fixtures = { producao: Producao };

const METODOS_DE_LEITURA = new Set(['GET', 'HEAD', 'OPTIONS']);
/**
 * Lista FECHADA de escritas permitidas fora de `/api/`: só a negociação do SignalR
 * (`POST /hubs/<hub>/negotiate`). Ampliá-la é mudança de segurança revisada.
 */
const ESCRITAS_FORA_DA_API: ReadonlyArray<{ metodo: string; caminho: RegExp }> = [
  { metodo: 'POST', caminho: /^\/hubs\/[a-z0-9-]+\/negotiate$/ },
];

function tela(info: TestInfo): string {
  return info.project.name === 'celular' ? 'celular' : 'desktop';
}

function carimbo(): string {
  const agora = new Date();
  const dois = (n: number) => String(n).padStart(2, '0');
  return `${agora.getFullYear()}-${dois(agora.getMonth() + 1)}-${dois(agora.getDate())} ${dois(agora.getHours())}:${dois(agora.getMinutes())}:${dois(agora.getSeconds())}`;
}

async function instalarTravaDeEscrita(page: Page, contexto: ContextoProducao, violacoes: string[]): Promise<void> {
  const rastreadas = new Set<string>();
  const descrever = (request: Request) => `${request.method()} ${new URL(request.url()).pathname}`;

  const origem = new URL(contexto.enderecoBase).origin;
  await page.context().route(url => url.origin === origem, async route => {
    const request = route.request();
    const metodo = request.method();
    if (METODOS_DE_LEITURA.has(metodo)) return route.continue();

    const caminho = new URL(request.url()).pathname.toLowerCase().replace(/\/+$/, '');
    const bloquear = async (motivo: string) => {
      violacoes.push(`${descrever(request)}: ${motivo}`);
      await route.abort('blockedbyclient');
    };

    if (!caminho.startsWith('/api/')) {
      if (ESCRITAS_FORA_DA_API.some(item => item.metodo === metodo && item.caminho.test(caminho))) return route.continue();
      return bloquear('escrita fora de /api/ que não está na lista fechada');
    }

    if (caminho === '/api/auth/refresh' && metodo === 'POST') return route.continue();
    if (request.headers()['x-organization-id'] !== contexto.organizacaoId)
      return bloquear(`organização diferente da "${NOME_ORGANIZACAO}"`);

    if (caminho === '/api/workitems' && metodo === 'POST') {
      const corpo = request.postDataJSON() as { boardId?: string; title?: string } | null;
      if (!corpo?.boardId || !contexto.quadroIds.includes(corpo.boardId)) return bloquear('quadro fora do projeto de teste');
      if (!corpo.title?.startsWith(PREFIXO)) return bloquear(`título sem o prefixo ${PREFIXO}`);
      const resposta = await route.fetch();
      const texto = await resposta.text();
      if (resposta.ok()) rastreadas.add(String(JSON.parse(texto)).toLowerCase());
      return route.fulfill({ response: resposta, body: texto });
    }

    if (caminho === '/api/workitems/move' && metodo === 'POST') {
      const corpo = request.postDataJSON() as { workItemId?: string } | null;
      if (!corpo?.workItemId || !rastreadas.has(corpo.workItemId.toLowerCase())) return bloquear('tarefa que a suíte não criou');
      return route.continue();
    }

    const tarefa = /^\/api\/workitems\/([0-9a-f-]{36})(?:\/[a-z-]+)?$/.exec(caminho);
    if (tarefa && ['PUT', 'DELETE'].includes(metodo)) {
      if (!rastreadas.has(tarefa[1])) return bloquear('tarefa que a suíte não criou');
      return route.continue();
    }

    return bloquear('escrita fora da lista permitida');
  });
}

export const test = base.extend<Fixtures>({
  producao: async ({ page }, usar, testInfo) => {
    const contexto = lerContexto();
    const violacoes: string[] = [];
    await instalarTravaDeEscrita(page, contexto, violacoes);
    let prints = 0;

    const apiGet = async <T>(caminho: string): Promise<T> => page.evaluate(async ({ base, alvo }) => {
      const token = localStorage.getItem('prisma_workspace_token');
      const organizacao = localStorage.getItem('prisma_workspace_organization');
      if (!token || !organizacao) throw new Error('Sessão de produção não disponível no navegador.');
      const resposta = await fetch(`${base}${alvo}`, { headers: { Authorization: `Bearer ${token}`, 'X-Organization-Id': organizacao } });
      if (!resposta.ok) throw new Error(`GET ${alvo} respondeu ${resposta.status}.`);
      return resposta.json();
    }, { base: contexto.enderecoBase, alvo: caminho }) as Promise<T>;

    const producao: Producao = {
      contexto,
      tituloDeTeste: () => `${PREFIXO} ${carimbo()} ${tela(testInfo)}`,
      apiGet,
      tarefasAbertasComPrefixo: async () => (await apiGet<TarefaAberta[]>(`/api/WorkItems/project/${contexto.projetoId}`))
        .filter(tarefa => !tarefa.isArchived && tarefa.title.startsWith(PREFIXO)),
      abrirProjeto: async () => {
        await page.goto('/projects', { waitUntil: 'domcontentloaded' });
        const cartaoDoProjeto = page.getByRole('heading', { name: contexto.projetoNome, exact: true });
        await expect(cartaoDoProjeto).toBeVisible();
        await expect(page).not.toHaveURL(/\/login(?:[/?#]|$)/);
        const organizacaoAtiva = await page.evaluate(() => localStorage.getItem('prisma_workspace_organization'));
        expect(organizacaoAtiva, `A organização ativa no navegador precisa ser a "${NOME_ORGANIZACAO}".`).toBe(contexto.organizacaoId);
        await cartaoDoProjeto.click();
        await expect(page).toHaveURL(new RegExp(`/projects/${contexto.projetoId}/backlog`));
      },
      print: async (nome: string) => {
        prints += 1;
        const arquivo = testInfo.outputPath(`${String(prints).padStart(2, '0')}-${nome}.png`);
        await page.screenshot({ path: arquivo, scale: 'css' });
        await testInfo.attach(`print ${String(prints).padStart(2, '0')}-${nome}`, { path: arquivo, contentType: 'image/png' });
      },
    };

    await usar(producao);

    expect(violacoes, 'Escrita bloqueada pela trava do projeto de teste.').toEqual([]);
  },
});

export { expect } from '@playwright/test';
