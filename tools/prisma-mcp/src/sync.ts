// Sincronização dos arquivos do método com os documentos do Prisma (T-0023, PRD "Sincronização").
// O MCP compara e grava (D2): o texto nunca passa pela geração do modelo. A regra por documento é uma
// função pura (decideSync); o estado da última sincronização fica em `.prisma-sync.json` (D5).
// Nada aqui escreve conteúdo de documento em stderr ou log; o token fica só no cliente da API.
import { lstat, readFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { PrismaApi, PrismaApiError, type CreateMethodTaskDto, type MethodTaskDto } from './api.js';
import { readMetodoDir } from './config.js';
import {
  DocumentsClient, HU_KEY, assertDocumentKey, isAuthFailure, isNotFound, isVersionConflict,
  type MethodDocument, type MethodDocumentRevision, type MethodDocumentSummary,
} from './documents.js';
import {
  STATE_FILE, atomicWrite, readMetodoFile, resolveMetodoPath, writeMetodoFile,
  type MetodoFile, type SafePath,
} from './files.js';
import { AVULSAS_KEY, KIND_ORDER, scanMetodoDocuments, type DocumentKind, type LocalDocument } from './metodo.js';
import { SECTION_CLASS_LABEL, assembleResolution, diffSections, type SectionChoice, type SectionDiff } from './sections.js';

// ---------------------------------------------------------------------------------------------
// Estado da última sincronização

export interface SyncRecord { chave: string; tipo: DocumentKind; workItemId: string; versao: number; hash: string; }
export interface SyncState { versao: 1; documentos: Record<string, SyncRecord>; }

export async function readSyncState(root: string): Promise<SyncState> {
  const path = join(resolve(root), STATE_FILE);
  let text: string;
  try {
    if ((await lstat(path)).isSymbolicLink()) throw new Error(`${STATE_FILE} não pode ser link simbólico.`);
    text = await readFile(path, 'utf8');
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return { versao: 1, documentos: {} };
    throw error;
  }
  let parsed: unknown;
  try { parsed = JSON.parse(text.replace(/^﻿/, '')); } catch { throw new Error(`${STATE_FILE} não é JSON válido; corrija ou apague o arquivo e sincronize de novo.`); }
  const documentos: Record<string, SyncRecord> = {};
  const raw = (parsed as { documentos?: unknown })?.documentos;
  if (!raw || typeof raw !== 'object') throw new Error(`${STATE_FILE} sem o mapa "documentos".`);
  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    const r = value as Partial<SyncRecord>;
    if (!/^(EPIC-\d{4,}|HU-\d{4,}|HU-\d{4,}\/prd|T-\d{4,})$/.test(key) || r?.chave !== key || typeof r.versao !== 'number'
      || typeof r.hash !== 'string' || typeof r.workItemId !== 'string' || !['epic', 'hu', 'prd', 'task'].includes(r.tipo as string)) {
      throw new Error(`${STATE_FILE}: registro inválido para ${key}.`);
    }
    documentos[key] = { chave: key, tipo: r.tipo as DocumentKind, workItemId: r.workItemId, versao: r.versao, hash: r.hash };
  }
  return { versao: 1, documentos };
}

/** Grava o estado de forma atômica (temporário + renomear), com chaves em ordem e LF. */
export async function writeSyncState(root: string, state: SyncState): Promise<void> {
  const documentos: Record<string, SyncRecord> = {};
  for (const key of Object.keys(state.documentos).sort()) {
    const r = state.documentos[key];
    documentos[key] = { chave: r.chave, tipo: r.tipo, workItemId: r.workItemId, versao: r.versao, hash: r.hash };
  }
  const path = await resolveMetodoPath(root, STATE_FILE, { mustExist: false, allowState: true });
  await atomicWrite(path.abs, `${JSON.stringify({ versao: 1, documentos }, null, 2)}\n`);
}

// ---------------------------------------------------------------------------------------------
// Regra por documento (determinística)

export type SyncAction =
  | 'nada' | 'enviar' | 'puxar' | 'atualizar_estado' | 'conflito'
  | 'so_no_repo' | 'so_no_prisma' | 'criar_estado' | 'sumiu_no_repo';
export interface RuleInput {
  /** Hash do arquivo agora (LF). */
  local?: { hash: string };
  /** Registro da última sincronização. */
  estado?: { versao: number; hash: string };
  /** Versão e hash no Prisma. */
  remoto?: { versao: number; hash: string };
}

/**
 * | repo mudou | Prisma mudou | ação |
 * | não | não | nada |
 * | sim | não | enviar (baseVersion = S.versao) |
 * | não | sim | puxar (grava o arquivo com o texto do Prisma) |
 * | sim | sim, mesmo texto | atualizar_estado |
 * | sim | sim, textos diferentes | conflito |
 * | sem S, só num lado | | so_no_repo / so_no_prisma (o desenvolvedor decide) |
 * | sem S, nos dois lados | | criar_estado se o texto é igual; senão conflito |
 * Com S e sem arquivo: sumiu_no_repo (só avisa; nunca apaga no Prisma).
 */
export function decideSync({ local, estado, remoto }: RuleInput): SyncAction {
  if (estado && !local) return 'sumiu_no_repo';
  if (!estado) {
    if (local && remoto) return local.hash === remoto.hash ? 'criar_estado' : 'conflito';
    if (local) return 'so_no_repo';
    if (remoto) return 'so_no_prisma';
    return 'nada';
  }
  if (!remoto) return 'so_no_repo';
  const repoChanged = local!.hash !== estado.hash;
  const prismaChanged = remoto.versao !== estado.versao;
  if (!repoChanged && !prismaChanged) return 'nada';
  if (repoChanged && !prismaChanged) return 'enviar';
  if (!repoChanged && prismaChanged) return 'puxar';
  return local!.hash === remoto.hash ? 'atualizar_estado' : 'conflito';
}

// ---------------------------------------------------------------------------------------------
// Execução

export interface SyncOptions {
  /** Sem escopo: tudo. Chave (`EPIC-0001`, `HU-0001` = HU com PRD e tasks, `HU-0001/prd`, `T-0001`, `AVULSAS`) ou caminho de um arquivo. */
  escopo?: string;
  /** Só mostra o que faria: nada é gravado no arquivo, no estado nem no Prisma. */
  ensaio?: boolean;
  /** Documento só no repo: cria no Prisma (História, PRD, Épico, task e corpo). Sem isso, só lista. */
  criarNoPrisma?: boolean;
  /** Documento só no Prisma: grava o arquivo no `filePath` dele. Sem isso, só lista. */
  criarNoRepo?: boolean;
}
export type SyncStep = 'criar' | 'criar_task' | 'religar' | 'enviar' | 'puxar' | 'gravar_arquivo' | 'atualizar_estado' | 'criar_estado';
export interface SyncItem {
  chave: string;
  tipo: DocumentKind;
  arquivo: string | null;
  acao: SyncAction | 'avulsas';
  /** O que foi feito (ou seria, no ensaio), na ordem. */
  passos: SyncStep[];
  executado: boolean;
  /** Em `religar`: a História de destino. */
  religar_para?: string;
  versao?: number;
  aviso?: string;
  erro?: string;
  diferenca?: SectionDiff[];
}
export interface SyncReport {
  ensaio: boolean;
  escopo: string;
  pasta: string;
  itens: SyncItem[];
  erros_locais: { arquivo: string; erro: string }[];
  resumo: Record<string, number>;
}

/** História "Avulsas" (D11): sem arquivo no repositório, documento curto e fixo. */
export const AVULSAS_CONTENT = '---\nid: AVULSAS\ntitulo: "Avulsas"\n---\n\n# Avulsas\n\nTasks do método sem HU (`hu: avulsa`), em `docs/trabalho/avulsas/tasks/`.\n';

interface Entry { key: string; kind: DocumentKind; local?: LocalDocument; }
interface Context {
  root: string;
  options: SyncOptions & { somenteEnviar?: boolean };
  state: SyncState;
  remote: Map<string, MethodDocumentSummary>;
  /** Chaves que passariam a existir no Prisma (ensaio) ou já foram criadas nesta execução. */
  planned: Set<string>;
  fullDocs: Map<string, MethodDocument>;
  methodTasks?: Map<string, MethodTaskDto>;
  stateDirty: boolean;
}

const STATUS_AT_CREATION = new Set(['backlog', 'concluida']);
const MAX_ATTEMPTS = 3;

export class MethodDocumentService {
  readonly docs: DocumentsClient;
  readonly root: string;
  constructor(readonly api: PrismaApi, root: string = readMetodoDir()) {
    this.docs = new DocumentsClient(api);
    this.root = resolve(root);
  }

  // ---- Ferramentas simples -------------------------------------------------------------------

  async obterDocumento(chave: string): Promise<MethodDocument> {
    return this.docs.get(assertDocumentKey(chave));
  }

  async listarMudancas(desde?: string) {
    if (desde !== undefined && !Number.isFinite(Date.parse(desde))) throw new Error('Data inválida em "desde"; use ISO 8601, como 2026-11-12T00:00:00Z.');
    const [summaries, state] = await Promise.all([this.docs.list({ desde }), readSyncState(this.root)]);
    const documentos = summaries.map(s => {
      const record = state.documentos[s.key];
      return { ...s, versao_sincronizada: record?.versao ?? null,
        mudou_no_prisma: desde !== undefined ? true : !record || record.versao !== s.version };
    }).sort((a, b) => a.key.localeCompare(b.key));
    return { documentos, total: documentos.length, mudaram: documentos.filter(d => d.mudou_no_prisma).length };
  }

  async revisoesDocumento(chave: string, versao?: number): Promise<{ revisoes?: MethodDocumentRevision[]; revisao?: MethodDocumentRevision }> {
    assertDocumentKey(chave);
    if (versao === undefined) return { revisoes: await this.docs.revisions(chave) };
    return { revisao: await this.docs.revision(chave, versao) };
  }

  async vincularHu(idTask: string, chaveHu: string) {
    const huKey = chaveHu === 'avulsa' ? AVULSAS_KEY : chaveHu;
    if (!/^T-\d{4,}$/.test(idTask)) throw new Error('ID da task inválido. Use T-0001.');
    if (!HU_KEY.test(huKey)) throw new Error('HU inválida. Use HU-0001 ou AVULSAS.');
    return this.docs.relink(idTask, huKey);
  }

  // ---- Sincronização -------------------------------------------------------------------------

  /** Aplica a regra a um arquivo, a uma HU (com PRD e tasks), às avulsas ou a tudo. */
  async sincronizar(options: SyncOptions = {}): Promise<SyncReport> {
    return this.run(options);
  }

  /**
   * Lê o arquivo e cria ou atualiza no Prisma. Nunca grava o `.md`: se o Prisma mudou, avisa para
   * rodar `sincronizar`; se mudou nos dois lados, devolve a diferença por seção.
   */
  async enviarDocumento(caminho: string): Promise<SyncReport> {
    // A guarda de caminho vem antes de qualquer leitura ou chamada à API.
    await resolveMetodoPath(this.root, caminho);
    return this.run({ escopo: caminho, criarNoPrisma: true, somenteEnviar: true });
  }

  private async run(options: SyncOptions & { somenteEnviar?: boolean }): Promise<SyncReport> {
    const escopo = options.escopo?.trim() || undefined;
    let scopePath: SafePath | undefined;
    if (escopo !== undefined && !/^(EPIC-\d{4,}|HU-\d{4,}|AVULSAS|HU-\d{4,}\/prd|T-\d{4,})$/.test(escopo)) {
      scopePath = await resolveMetodoPath(this.root, escopo);
    }
    const scan = await scanMetodoDocuments(this.root);
    let scoped = scan.documents;
    if (scopePath) {
      const doc = scan.documents.find(d => d.rel === scopePath!.rel);
      if (!doc) {
        const erro = scan.erros.find(e => e.arquivo === scopePath!.rel);
        throw new Error(erro ? `${scopePath.rel}: ${erro.erro}` : `${scopePath.rel} não é épico, HU, PRD ou task do método.`);
      }
      scoped = [doc];
    }
    const ctx: Context = {
      root: this.root, options, state: await readSyncState(this.root),
      remote: new Map((await this.docs.list()).map(s => [s.key, s])), planned: new Set(), fullDocs: new Map(), stateDirty: false,
    };
    const huScope = escopo && /^HU-\d{4,}$/.test(escopo) ? escopo : undefined;
    const inScope = (key: string, kind: DocumentKind, parentKey: string | null): boolean => {
      if (scopePath) return scoped.some(d => d.key === key);
      if (escopo === undefined) return true;
      if (huScope) return key === huScope || key === `${huScope}/prd` || (kind === 'task' && parentKey === huScope);
      if (escopo === AVULSAS_KEY) return kind === 'task' && parentKey === AVULSAS_KEY;
      return key === escopo;
    };
    const local = scoped.filter(d => inScope(d.key, d.kind, d.parentKey));
    const localKeys = new Set(local.map(d => d.key));
    const entries: Entry[] = local.map(d => ({ key: d.key, kind: d.kind, local: d }));
    if (!scopePath) {
      // Tasks só no Prisma: a HU delas vem do campo HU da task do método.
      const remoteTaskHu = async (key: string) => {
        const task = (await this.methodTasks(ctx)).get(key);
        return task?.hu === 'avulsa' ? AVULSAS_KEY : task?.hu ?? null;
      };
      const allLocalKeys = new Set(scan.documents.map(d => d.key));
      for (const summary of ctx.remote.values()) {
        if (summary.key === AVULSAS_KEY || allLocalKeys.has(summary.key)) continue;
        const parent = summary.kind === 'task' && (huScope || escopo === AVULSAS_KEY) ? await remoteTaskHu(summary.key) : null;
        if (inScope(summary.key, summary.kind, parent)) entries.push({ key: summary.key, kind: summary.kind });
      }
      for (const record of Object.values(ctx.state.documentos)) {
        if (localKeys.has(record.chave) || allLocalKeys.has(record.chave) || entries.some(e => e.key === record.chave)) continue;
        if (escopo === undefined || record.chave === escopo || (huScope && record.chave === `${huScope}/prd`)) entries.push({ key: record.chave, kind: record.tipo });
      }
    }
    entries.sort((a, b) => KIND_ORDER[a.kind] - KIND_ORDER[b.kind] || a.key.localeCompare(b.key));

    const itens: SyncItem[] = [];
    // Épicos → HUs (e Avulsas) → PRDs → tasks: o pai existe antes do filho.
    let avulsasHandled = false;
    for (const entry of entries) {
      if (!avulsasHandled && KIND_ORDER[entry.kind] > KIND_ORDER.hu) {
        avulsasHandled = true;
        const avulsas = await this.avulsasItem(ctx, local);
        if (avulsas) itens.push(avulsas);
      }
      itens.push(await this.syncEntry(ctx, entry));
    }
    if (!avulsasHandled) {
      const avulsas = await this.avulsasItem(ctx, local);
      if (avulsas) itens.push(avulsas);
    }
    if (ctx.stateDirty && !options.ensaio) await writeSyncState(this.root, ctx.state);
    const resumo: Record<string, number> = {};
    for (const item of itens) resumo[item.acao] = (resumo[item.acao] ?? 0) + 1;
    resumo.erros = itens.filter(i => i.erro).length + (scopePath ? 0 : scan.erros.length);
    return { ensaio: options.ensaio === true, escopo: escopo ?? 'tudo', pasta: this.root, itens,
      erros_locais: scopePath ? [] : scan.erros, resumo };
  }

  private async methodTasks(ctx: Context): Promise<Map<string, MethodTaskDto>> {
    if (!ctx.methodTasks) ctx.methodTasks = new Map((await this.api.listMethodTasks()).map(t => [t.id, t]));
    return ctx.methodTasks;
  }

  private exists(ctx: Context, key: string): boolean { return ctx.remote.has(key) || ctx.planned.has(key); }

  private setState(ctx: Context, key: string, kind: DocumentKind, workItemId: string, versao: number, hash: string): void {
    ctx.state.documentos[key] = { chave: key, tipo: kind, workItemId, versao, hash };
    ctx.stateDirty = true;
  }

  private async persist(ctx: Context): Promise<void> {
    if (ctx.options.ensaio || !ctx.stateDirty) return;
    await writeSyncState(ctx.root, ctx.state);
    ctx.stateDirty = false;
  }

  /** Cria a História "Avulsas" quando há task avulsa no escopo que vai (ou já está) no Prisma. */
  private async avulsasItem(ctx: Context, local: LocalDocument[]): Promise<SyncItem | undefined> {
    const avulsas = local.filter(d => d.kind === 'task' && d.parentKey === AVULSAS_KEY);
    if (!avulsas.length || this.exists(ctx, AVULSAS_KEY)) return undefined;
    const tasks = await this.methodTasks(ctx);
    if (!ctx.options.criarNoPrisma && !avulsas.some(d => tasks.has(d.key))) return undefined;
    const item: SyncItem = { chave: AVULSAS_KEY, tipo: 'hu', arquivo: null, acao: 'avulsas', passos: ['criar'], executado: false };
    if (ctx.options.ensaio) { ctx.planned.add(AVULSAS_KEY); return item; }
    try {
      const doc = await this.docs.put(AVULSAS_KEY, { content: AVULSAS_CONTENT, baseVersion: 0 });
      ctx.remote.set(AVULSAS_KEY, doc); ctx.planned.add(AVULSAS_KEY);
      item.executado = true; item.versao = doc.version;
    } catch (error) {
      if (isAuthFailure(error)) throw error;
      if (isVersionConflict(error)) { ctx.planned.add(AVULSAS_KEY); item.aviso = 'A História AVULSAS já existia no Prisma.'; }
      else item.erro = message(error);
    }
    return item;
  }

  private async syncEntry(ctx: Context, entry: Entry): Promise<SyncItem> {
    const { key, kind, local } = entry;
    const item: SyncItem = { chave: key, tipo: kind, arquivo: local ? `docs/trabalho/${local.rel}` : ctx.remote.get(key)?.filePath ?? null,
      acao: 'nada', passos: [], executado: false };
    try {
      let path: SafePath | undefined;
      let file: MetodoFile | undefined;
      if (local) {
        path = await resolveMetodoPath(ctx.root, local.rel);
        file = await readMetodoFile(path);
      }
      if (kind === 'task' && local) await this.ensureTask(ctx, item, local);
      for (let attempt = 1; ; attempt++) {
        const remote = ctx.remote.get(key);
        const record = ctx.state.documentos[key];
        item.acao = decideSync({
          local: file && { hash: file.hash }, estado: record && { versao: record.versao, hash: record.hash },
          remoto: remote && { versao: remote.version, hash: remote.contentHash },
        });
        try {
          await this.apply(ctx, item, { key, kind, local, path, file, remote, record });
          break;
        } catch (error) {
          // 409: o Prisma mudou entre a leitura e o envio; trata como "Prisma mudou" e compara de novo.
          if (!isVersionConflict(error) || attempt >= MAX_ATTEMPTS) throw error;
          const fresh = await this.docs.find(key);
          if (fresh) { ctx.remote.set(key, fresh); ctx.fullDocs.set(key, fresh); } else ctx.remote.delete(key);
          item.passos = item.passos.filter(p => p === 'criar_task' || p === 'religar');
        }
      }
      await this.persist(ctx);
    } catch (error) {
      if (isAuthFailure(error)) throw error;
      item.erro = message(error);
    }
    return item;
  }

  /** Task local: cria a task do método se faltar (com criar no Prisma) e religa à História da pasta. */
  private async ensureTask(ctx: Context, item: SyncItem, local: LocalDocument): Promise<void> {
    const expected = local.parentKey!;
    const tasks = await this.methodTasks(ctx);
    const task = tasks.get(local.key);
    if (!task) {
      if (!ctx.options.criarNoPrisma) return;
      if (expected !== AVULSAS_KEY && !this.exists(ctx, expected)) {
        item.aviso = `${expected} não existe no Prisma: a task nasce no topo e é religada quando a História existir.`;
      }
      item.passos.push('criar_task');
      if (ctx.options.ensaio) { ctx.planned.add(`task:${local.key}`); if (expected === AVULSAS_KEY) ctx.planned.add(AVULSAS_KEY); return; }
      const created = await this.api.createMethodTask(taskDto(local, expected));
      tasks.set(local.key, created);
      if (expected === AVULSAS_KEY) ctx.planned.add(AVULSAS_KEY);
      return;
    }
    const current = await this.currentParent(ctx, local.key, task);
    if (current === expected) return;
    if (!this.exists(ctx, expected)) {
      item.aviso = `Religação pendente: ${expected} não existe no Prisma (use criar no Prisma ou envie a HU antes).`;
      return;
    }
    item.passos.push('religar');
    item.religar_para = expected;
    if (ctx.options.ensaio) return;
    await this.docs.relink(local.key, expected);
    const doc = ctx.fullDocs.get(local.key);
    if (doc) ctx.fullDocs.set(local.key, { ...doc, parentKey: expected });
  }

  /** Pai atual da task no Prisma: pelo documento da task, ou pelo item pai quando ainda não há documento. */
  private async currentParent(ctx: Context, key: string, task: MethodTaskDto): Promise<string | null> {
    if (ctx.remote.has(key)) {
      const doc = await this.fullDoc(ctx, key);
      if (doc) return doc.parentKey;
    }
    const item = await this.api.getWorkItem(task.workItemId);
    if (!item.parentId) return null;
    for (const s of ctx.remote.values()) if (s.kind === 'hu' && s.workItemId === item.parentId) return s.key;
    return `item:${item.parentId}`;
  }

  private async fullDoc(ctx: Context, key: string): Promise<MethodDocument | undefined> {
    const remote = ctx.remote.get(key);
    const cached = ctx.fullDocs.get(key);
    if (cached && remote && cached.version === remote.version) return cached;
    const doc = await this.docs.find(key);
    if (doc) { ctx.fullDocs.set(key, doc); ctx.remote.set(key, doc); }
    return doc;
  }

  private async apply(ctx: Context, item: SyncItem, d: {
    key: string; kind: DocumentKind; local?: LocalDocument; path?: SafePath; file?: MetodoFile;
    remote?: MethodDocumentSummary; record?: SyncRecord;
  }): Promise<void> {
    const { key, kind, local, path, file, remote, record } = d;
    const ensaio = ctx.options.ensaio === true;
    const parentKey = kind === 'hu' && local?.parentKey ? local.parentKey : undefined;
    const send = async (baseVersion: number, step: SyncStep) => {
      item.passos.push(step);
      if (parentKey && !this.exists(ctx, parentKey)) item.aviso = `O épico ${parentKey} não existe no Prisma.`;
      if (ensaio) { ctx.planned.add(key); return; }
      const doc = await this.docs.put(key, { content: file!.lf, baseVersion, filePath: `docs/trabalho/${local!.rel}`, ...(parentKey ? { parentKey } : {}) });
      ctx.remote.set(key, doc); ctx.fullDocs.set(key, doc); ctx.planned.add(key);
      this.setState(ctx, key, kind, doc.workItemId, doc.version, file!.hash);
      item.executado = true; item.versao = doc.version;
    };
    switch (item.acao) {
      case 'nada': return;
      case 'enviar': return send(record!.versao, 'enviar');
      case 'atualizar_estado':
      case 'criar_estado':
        item.passos.push(item.acao);
        if (ensaio) return;
        this.setState(ctx, key, kind, remote!.workItemId, remote!.version, file!.hash);
        item.executado = true; item.versao = remote!.version;
        return;
      case 'puxar': {
        if (ctx.options.somenteEnviar) {
          item.aviso = `O Prisma tem uma versão mais nova (v${remote!.version}); nada foi enviado. Rode sincronizar para puxar.`;
          return;
        }
        item.passos.push('puxar');
        if (ensaio) return;
        const doc = await this.fullDoc(ctx, key);
        if (!doc) throw new Error(`${key} sumiu do Prisma durante a sincronização.`);
        await this.assertUnchanged(path!, file!);
        const written = await writeMetodoFile(path!, doc.content, file);
        this.setState(ctx, key, kind, doc.workItemId, doc.version, written.hash);
        item.executado = true; item.versao = doc.version;
        return;
      }
      case 'conflito': {
        const doc = await this.fullDoc(ctx, key);
        if (!doc) throw new Error(`${key} sumiu do Prisma durante a sincronização.`);
        const base = record ? await this.baseText(key, record.versao) : undefined;
        item.diferenca = diffSections(base, file!.lf, doc.content);
        item.aviso = 'Editado nos dois lados: nada foi gravado. Escolha repo, prisma ou juntar por seção e chame resolver_conflito.';
        return;
      }
      case 'so_no_repo': {
        if (!ctx.options.criarNoPrisma) {
          item.aviso = record ? 'Está no estado mas não no Prisma; envie de novo com criar no Prisma, se for o caso.'
            : 'Só no repositório: use enviar_documento ou sincronizar com criar no Prisma para criar no Prisma.';
          return;
        }
        if (kind === 'prd' && !this.exists(ctx, local!.parentKey!)) { item.aviso = `PRD não enviado: a História ${local!.parentKey} não existe no Prisma.`; return; }
        if (kind === 'task' && !(await this.methodTasks(ctx)).has(key) && !ctx.planned.has(`task:${key}`)) {
          item.aviso = 'Corpo da task não enviado: a task do método não existe no Prisma.'; return;
        }
        return send(0, 'criar');
      }
      case 'so_no_prisma': {
        if (!ctx.options.criarNoRepo) { item.aviso = 'Só no Prisma: sincronize com criar no repositório para gravar o arquivo, ou ignore.'; return; }
        if (!remote!.filePath) { item.aviso = 'Só no Prisma e sem caminho de arquivo: nada gravado.'; return; }
        const target = await resolveMetodoPath(ctx.root, remote!.filePath, { mustExist: false });
        if (await fileExists(target.abs)) throw new Error(`Já existe um arquivo em ${target.rel} com outra chave; nada gravado.`);
        item.arquivo = target.filePath;
        item.passos.push('gravar_arquivo');
        if (ensaio) return;
        const doc = await this.fullDoc(ctx, key);
        if (!doc) throw new Error(`${key} sumiu do Prisma durante a sincronização.`);
        const written = await writeMetodoFile(target, doc.content);
        this.setState(ctx, key, kind, doc.workItemId, doc.version, written.hash);
        item.executado = true; item.versao = doc.version;
        return;
      }
      case 'sumiu_no_repo':
        item.aviso = 'O arquivo sumiu do repositório; nada é apagado no Prisma.';
        return;
    }
  }

  private async baseText(key: string, version: number): Promise<string | undefined> {
    try { return (await this.docs.revision(key, version)).content; }
    catch (error) { if (isNotFound(error)) return undefined; throw error; }
  }

  private async assertUnchanged(path: SafePath, before: MetodoFile): Promise<void> {
    const now = await readMetodoFile(path);
    if (now.hash !== before.hash || !now.bytes.equals(before.bytes)) throw new Error(`${path.rel} mudou durante a sincronização; nada gravado. Rode de novo.`);
  }

  // ---- Conflito ------------------------------------------------------------------------------

  /**
   * Grava a resolução nos dois lados: monta o texto pelas escolhas por seção (repo, prisma ou
   * juntar com o texto aprovado), envia ao Prisma com a versão atual como base, grava o arquivo com
   * o fim de linha e o BOM que ele usava e atualiza o estado.
   */
  async resolverConflito(chave: string, escolhas: Record<string, SectionChoice>) {
    assertDocumentKey(chave);
    const scan = await scanMetodoDocuments(this.root);
    const local = scan.documents.find(d => d.key === chave);
    if (!local) throw new Error(`${chave} não tem arquivo no repositório.`);
    const path = await resolveMetodoPath(this.root, local.rel);
    const file = await readMetodoFile(path);
    const state = await readSyncState(this.root);
    const record = state.documentos[chave];
    const remote = await this.docs.get(chave);
    const base = record ? await this.baseText(chave, record.versao) : undefined;
    const diff = diffSections(base, file.lf, remote.content);
    const merged = assembleResolution(diff, escolhas);
    await this.assertUnchanged(path, file);
    const parentKey = local.kind === 'hu' && local.parentKey ? local.parentKey : undefined;
    let doc: MethodDocument;
    try {
      doc = await this.docs.put(chave, { content: merged, baseVersion: remote.version, filePath: `docs/trabalho/${local.rel}`, ...(parentKey ? { parentKey } : {}) });
    } catch (error) {
      if (isVersionConflict(error)) throw new Error(`O Prisma mudou de novo em ${chave}; nada foi gravado. Rode sincronizar e refaça as escolhas.`);
      throw error;
    }
    await this.assertUnchanged(path, file);
    const written = await writeMetodoFile(path, merged, file);
    state.documentos[chave] = { chave, tipo: local.kind, workItemId: doc.workItemId, versao: doc.version, hash: written.hash };
    await writeSyncState(this.root, state);
    return { chave, arquivo: path.filePath, versao: doc.version, hash: written.hash,
      secoes: diff.map(s => ({ secao: s.secao, classe: s.classe, escolha: describeChoice(escolhas[s.secao]) })) };
  }

  // ---- Retrato antes/depois (T-0025) -----------------------------------------------------------

  /** Retrato de todas as tasks do método: id, status, pai (História), autorização, comentários e PR. Só lê. */
  async snapshot(): Promise<TaskSnapshot> {
    const identity = await this.api.initialize();
    const [tasks, summaries] = await Promise.all([this.api.listMethodTasks(), this.docs.list()]);
    const historyByItem = new Map(summaries.filter(s => s.kind === 'hu').map(s => [s.workItemId, s.key]));
    const rows: TaskSnapshotRow[] = [];
    for (const task of [...tasks].sort((a, b) => a.id.localeCompare(b.id))) {
      let parentId: string | null = null;
      try { parentId = (await this.api.getWorkItem(task.workItemId)).parentId ?? null; }
      catch (error) { if (isAuthFailure(error)) throw error; }
      let comentarios: number | null = null;
      try { comentarios = (await this.api.getWorkItemComments(task.workItemId))?.length ?? 0; }
      catch (error) { if (error instanceof PrismaApiError && error.status === 401) throw error; }
      rows.push({ id: task.id, workItemId: task.workItemId, titulo: task.title, status: task.status, hu: task.hu,
        pai: parentId ? historyByItem.get(parentId) ?? `item:${parentId}` : null, autorizada: task.authorized, comentarios, pr: task.pr });
    }
    return { gerado_em: new Date().toISOString(), projeto: identity.projectName, tasks: rows };
  }
}

export interface TaskSnapshotRow {
  id: string; workItemId: string; titulo: string; status: string; hu: string | null;
  /** História pai (`HU-0001`, `AVULSAS`), `item:<id>` se o pai não é História do método, ou null (topo). */
  pai: string | null;
  autorizada: boolean;
  /** null quando a rota de comentários não respondeu para o token. */
  comentarios: number | null;
  pr: string | null;
}
export interface TaskSnapshot { gerado_em: string; projeto: string; tasks: TaskSnapshotRow[]; }

export function snapshotMarkdown(snapshot: TaskSnapshot): string {
  const cell = (v: unknown) => String(v ?? '—').replaceAll('|', '\\|');
  return [`# Retrato das tasks · ${cell(snapshot.projeto)} · ${snapshot.gerado_em}`, '',
    '| id | status | pai | hu | autorizada | comentários | pr |', '|---|---|---|---|---|---|---|',
    ...snapshot.tasks.map(t => `| ${t.id} | ${t.status} | ${cell(t.pai)} | ${cell(t.hu)} | ${t.autorizada ? 'sim' : 'não'} | ${cell(t.comentarios)} | ${cell(t.pr)} |`),
    ''].join('\n');
}

function taskDto(local: LocalDocument, huKey: string): CreateMethodTaskDto {
  const m = local.meta;
  const status = typeof m.status === 'string' && STATUS_AT_CREATION.has(m.status) ? m.status : 'backlog';
  return {
    id: local.key,
    title: typeof m.titulo === 'string' && m.titulo.trim() ? m.titulo.trim() : local.key,
    hu: huKey === AVULSAS_KEY ? 'avulsa' : huKey,
    filePath: `docs/trabalho/${local.rel}`,
    covers: Array.isArray(m.cobre) ? m.cobre.filter((x): x is string => typeof x === 'string') : [],
    risk: m.risco === 'alto' ? 'alto' : 'normal',
    status,
  };
}

function describeChoice(choice: SectionChoice | undefined): string {
  if (choice === undefined) return 'igual';
  return typeof choice === 'string' ? choice : 'juntar';
}

async function fileExists(path: string): Promise<boolean> {
  try { await lstat(path); return true; }
  catch (error) { if ((error as NodeJS.ErrnoException).code === 'ENOENT') return false; throw error; }
}

function message(error: unknown): string {
  return error instanceof Error ? error.message : 'Falha ao sincronizar o documento.';
}

/** Resumo em texto do relatório (sem conteúdo de documento): para o MCP e para o CLI. */
export function describeReport(report: SyncReport): string {
  const lines: string[] = [];
  lines.push(`${report.ensaio ? 'Ensaio (nada foi gravado em nenhum lado)' : 'Sincronização'} · escopo: ${report.escopo}`);
  const pick = (f: (i: SyncItem) => boolean) => report.itens.filter(f);
  const keys = (items: SyncItem[]) => items.map(i => i.chave).join(', ');
  const group = (title: string, items: SyncItem[], fmt: (items: SyncItem[]) => string = keys) => {
    if (items.length) lines.push(`${title} (${items.length}): ${fmt(items)}`);
  };
  const created = (kind: DocumentKind) => pick(i => i.tipo === kind && i.passos.includes('criar') && i.acao !== 'avulsas');
  group('Épicos a criar', created('epic'));
  group('Histórias a criar', created('hu'));
  group('Avulsas', pick(i => i.acao === 'avulsas'), () => 'criar a História AVULSAS');
  group('PRDs a criar', created('prd'));
  group('Tasks a religar (vincular_hu)', pick(i => i.passos.includes('religar')), items => items.map(i => `${i.chave} → ${i.religar_para}`).join(', '));
  group('Tasks a criar no Prisma', pick(i => i.passos.includes('criar_task')));
  group('Corpos de task a criar', created('task'));
  group('A enviar (mudou no repo)', pick(i => i.passos.includes('enviar')));
  group('A puxar (mudou no Prisma)', pick(i => i.passos.includes('puxar')));
  group('Arquivos a gravar (só no Prisma)', pick(i => i.passos.includes('gravar_arquivo')));
  group('Só atualizar o estado', pick(i => i.passos.includes('atualizar_estado') || i.passos.includes('criar_estado')));
  group('Conflitos', pick(i => i.acao === 'conflito' && !i.erro), items => items.map(i => `${i.chave} [${(i.diferenca ?? [])
    .filter(s => s.classe !== 'igual').map(s => `${s.secao}: ${SECTION_CLASS_LABEL[s.classe]}`).join('; ')}]`).join(', '));
  group('Só no repositório (não criados)', pick(i => i.acao === 'so_no_repo' && !i.passos.length && !i.erro));
  group('Só no Prisma', pick(i => i.acao === 'so_no_prisma' && !i.passos.length && !i.erro));
  group('Sumiram do repositório (nada é apagado no Prisma)', pick(i => i.acao === 'sumiu_no_repo'));
  const unchanged = pick(i => i.acao === 'nada' && !i.passos.length && !i.erro && !i.aviso).length;
  lines.push(`Sem mudança: ${unchanged}`);
  for (const i of report.itens) {
    if (i.erro) lines.push(`Erro em ${i.chave}: ${i.erro}`);
    else if (i.aviso && !['conflito', 'so_no_repo', 'so_no_prisma', 'sumiu_no_repo'].includes(i.acao)) lines.push(`Aviso em ${i.chave}: ${i.aviso}`);
  }
  for (const e of report.erros_locais) lines.push(`Arquivo ignorado ${e.arquivo}: ${e.erro}`);
  return lines.join('\n');
}

