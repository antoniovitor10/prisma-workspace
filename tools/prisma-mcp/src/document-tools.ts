// Ferramentas MCP de documento e sincronização (T-0023, CA-12). Ficam fora do server.ts para reduzir
// conflito com a HU-0007; seguem o mesmo padrão: erro sem token, 401/403 com "pare e avise".
import type { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { z } from 'zod';
import type { ToolExecutor } from './server.js';
import { SECTION_CLASS_LABEL, type SectionChoice } from './sections.js';
import { describeReport, type MethodDocumentService, type SyncReport } from './sync.js';

const documentKey = z.string().trim().regex(/^(EPIC-\d{4,}|HU-\d{4,}|AVULSAS|HU-\d{4,}\/prd|T-\d{4,})$/,
  'Use EPIC-0001, HU-0001, HU-0001/prd, AVULSAS ou T-0001.');
const taskId = z.string().regex(/^T-\d{4,}$/, 'Use o ID do método, como T-0001.');
const path = z.string().trim().min(1, 'Informe o caminho do arquivo.');
const choice = z.union([z.enum(['repo', 'prisma']), z.object({ juntar: z.string() }).strict()]);

const readOnly = { readOnlyHint: true, destructiveHint: false, openWorldHint: false };
const write = { readOnlyHint: false, destructiveHint: false, openWorldHint: false };

function reportData(report: SyncReport): Record<string, unknown> {
  return report as unknown as Record<string, unknown>;
}

export function registerDocumentTools(server: McpServer, execute: ToolExecutor, documents: MethodDocumentService): void {
  server.registerTool('obter_documento', {
    description: 'Lê um documento do método no Prisma (épico, HU, PRD ou corpo da task): texto, versão, hash, autor, canal e status da HU.',
    inputSchema: { chave: documentKey }, annotations: readOnly,
  }, ({ chave }) => execute(async () => {
    const doc = await documents.obterDocumento(chave);
    return {
      summary: `${doc.key} · v${doc.version} · ${doc.kind}${doc.status ? ` · ${doc.status}` : ''} · por ${doc.updatedByName} (${doc.updatedVia}) em ${doc.updatedAtUtc}`,
      data: { documento: doc },
    };
  }));

  server.registerTool('listar_mudancas', {
    description: 'Lista os metadados (sem texto) dos documentos do projeto e marca os que mudaram no Prisma desde a última sincronização (.prisma-sync.json) ou desde a data informada.',
    inputSchema: { desde: z.string().trim().min(1).optional() }, annotations: readOnly,
  }, ({ desde }) => execute(async () => {
    const result = await documents.listarMudancas(desde);
    const changed = result.documentos.filter(d => d.mudou_no_prisma);
    return {
      summary: `${result.total} documentos; ${result.mudaram} mudaram no Prisma${changed.length ? `: ${changed.map(d => `${d.key} v${d.version}`).join(', ')}` : '.'}`,
      data: result as unknown as Record<string, unknown>,
    };
  }));

  server.registerTool('enviar_documento', {
    description: 'Lê o arquivo (dentro de PRISMA_METODO_DIR) e cria ou atualiza o documento no Prisma: cria Épico/História na primeira vez e põe a task sob a História. Nunca grava o arquivo; se o Prisma mudou, avisa; se mudou nos dois lados, devolve a diferença por seção.',
    inputSchema: { caminho: path }, annotations: write,
  }, ({ caminho }) => execute(async () => {
    const report = await documents.enviarDocumento(caminho);
    return { summary: describeReport(report), data: reportData(report) };
  }));

  server.registerTool('sincronizar', {
    description: 'Aplica a regra de sincronização a um arquivo, a uma HU com PRD e tasks (escopo HU-0001), às avulsas (AVULSAS) ou a tudo (sem escopo), na ordem épico → HU → PRD → task. ensaio: true só mostra o que faria. Documento só num lado é listado; criar_no_prisma/criar_no_repo criam no outro lado.',
    inputSchema: {
      escopo: z.string().trim().min(1).optional(),
      ensaio: z.boolean().optional(),
      criar_no_prisma: z.boolean().optional(),
      criar_no_repo: z.boolean().optional(),
    }, annotations: write,
  }, ({ escopo, ensaio, criar_no_prisma, criar_no_repo }) => execute(async () => {
    const report = await documents.sincronizar({ escopo, ensaio, criarNoPrisma: criar_no_prisma, criarNoRepo: criar_no_repo });
    return { summary: describeReport(report), data: reportData(report) };
  }));

  server.registerTool('resolver_conflito', {
    description: 'Grava nos dois lados a resolução de um conflito: para cada seção que não é igual, "repo", "prisma" ou { "juntar": "<texto da seção inteira, com a linha ## , aprovado pelo desenvolvedor>" }. A seção antes do primeiro ## chama "cabeçalho".',
    inputSchema: { chave: documentKey, escolhas: z.record(z.string(), choice) }, annotations: write,
  }, ({ chave, escolhas }) => execute(async () => {
    const result = await documents.resolverConflito(chave, escolhas as Record<string, SectionChoice>);
    return {
      summary: `Conflito resolvido em ${result.chave}: arquivo e Prisma na v${result.versao}.\n${result.secoes
        .filter(s => s.classe !== 'igual').map(s => `${s.secao} (${SECTION_CLASS_LABEL[s.classe]}): ${s.escolha}`).join('\n')}`,
      data: result as unknown as Record<string, unknown>,
    };
  }));

  server.registerTool('revisoes_documento', {
    description: 'Lista as versões de um documento (autor, data, canal); com versao, devolve o texto daquela versão.',
    inputSchema: { chave: documentKey, versao: z.number().int().min(1).optional() }, annotations: readOnly,
  }, ({ chave, versao }) => execute(async () => {
    const result = await documents.revisoesDocumento(chave, versao);
    if (result.revisao) {
      const r = result.revisao;
      return { summary: `${chave} · v${r.version} · por ${r.authorName} (${r.via}) em ${r.createdAtUtc}`, data: { revisao: r } };
    }
    const list = result.revisoes ?? [];
    return {
      summary: list.length ? list.map(r => `v${r.version} · ${r.authorName} (${r.via}) · ${r.createdAtUtc}`).join('\n') : 'Nenhuma revisão.',
      data: { revisoes: list, total: list.length },
    };
  }));

  server.registerTool('vincular_hu', {
    description: 'Religa uma task existente à História da HU (ou a AVULSAS), sem trocar a identidade da task nem o status.',
    inputSchema: { id_task: taskId, chave_hu: z.string().trim().regex(/^(HU-\d{4,}|AVULSAS|avulsa)$/, 'Use HU-0001 ou AVULSAS.') }, annotations: write,
  }, ({ id_task, chave_hu }) => execute(async () => {
    const result = await documents.vincularHu(id_task, chave_hu);
    return { summary: `${result.taskId} agora está sob ${result.huKey}.`, data: { vinculo: result } };
  }));
}
