import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useOutletContext, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import styled from 'styled-components';
import {
  Bold, ChevronDown, ChevronRight, Code, Download, FileText, Heading1, Heading2, Heading3,
  History, Image as ImageIcon, Italic, List, ListOrdered, Lock, Paperclip, Plus, Quote,
  RotateCcw, Trash2, Upload, X,
} from 'lucide-react';
import { api } from '../services/api';
import type { ProjectSummary } from './Projects';

interface WikiNode { id: string; parentPageId: string | null; title: string; position: number; updatedAt: string; lockedByOther: boolean; }
interface WikiLock { lockedByUserId: string | null; lockedByName: string | null; lockedAt: string | null; lockedByOther: boolean; }
interface WikiPage { id: string; projectId: string; parentPageId: string | null; title: string; contentHtml: string; updatedAt: string; updatedByName: string; canEdit: boolean; lock: WikiLock; }

const Shell = styled.div`
  display: grid;
  grid-template-columns: 264px minmax(0, 1fr);
  gap: 0;
  height: calc(100vh - 168px);
  margin: 0 auto;
  max-width: 1180px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: 14px;
  overflow: hidden;
  background: ${({ theme }) => theme.color.surface};
  @media (max-width: 800px) { grid-template-columns: 1fr; height: auto; }
`;

const Side = styled.aside`
  display: flex;
  flex-direction: column;
  min-height: 0;
  border-right: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.neutral[50]};
  header { display: flex; align-items: center; gap: 8px; padding: 13px 14px; border-bottom: 1px solid ${({ theme }) => theme.color.border}; }
  header strong { font-size: 14px; font-weight: 800; flex: 1; }
`;
const Tree = styled.div`flex: 1; min-height: 0; overflow: auto; padding: 8px 6px;`;
const NewBtn = styled.button`
  display: inline-flex; align-items: center; gap: 5px; min-height: 28px; padding: 0 8px;
  border: 1px solid ${({ theme }) => theme.color.border}; border-radius: 8px;
  background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
  font-size: 13px; font-weight: 700;
`;
const Row = styled.div<{ $active?: boolean; $depth: number }>`
  display: flex; align-items: center; gap: 3px;
  padding: 5px 6px 5px ${({ $depth }) => 6 + $depth * 14}px;
  border-radius: 7px;
  cursor: pointer;
  color: ${({ theme, $active }) => $active ? theme.color.text : theme.color.textMuted};
  background: ${({ theme, $active }) => $active ? `color-mix(in srgb, ${theme.color.accentBlue} 14%, transparent)` : 'transparent'};
  font-size: 14px; font-weight: ${({ $active }) => $active ? 700 : 550};
  &:hover { background: ${({ theme, $active }) => $active ? `color-mix(in srgb, ${theme.color.accentBlue} 16%, transparent)` : theme.color.neutral[100]}; }
  .caret { display: grid; place-items: center; width: 16px; height: 16px; flex: 0 0 16px; opacity: .6; }
  .title { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .act { opacity: 0; display: grid; place-items: center; width: 20px; height: 20px; border-radius: 5px; color: ${({ theme }) => theme.color.textMuted}; }
  &:hover .act { opacity: 1; }
  .act:hover { background: ${({ theme }) => theme.color.neutral[200]}; color: ${({ theme }) => theme.color.text}; }
`;

const Main = styled.section`position: relative; display: flex; flex-direction: column; min-width: 0; min-height: 0;`;
const PageHead = styled.div`
  display: flex; align-items: center; gap: 10px; padding: 14px 20px 10px;
  input { flex: 1; border: none; background: transparent; font-size: 22px; font-weight: 800; color: ${({ theme }) => theme.color.text}; }
  input:focus { outline: none; }
  input:disabled { color: ${({ theme }) => theme.color.text}; }
`;
const Status = styled.span<{ $tone?: 'muted' | 'lock' }>`
  display: inline-flex; align-items: center; gap: 5px;
  font-size: 13px; font-weight: 700;
  color: ${({ theme, $tone }) => $tone === 'lock' ? theme.color.warning : theme.color.textMuted};
`;
const Toolbar = styled.div`
  display: flex; flex-wrap: wrap; gap: 3px; padding: 6px 18px; border-block: 1px solid ${({ theme }) => theme.color.border};
  button { display: grid; place-items: center; width: 30px; height: 28px; border: none; border-radius: 6px; background: transparent; color: ${({ theme }) => theme.color.textMuted}; }
  button:hover { background: ${({ theme }) => theme.color.neutral[100]}; color: ${({ theme }) => theme.color.text}; }
  button.on { background: ${({ theme }) => `color-mix(in srgb, ${theme.color.accentBlue} 16%, transparent)`}; color: ${({ theme }) => theme.color.accentBlue}; }
  .sep { width: 1px; height: 20px; margin: 4px 4px; background: ${({ theme }) => theme.color.border}; }
`;
const EditorWrap = styled.div`
  flex: 1; min-height: 0; overflow: auto; padding: 18px 24px 60px;
  .ProseMirror { min-height: 260px; outline: none; font-size: 15px; line-height: 1.7; color: ${({ theme }) => theme.color.text}; }
  .ProseMirror:focus { outline: none; }
  .ProseMirror h1 { font-size: 22px; font-weight: 800; margin: 18px 0 8px; }
  .ProseMirror h2 { font-size: 18px; font-weight: 750; margin: 16px 0 7px; }
  .ProseMirror h3 { font-size: 15px; font-weight: 700; margin: 14px 0 6px; }
  .ProseMirror p { margin: 7px 0; }
  .ProseMirror ul, .ProseMirror ol { padding-left: 22px; margin: 7px 0; }
  .ProseMirror blockquote { border-left: 3px solid ${({ theme }) => theme.color.border}; padding-left: 12px; color: ${({ theme }) => theme.color.textMuted}; margin: 10px 0; }
  .ProseMirror pre { background: ${({ theme }) => theme.color.neutral[100]}; padding: 10px 12px; border-radius: 8px; font-size: 14px; overflow: auto; }
  .ProseMirror img { max-width: 100%; height: auto; border-radius: 8px; margin: 8px 0; }
  .ProseMirror img.ProseMirror-selectednode { outline: 2px solid ${({ theme }) => theme.color.accentBlue}; }
  .ProseMirror p.is-editor-empty:first-child::before { content: attr(data-placeholder); color: ${({ theme }) => theme.color.textMuted}; float: left; height: 0; pointer-events: none; }
`;
const Empty = styled.div`display: grid; place-items: center; flex: 1; color: ${({ theme }) => theme.color.textMuted}; font-size: 14px; text-align: center; gap: 8px; padding: 40px;`;

const MAX_IMAGE_BYTES = 1_500_000;

const HeadBtn = styled.button`
  display: inline-flex; align-items: center; gap: 5px; min-height: 28px; padding: 0 9px;
  border: 1px solid ${({ theme }) => theme.color.border}; border-radius: 8px;
  background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px; font-weight: 700; white-space: nowrap;
  &:hover { color: ${({ theme }) => theme.color.text}; }
`;
const Attach = styled.div`
  border-top: 1px solid ${({ theme }) => theme.color.border};
  padding: 12px 24px 18px;
  header { display: flex; align-items: center; gap: 8px; margin-bottom: 9px; }
  header strong { font-size: 13.5px; font-weight: 750; flex: 1; }
  .files { display: flex; flex-wrap: wrap; gap: 8px; }
  .none { color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; }
`;
const FileChip = styled.div`
  display: inline-flex; align-items: center; gap: 8px; padding: 6px 8px 6px 10px;
  border: 1px solid ${({ theme }) => theme.color.border}; border-radius: 9px;
  background: ${({ theme }) => theme.color.neutral[50]}; font-size: 13px; max-width: 260px;
  .name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .size { color: ${({ theme }) => theme.color.textMuted}; }
  button { display: grid; place-items: center; width: 22px; height: 22px; border: none; border-radius: 5px; background: transparent; color: ${({ theme }) => theme.color.textMuted}; }
  button:hover { background: ${({ theme }) => theme.color.neutral[200]}; color: ${({ theme }) => theme.color.text}; }
`;
const Drawer = styled.div`
  position: absolute; inset: 0 0 0 auto; width: min(380px, 92%); z-index: 5;
  display: flex; flex-direction: column;
  background: ${({ theme }) => theme.color.surface};
  border-left: 1px solid ${({ theme }) => theme.color.border};
  box-shadow: -12px 0 40px rgba(16, 24, 40, .12);
  header { display: flex; align-items: center; gap: 8px; padding: 13px 15px; border-bottom: 1px solid ${({ theme }) => theme.color.border}; }
  header strong { flex: 1; font-size: 14px; font-weight: 800; }
  header button { display: grid; place-items: center; width: 26px; height: 26px; border: none; border-radius: 6px; background: transparent; color: ${({ theme }) => theme.color.textMuted}; }
`;
const RevList = styled.div`overflow: auto; padding: 8px;`;
const RevRow = styled.button<{ $active?: boolean }>`
  display: block; width: 100%; text-align: left; padding: 9px 10px; margin-bottom: 4px;
  border: 1px solid ${({ theme, $active }) => $active ? theme.color.accentBlue : 'transparent'};
  border-radius: 8px; background: ${({ theme, $active }) => $active ? `color-mix(in srgb, ${theme.color.accentBlue} 10%, transparent)` : 'transparent'};
  strong { display: block; font-size: 13.5px; font-weight: 700; color: ${({ theme }) => theme.color.text}; }
  span { font-size: 12px; color: ${({ theme }) => theme.color.textMuted}; }
  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; }
`;
const RevPreview = styled.div`
  flex: 1; min-height: 0; overflow: auto; padding: 12px 15px; border-top: 1px solid ${({ theme }) => theme.color.border};
  font-size: 14px; line-height: 1.6;
  img { max-width: 100%; }
  h1 { font-size: 18px; } h2 { font-size: 15px; }
`;
const RevActions = styled.div`display: flex; gap: 8px; padding: 10px 15px; border-top: 1px solid ${({ theme }) => theme.color.border};`;
const fmtBytes = (bytes: number) => bytes >= 1_000_000 ? `${(bytes / 1_000_000).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1000))} KB`;

function buildChildren(nodes: WikiNode[]): Map<string | null, WikiNode[]> {
  const map = new Map<string | null, WikiNode[]>();
  for (const node of nodes) {
    const key = node.parentPageId;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(node);
  }
  for (const list of map.values()) list.sort((a, b) => a.position - b.position || a.title.localeCompare(b.title));
  return map;
}

export function ProjectWiki() {
  const { project } = useOutletContext<{ project: ProjectSummary }>();
  const { pageId } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const treeQuery = useQuery<WikiNode[]>({
    queryKey: ['wiki-tree', project.id],
    queryFn: () => api.getWikiTree(project.id),
  });
  const nodes = treeQuery.data ?? [];
  const childrenMap = useMemo(() => buildChildren(nodes), [nodes]);

  // Seleção: usa a página da URL; se não houver, cai na primeira do topo.
  const selectedId = pageId ?? childrenMap.get(null)?.[0]?.id;
  const goTo = (id: string) => navigate(`/projects/${project.id}/wiki/${id}`);

  const pageQuery = useQuery<WikiPage>({
    queryKey: ['wiki-page', project.id, selectedId],
    queryFn: () => api.getWikiPage(project.id, selectedId!),
    enabled: !!selectedId,
  });

  const createPage = useMutation({
    mutationFn: (parentPageId: string | null) => api.createWikiPage(project.id, parentPageId, 'Nova página'),
    onSuccess: async (page: WikiPage) => {
      await queryClient.invalidateQueries({ queryKey: ['wiki-tree', project.id] });
      goTo(page.id);
    },
  });
  const deletePage = useMutation({
    mutationFn: (id: string) => api.deleteWikiPage(project.id, id),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['wiki-tree', project.id] }); navigate(`/projects/${project.id}/wiki`); },
  });

  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const toggle = (id: string) => setExpanded((current) => { const next = new Set(current); next.has(id) ? next.delete(id) : next.add(id); return next; });

  const renderNodes = (parentId: string | null, depth: number): React.ReactNode =>
    (childrenMap.get(parentId) ?? []).map((node) => {
      const kids = childrenMap.get(node.id) ?? [];
      const isOpen = expanded.has(node.id);
      return (
        <div key={node.id}>
          <Row $active={node.id === selectedId} $depth={depth} onClick={() => goTo(node.id)}>
            <span className="caret" onClick={(event) => { event.stopPropagation(); if (kids.length) toggle(node.id); }}>
              {kids.length ? (isOpen ? <ChevronDown size={13} /> : <ChevronRight size={13} />) : null}
            </span>
            <span className="title">{node.title}</span>
            {node.lockedByOther && <Lock size={11} />}
            <span className="act" title="Nova subpágina" onClick={(event) => { event.stopPropagation(); createPage.mutate(node.id); setExpanded((c) => new Set(c).add(node.id)); }}><Plus size={13} /></span>
            <span className="act" title="Excluir" onClick={(event) => { event.stopPropagation(); if (confirm(`Mover "${node.title}" e subpáginas para a lixeira?`)) deletePage.mutate(node.id); }}><Trash2 size={12} /></span>
          </Row>
          {isOpen && renderNodes(node.id, depth + 1)}
        </div>
      );
    });

  return (
    <Shell>
      <Side>
        <header>
          <FileText size={15} />
          <strong>Wiki</strong>
          <NewBtn onClick={() => createPage.mutate(null)}><Plus size={12} />Página</NewBtn>
        </header>
        <Tree>
          {treeQuery.isLoading ? <Row $depth={0}>Carregando...</Row>
            : nodes.length === 0 ? <Row $depth={0}>Sem páginas ainda.</Row>
            : renderNodes(null, 0)}
        </Tree>
      </Side>
      <Main>
        {!selectedId ? <Empty><FileText size={22} /><div>Selecione uma página ou crie a primeira.</div></Empty>
          : pageQuery.data ? <WikiEditor key={pageQuery.data.id} projectId={project.id} page={pageQuery.data} />
          : <Empty>Carregando página...</Empty>}
      </Main>
    </Shell>
  );
}

function WikiEditor({ projectId, page }: { projectId: string; page: WikiPage }) {
  const queryClient = useQueryClient();
  const [title, setTitle] = useState(page.title);
  const [status, setStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  const [historyOpen, setHistoryOpen] = useState(false);
  const readOnly = !page.canEdit || page.lock.lockedByOther;
  const saveTimer = useRef<number | undefined>(undefined);
  const latestHtml = useRef<string>(page.contentHtml);
  const imageInputRef = useRef<HTMLInputElement>(null);

  const editor = useEditor({
    extensions: [StarterKit, Image.configure({ inline: false })],
    content: page.contentHtml || '',
    editable: !readOnly,
    onUpdate: ({ editor: instance }) => {
      latestHtml.current = instance.getHTML();
      window.clearTimeout(saveTimer.current);
      saveTimer.current = window.setTimeout(() => void flush(), 1200);
    },
  }, [page.id]);

  const flush = useCallback(async () => {
    if (readOnly) return;
    try {
      setStatus('saving');
      await api.saveWikiContent(projectId, page.id, latestHtml.current);
      setStatus('saved');
    } catch { setStatus('error'); }
  }, [projectId, page.id, readOnly]);

  // Trava: adquire ao abrir e mantém com heartbeat; libera ao sair.
  useEffect(() => {
    if (readOnly) return;
    let alive = true;
    void api.acquireWikiLock(projectId, page.id).catch(() => undefined);
    const beat = window.setInterval(() => { if (alive) void api.acquireWikiLock(projectId, page.id).catch(() => undefined); }, 90_000);
    return () => {
      alive = false;
      window.clearInterval(beat);
      window.clearTimeout(saveTimer.current);
      void flush();
      void api.releaseWikiLock(projectId, page.id).catch(() => undefined);
    };
  }, [projectId, page.id, readOnly, flush]);

  const renameMutation = useMutation({
    mutationFn: (value: string) => api.renameWikiPage(projectId, page.id, value),
    onSuccess: () => { void queryClient.invalidateQueries({ queryKey: ['wiki-tree', projectId] }); },
  });
  const commitTitle = () => { const value = title.trim(); if (value && value !== page.title) renameMutation.mutate(value); };

  const pickImage = () => imageInputRef.current?.click();
  const onImageChosen = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    if (file.size > MAX_IMAGE_BYTES) { alert('Imagem muito grande (máx. 1,5 MB). Use um anexo para arquivos maiores.'); return; }
    const reader = new FileReader();
    reader.onload = () => editor?.chain().focus().setImage({ src: reader.result as string }).run();
    reader.readAsDataURL(file);
  };

  const btn = (active: boolean, onClick: () => void, icon: React.ReactNode, label: string) =>
    <button type="button" className={active ? 'on' : ''} disabled={readOnly} onClick={onClick} title={label} aria-label={label}>{icon}</button>;

  return (
    <>
      <PageHead>
        <input value={title} disabled={readOnly} onChange={(event) => setTitle(event.target.value)} onBlur={commitTitle}
          onKeyDown={(event) => { if (event.key === 'Enter') (event.target as HTMLInputElement).blur(); }} />
        {page.lock.lockedByOther
          ? <Status $tone="lock"><Lock size={12} />Editando: {page.lock.lockedByName}</Status>
          : status === 'saving' ? <Status>Salvando...</Status>
          : status === 'saved' ? <Status>Salvo</Status>
          : status === 'error' ? <Status $tone="lock">Falha ao salvar</Status>
          : <Status>{readOnly ? 'Somente leitura' : 'Autosave ativo'}</Status>}
        <HeadBtn onClick={() => setHistoryOpen(true)}><History size={13} />Histórico</HeadBtn>
      </PageHead>
      {editor && !readOnly && (
        <Toolbar>
          {btn(editor.isActive('bold'), () => editor.chain().focus().toggleBold().run(), <Bold size={14} />, 'Negrito')}
          {btn(editor.isActive('italic'), () => editor.chain().focus().toggleItalic().run(), <Italic size={14} />, 'Itálico')}
          <span className="sep" />
          {btn(editor.isActive('heading', { level: 1 }), () => editor.chain().focus().toggleHeading({ level: 1 }).run(), <Heading1 size={14} />, 'Título 1')}
          {btn(editor.isActive('heading', { level: 2 }), () => editor.chain().focus().toggleHeading({ level: 2 }).run(), <Heading2 size={14} />, 'Título 2')}
          {btn(editor.isActive('heading', { level: 3 }), () => editor.chain().focus().toggleHeading({ level: 3 }).run(), <Heading3 size={14} />, 'Título 3')}
          <span className="sep" />
          {btn(editor.isActive('bulletList'), () => editor.chain().focus().toggleBulletList().run(), <List size={14} />, 'Lista')}
          {btn(editor.isActive('orderedList'), () => editor.chain().focus().toggleOrderedList().run(), <ListOrdered size={14} />, 'Lista numerada')}
          {btn(editor.isActive('blockquote'), () => editor.chain().focus().toggleBlockquote().run(), <Quote size={14} />, 'Citação')}
          {btn(editor.isActive('codeBlock'), () => editor.chain().focus().toggleCodeBlock().run(), <Code size={14} />, 'Código')}
          <span className="sep" />
          {btn(false, pickImage, <ImageIcon size={14} />, 'Inserir imagem')}
        </Toolbar>
      )}
      <input ref={imageInputRef} type="file" accept="image/*" hidden onChange={onImageChosen} />
      <EditorWrap><EditorContent editor={editor} /></EditorWrap>
      <WikiTaskLinks projectId={projectId} pageId={page.id} canEdit={!readOnly} />
      <WikiAttachments projectId={projectId} pageId={page.id} canEdit={!readOnly} />
      {historyOpen && <HistoryDrawer projectId={projectId} pageId={page.id} canEdit={page.canEdit} onClose={() => setHistoryOpen(false)}
        onReverted={async () => { await queryClient.invalidateQueries({ queryKey: ['wiki-page', projectId, page.id] }); setHistoryOpen(false); }} />}
    </>
  );
}

interface WikiAttachmentItem { id: string; fileName: string; fileSize: number; mimeType?: string; uploadedByName: string; createdAt: string; }

function WikiAttachments({ projectId, pageId, canEdit }: { projectId: string; pageId: string; canEdit: boolean }) {
  const queryClient = useQueryClient();
  const inputRef = useRef<HTMLInputElement>(null);
  const list = useQuery<WikiAttachmentItem[]>({ queryKey: ['wiki-attachments', projectId, pageId], queryFn: () => api.getWikiAttachments(projectId, pageId) });
  const upload = useMutation({
    mutationFn: (file: File) => api.uploadWikiAttachment(projectId, pageId, file),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['wiki-attachments', projectId, pageId] }),
    onError: (error) => alert((error as Error).message),
  });
  const remove = useMutation({
    mutationFn: (id: string) => api.deleteWikiAttachment(projectId, pageId, id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['wiki-attachments', projectId, pageId] }),
  });
  const download = async (item: WikiAttachmentItem) => {
    const blob = await api.downloadWikiAttachment(projectId, pageId, item.id);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url; anchor.download = item.fileName; anchor.click();
    URL.revokeObjectURL(url);
  };
  const items = list.data ?? [];
  return (
    <Attach>
      <header>
        <Paperclip size={13} />
        <strong>Anexos</strong>
        {canEdit && <HeadBtn onClick={() => inputRef.current?.click()} disabled={upload.isPending}><Upload size={12} />{upload.isPending ? 'Enviando...' : 'Anexar'}</HeadBtn>}
        <input ref={inputRef} type="file" hidden onChange={(event) => { const file = event.target.files?.[0]; event.target.value = ''; if (file) upload.mutate(file); }} />
      </header>
      {items.length === 0 ? <span className="none">Nenhum anexo nesta página.</span>
        : <div className="files">{items.map((item) => (
          <FileChip key={item.id}>
            <span className="name" title={item.fileName}>{item.fileName}</span>
            <span className="size">{fmtBytes(item.fileSize)}</span>
            <button title="Baixar" onClick={() => void download(item)}><Download size={13} /></button>
            {canEdit && <button title="Remover" onClick={() => { if (confirm(`Remover "${item.fileName}"?`)) remove.mutate(item.id); }}><Trash2 size={12} /></button>}
          </FileChip>
        ))}</div>}
    </Attach>
  );
}

interface WikiTaskLink { workItemId: string; number: number; title: string; reference: string; }

function WikiTaskLinks({ projectId, pageId, canEdit }: { projectId: string; pageId: string; canEdit: boolean }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [num, setNum] = useState('');
  const list = useQuery<WikiTaskLink[]>({ queryKey: ['wiki-links', projectId, pageId], queryFn: () => api.getWikiPageLinks(projectId, pageId) });
  const link = useMutation({
    mutationFn: (value: number) => api.linkWikiTask(projectId, pageId, value),
    onSuccess: () => { setNum(''); queryClient.invalidateQueries({ queryKey: ['wiki-links', projectId, pageId] }); },
    onError: (error) => alert((error as Error).message),
  });
  const unlink = useMutation({
    mutationFn: (workItemId: string) => api.unlinkWikiTask(projectId, pageId, workItemId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['wiki-links', projectId, pageId] }),
  });
  const items = list.data ?? [];
  const submit = () => { const value = parseInt(num.replace(/\D/g, ''), 10); if (value > 0) link.mutate(value); };
  return (
    <Attach>
      <header>
        <FileText size={13} />
        <strong>Tarefas vinculadas</strong>
        {canEdit && <>
          <input value={num} placeholder="Nº da tarefa" onChange={(event) => setNum(event.target.value)}
            onKeyDown={(event) => { if (event.key === 'Enter') submit(); }}
            style={{ width: 96, minHeight: 28, padding: '0 8px', borderRadius: 8, border: '1px solid #e2e5ea', fontSize: 13.5 }} />
          <HeadBtn onClick={submit} disabled={link.isPending}><Plus size={12} />Vincular</HeadBtn>
        </>}
      </header>
      {items.length === 0 ? <span className="none">Nenhuma tarefa vinculada.</span>
        : <div className="files">{items.map((item) => (
          <FileChip key={item.workItemId}>
            <span className="name" title={item.title} style={{ cursor: 'pointer' }}
              onClick={() => navigate(`/projects/${projectId}/backlog?item=${item.workItemId}`)}>
              <strong>{item.reference}</strong> · {item.title}
            </span>
            {canEdit && <button title="Desvincular" onClick={() => unlink.mutate(item.workItemId)}><X size={13} /></button>}
          </FileChip>
        ))}</div>}
    </Attach>
  );
}

interface WikiRevision { id: string; authorName: string; createdAt: string; updatedAt: string; isCurrent: boolean; }
interface WikiRevisionContent { id: string; title: string; contentHtml: string; authorName: string; updatedAt: string; }

function HistoryDrawer({ projectId, pageId, canEdit, onClose, onReverted }: { projectId: string; pageId: string; canEdit: boolean; onClose: () => void; onReverted: () => void }) {
  const [selected, setSelected] = useState<string | null>(null);
  const history = useQuery<WikiRevision[]>({ queryKey: ['wiki-history', projectId, pageId], queryFn: () => api.getWikiHistory(projectId, pageId) });
  const preview = useQuery<WikiRevisionContent>({ queryKey: ['wiki-revision', projectId, pageId, selected], queryFn: () => api.getWikiRevision(projectId, pageId, selected!), enabled: !!selected });
  const revert = useMutation({ mutationFn: (id: string) => api.revertWikiPage(projectId, pageId, id), onSuccess: onReverted });
  const fmt = (iso: string) => new Date(iso).toLocaleString('pt-BR');
  return (
    <Drawer>
      <header><History size={14} /><strong>Histórico de versões</strong><button onClick={onClose} aria-label="Fechar"><X size={15} /></button></header>
      <RevList>
        {history.isLoading ? <RevRow>Carregando...</RevRow>
          : (history.data ?? []).length === 0 ? <RevRow>Sem versões ainda.</RevRow>
          : (history.data ?? []).map((rev) => (
            <RevRow key={rev.id} $active={selected === rev.id} onClick={() => setSelected(rev.id)}>
              <strong>{rev.authorName}{rev.isCurrent ? ' · atual' : ''}</strong>
              <span>{fmt(rev.updatedAt)}</span>
            </RevRow>
          ))}
      </RevList>
      {selected && preview.data && (
        <>
          <RevPreview dangerouslySetInnerHTML={{ __html: preview.data.contentHtml || '<p><em>Vazio</em></p>' }} />
          {canEdit && <RevActions>
            <HeadBtn onClick={() => revert.mutate(selected)} disabled={revert.isPending}><RotateCcw size={12} />{revert.isPending ? 'Restaurando...' : 'Restaurar esta versão'}</HeadBtn>
          </RevActions>}
        </>
      )}
    </Drawer>
  );
}
