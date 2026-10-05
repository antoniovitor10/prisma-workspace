import { useCallback, useEffect, useRef, useState, type ReactNode, type Ref } from 'react';
import { EditorContent, useEditor } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Underline from '@tiptap/extension-underline';
import Link from '@tiptap/extension-link';
import Highlight from '@tiptap/extension-highlight';
import TextAlign from '@tiptap/extension-text-align';
import TaskList from '@tiptap/extension-task-list';
import TaskItem from '@tiptap/extension-task-item';
import Placeholder from '@tiptap/extension-placeholder';
import Image from '@tiptap/extension-image';
import type { EditorView } from '@tiptap/pm/view';
import {
  AlignCenter, AlignLeft, AlignRight, Bold, CheckSquare, Code, Highlighter,
  Image as ImageIcon, Italic, Link2, List, ListOrdered, Maximize2, Minimize2,
  Paperclip, Pilcrow, Quote, Redo2, Strikethrough, Underline as UnderlineIcon, Undo2,
} from 'lucide-react';
import styled from 'styled-components';
import { api } from '../services/api';

const PASTED_IMAGE_PLACEHOLDER = '/image-placeholder.svg';
const REMOTE_IMAGE_TIMEOUT_MS = 10_000;
const MAX_REMOTE_IMAGE_BYTES = 10_000_000;

const looksLikeImageUrl = (value:string) => {
  try {
    return /\.(?:avif|gif|jpe?g|png|svg|webp)$/i.test(new URL(value).pathname);
  } catch {
    return false;
  }
};

const TaskImage = Image.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      attachmentId: {
        default: null,
        parseHTML: element => element.getAttribute('data-attachment-id'),
        renderHTML: attributes => attributes.attachmentId
          ? { 'data-attachment-id': attributes.attachmentId }
          : {},
      },
    };
  },
});

const Frame = styled.section<{ $expanded: boolean }>`
  position:${({$expanded})=>$expanded?'fixed':'relative'};
  inset:${({$expanded})=>$expanded?'0':'auto'};
  z-index:${({$expanded})=>$expanded?'100':'auto'};
  display:flex;flex-direction:column;min-height:${({$expanded})=>$expanded?'100dvh':'440px'};
  width:100%;overflow:hidden;border:1px solid ${({theme})=>theme.color.border};
  border-radius:${({$expanded,theme})=>$expanded?'0':theme.radius.lg};background:${({theme})=>theme.color.surface};
  box-shadow:${({$expanded,theme})=>$expanded?theme.shadow.lg:'none'};
`;
const Toolbar = styled.div`
  position:sticky;top:0;z-index:2;display:flex;align-items:center;gap:3px;min-height:50px;
  padding:7px 10px;overflow-x:auto;border-bottom:1px solid ${({theme})=>theme.color.border};
  background:${({theme})=>theme.color.surface};
  scroll-behavior:smooth;overscroll-behavior-x:contain;
  .toolbarGroup{display:flex;align-items:center;gap:3px;padding:2px;border:1px solid transparent;border-radius:9px;flex:0 0 auto;}
  .toolbarGroup:hover{border-color:${({theme})=>theme.color.border};background:${({theme})=>theme.color.neutral[50]};}
  @media (max-width:640px){padding:6px;gap:2px;min-height:48px;flex-wrap:wrap;overflow-x:hidden}.sep{margin:0 2px}
  button{display:grid;flex:0 0 34px;width:34px;height:34px;place-items:center;border-radius:7px;color:${({theme})=>theme.color.textMuted};}
  button:hover:not(:disabled){background:${({theme})=>theme.color.neutral[100]};color:${({theme})=>theme.color.text};}
  button:focus-visible,select:focus-visible{outline:2px solid ${({theme})=>theme.color.accentBlue};outline-offset:2px;}
  button[aria-pressed=true]{background:${({theme})=>`color-mix(in srgb, ${theme.color.accentBlue} 14%, transparent)`};color:${({theme})=>theme.color.accentBlue};}
  button:disabled{opacity:.35}.sep{flex:0 0 1px;width:1px;height:24px;margin:0 5px;background:${({theme})=>theme.color.border};}
  select{flex:0 0 auto;min-width:116px;height:34px;padding:0 28px 0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:7px;background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.text};font:inherit;font-size:13px;}
`;
const Editor = styled.div<{ $expanded:boolean }>`
  flex:1;min-height:${({$expanded})=>$expanded?'0':'386px'};overflow:auto;padding:18px 22px 48px;
  .ProseMirror{min-height:${({$expanded})=>$expanded?'calc(100dvh - 126px)':'330px'};outline:none;color:${({theme})=>theme.color.text};font-size:15px;line-height:1.7;}
  .ProseMirror p{margin:7px 0}.ProseMirror h1{margin:18px 0 8px;font-size:26px;line-height:1.25}.ProseMirror h2{margin:16px 0 7px;font-size:21px}.ProseMirror h3{margin:14px 0 6px;font-size:17px}
  .ProseMirror ul,.ProseMirror ol{margin:8px 0;padding-left:24px}.ProseMirror ul[data-type=taskList]{padding:0;list-style:none}.ProseMirror ul[data-type=taskList] li{display:flex;gap:8px}.ProseMirror ul[data-type=taskList] li>div{flex:1}.ProseMirror input[type=checkbox]{margin-top:6px}
  .ProseMirror blockquote{margin:12px 0;padding-left:14px;border-left:3px solid ${({theme})=>theme.color.accentBlue};color:${({theme})=>theme.color.textMuted}}
  .ProseMirror pre{margin:10px 0;padding:12px;border-radius:8px;background:${({theme})=>theme.color.neutral[100]};overflow:auto}.ProseMirror code{font-family:ui-monospace,SFMono-Regular,Consolas,monospace}
  .ProseMirror a{color:${({theme})=>theme.color.accentBlue};text-decoration:underline}.ProseMirror img{display:block;max-width:100%;height:auto;margin:12px 0;border-radius:8px}
  .ProseMirror img[data-attachment-id]{min-width:96px;min-height:72px;background:${({theme})=>theme.color.neutral[100]};object-fit:contain}
  .ProseMirror img.image-load-error{padding:18px;border:1px dashed ${({theme})=>theme.color.danger};}
  .ProseMirror p.is-editor-empty:first-child::before{content:attr(data-placeholder);float:left;height:0;color:${({theme})=>theme.color.textMuted};pointer-events:none}
`;
const Footer = styled.footer`display:flex;min-height:36px;align-items:center;justify-content:space-between;gap:12px;padding:0 14px;border-top:1px solid ${({theme})=>theme.color.border};color:${({theme})=>theme.color.textMuted};font-size:12px;letter-spacing:.01em;`;

interface UploadedImage { attachmentId:string; alt:string; }
interface Props {
  value:string;
  workItemId?:string;
  onSave:(html:string)=>Promise<void>|void;
  onOpenAttachments:()=>void;
  onUploadImage?:(file:File)=>Promise<UploadedImage>;
}

export function RichTaskDescriptionEditor({value,workItemId,onSave,onOpenAttachments,onUploadImage}:Props){
  const [expanded,setExpanded]=useState(false);
  const [status,setStatus]=useState<'idle'|'uploading'|'saving'|'saved'|'error'>('idle');
  const [feedback,setFeedback]=useState<string|undefined>();
  const timer=useRef<number|undefined>(undefined);
  const expandButtonRef=useRef<HTMLButtonElement>(null);
  const wasExpanded=useRef(false);
  const latest=useRef(value);
  const saved=useRef(value);
  const onSaveRef=useRef(onSave);
  const onUploadImageRef=useRef(onUploadImage);
  const hydrateImagesRef=useRef<()=>Promise<void>>(async()=>{});
  const imageObjectUrls=useRef(new Map<string,string>());
  const loadingImages=useRef(new Set<string>());
  useEffect(()=>{onSaveRef.current=onSave;},[onSave]);
  useEffect(()=>{onUploadImageRef.current=onUploadImage;},[onUploadImage]);
  const save=useCallback(async()=>{
    window.clearTimeout(timer.current);
    if(latest.current===saved.current)return;
    const html=latest.current;
    try{setStatus('saving');await onSaveRef.current(html);saved.current=html;setStatus('saved');}catch{setStatus('error');}
  },[]);
  const uploadAndInsertImages=useCallback((view:EditorView,images:File[],insertionPosition:number,source:'colada'|'arrastada')=>{
    if(!images.length||!onUploadImageRef.current)return false;
    void (async()=>{
      setStatus('uploading');
      const action=source==='colada'?'colada':'arrastada';
      setFeedback(images.length===1?`Enviando imagem ${action}...`:`Enviando ${images.length} imagens ${action}s...`);
      try{
        const uploaded=[] as UploadedImage[];
        for(const image of images)uploaded.push(await onUploadImageRef.current!(image));
        const nodes=uploaded.map(image=>view.state.schema.nodes.image.create({
          src:PASTED_IMAGE_PLACEHOLDER,
          alt:image.alt,
          title:'Imagem anexada à tarefa',
          attachmentId:image.attachmentId,
        }));
        const position=Math.min(insertionPosition,view.state.doc.content.size);
        view.dispatch(view.state.tr.insert(position,nodes));
        setStatus('idle');
        setFeedback(images.length===1?`Imagem ${action} e anexada à tarefa.`:`${images.length} imagens ${action}s e anexadas à tarefa.`);
        window.requestAnimationFrame(()=>void hydrateImagesRef.current());
      }catch{
        setStatus('error');
        setFeedback(`Não foi possível enviar a imagem ${action}. Tente novamente.`);
      }
    })();
    return true;
  },[]);
  const editor=useEditor({
    extensions:[StarterKit.configure({link:false,underline:false}),Underline,Link.configure({openOnClick:false,autolink:true}),Highlight,
      TextAlign.configure({types:['heading','paragraph']}),TaskList,TaskItem.configure({nested:true}),
      Placeholder.configure({placeholder:'Digite aqui os detalhes da tarefa'}),TaskImage.configure({allowBase64:false})],
    content:value||'',
    onUpdate:({editor:instance})=>{latest.current=instance.getHTML();setStatus('idle');setFeedback(undefined);window.clearTimeout(timer.current);timer.current=window.setTimeout(()=>void save(),900);},
    editorProps:{
      attributes:{'aria-label':'Descrição da tarefa',role:'textbox','aria-multiline':'true'},
      handlePaste:(view,event)=>{
        const images=Array.from(event.clipboardData?.files??[]).filter(file=>file.type.startsWith('image/'));
        if(!images.length)return false;
        event.preventDefault();
        return uploadAndInsertImages(view,images,view.state.selection.from,'colada');
      },
      handleDrop:(view,event,_slice,moved)=>{
        if(moved)return false;
        const images=Array.from(event.dataTransfer?.files??[]).filter(file=>file.type.startsWith('image/'));
        if(!images.length)return false;
        event.preventDefault();
        const position=view.posAtCoords({left:event.clientX,top:event.clientY})?.pos??view.state.selection.from;
        return uploadAndInsertImages(view,images,position,'arrastada');
      },
    },
  });
  const hydrateAttachmentImages=useCallback(async()=>{
    if(!editor||!workItemId)return;
    const elements=Array.from(editor.view.dom.querySelectorAll<HTMLImageElement>('img[data-attachment-id]'));
    await Promise.all(elements.map(async element=>{
      const attachmentId=element.dataset.attachmentId;
      if(!attachmentId)return;
      const cached=imageObjectUrls.current.get(attachmentId);
      if(cached){if(element.src!==cached)element.src=cached;return;}
      if(loadingImages.current.has(attachmentId))return;
      loadingImages.current.add(attachmentId);
      try{
        const blob=await api.downloadAttachment(workItemId,attachmentId);
        const objectUrl=URL.createObjectURL(blob);
        imageObjectUrls.current.set(attachmentId,objectUrl);
        if(element.isConnected&&element.dataset.attachmentId===attachmentId){element.src=objectUrl;element.classList.remove('image-load-error');}
      }catch{
        element.classList.add('image-load-error');
        setFeedback('Uma imagem anexada não pôde ser carregada.');
      }finally{loadingImages.current.delete(attachmentId);}
    }));
  },[editor,workItemId]);
  hydrateImagesRef.current=hydrateAttachmentImages;
  useEffect(()=>{if(editor&&value!==latest.current){latest.current=value;saved.current=value;editor.commands.setContent(value||'',{emitUpdate:false});}},[editor,value]);
  useEffect(()=>{const frame=window.requestAnimationFrame(()=>void hydrateAttachmentImages());return()=>window.cancelAnimationFrame(frame);},[hydrateAttachmentImages,value]);
  useEffect(()=>()=>{for(const objectUrl of imageObjectUrls.current.values())URL.revokeObjectURL(objectUrl);imageObjectUrls.current.clear();},[]);
  useEffect(()=>{
    if(!editor)return;
    const onImageError=(event:Event)=>{const image=event.target as HTMLImageElement;if(image.tagName==='IMG'&&!image.dataset.attachmentId){image.classList.add('image-load-error');setFeedback('A imagem indicada não pôde ser carregada.');}};
    editor.view.dom.addEventListener('error',onImageError,true);
    return()=>editor.view.dom.removeEventListener('error',onImageError,true);
  },[editor]);
  useEffect(()=>()=>{window.clearTimeout(timer.current);if(latest.current!==saved.current)void onSaveRef.current(latest.current);},[]);
  useEffect(()=>{if(!expanded)return;const key=(event:KeyboardEvent)=>{if(event.key==='Escape'){event.preventDefault();event.stopPropagation();setExpanded(false);}};window.addEventListener('keydown',key,true);return()=>window.removeEventListener('keydown',key,true);},[expanded]);
  useEffect(()=>{if(!editor)return;const frame=window.requestAnimationFrame(()=>{if(expanded){editor.commands.focus();wasExpanded.current=true;}else if(wasExpanded.current){expandButtonRef.current?.focus();wasExpanded.current=false;}});return()=>window.cancelAnimationFrame(frame);},[editor,expanded]);
  if(!editor)return null;
  const button=(label:string,active:boolean,action:()=>void,icon:ReactNode,disabled=false,ref?:Ref<HTMLButtonElement>)=><button ref={ref} type="button" aria-label={label} title={label} aria-pressed={active} disabled={disabled} onMouseDown={e=>e.preventDefault()} onClick={action}>{icon}</button>;
  const safeUrl=(value:string)=>{try{const url=new URL(value);return url.protocol==='https:'||url.protocol==='http:';}catch{return false;}};
  const safeImageUrl=(value:string)=>{try{return new URL(value).protocol==='https:';}catch{return false;}};
  const insertRemoteImage=async(src:string)=>{
    if(!onUploadImageRef.current){setFeedback('Salve a tarefa antes de inserir uma imagem por endereço.');return;}
    setStatus('uploading');
    setFeedback('Baixando e anexando imagem...');
    const controller=new AbortController();
    const timeout=window.setTimeout(()=>controller.abort(),REMOTE_IMAGE_TIMEOUT_MS);
    try{
      const response=await fetch(src,{signal:controller.signal,referrerPolicy:'no-referrer'});
      if(!response.ok)throw new Error('Falha ao baixar a imagem.');
      const contentType=(response.headers.get('content-type')||'').split(';')[0].trim().toLowerCase();
      if(!contentType.startsWith('image/'))throw new Error('O endereço não retornou uma imagem.');
      const declaredSize=Number(response.headers.get('content-length')||0);
      if(declaredSize>MAX_REMOTE_IMAGE_BYTES)throw new Error('A imagem excede 10 MB.');
      const blob=await response.blob();
      if(blob.size>MAX_REMOTE_IMAGE_BYTES)throw new Error('A imagem excede 10 MB.');
      const fallbackExtension=contentType.split('/')[1]?.replace('jpeg','jpg')||'png';
      const pathName=decodeURIComponent(new URL(src).pathname.split('/').pop()||'').trim();
      const fileName=pathName.includes('.')?pathName:`imagem-remota.${fallbackExtension}`;
      const uploaded=await onUploadImageRef.current(new File([blob],fileName,{type:contentType}));
      editor.chain().focus(undefined,{scrollIntoView:false}).insertContent({
        type:'image',
        attrs:{
          src:PASTED_IMAGE_PLACEHOLDER,
          alt:uploaded.alt||fileName,
          title:'Imagem anexada à tarefa',
          attachmentId:uploaded.attachmentId,
        },
      }).run();
      setStatus('idle');
      setFeedback('Imagem anexada e inserida na descrição.');
      window.requestAnimationFrame(()=>void hydrateImagesRef.current());
    }catch(error){
      setStatus('error');
      const reason=error instanceof DOMException&&error.name==='AbortError'
        ? 'O carregamento da imagem excedeu 10 segundos.'
        : error instanceof Error ? error.message : 'Não foi possível baixar a imagem.';
      setFeedback(`${reason} Verifique se a URL é HTTPS, pública, direta e permite download pelo navegador.`);
    }finally{
      window.clearTimeout(timeout);
    }
  };
  const setLink=()=>{const previous=editor.getAttributes('link').href as string|undefined;const href=window.prompt('Endereço do link',previous??'https://');if(href===null)return;if(!href.trim()){editor.chain().focus().unsetLink().run();return;}const normalized=href.trim();if(!safeUrl(normalized)){setFeedback('Use um endereço http:// ou https:// válido.');editor.chain().focus().run();return;}if(editor.state.selection.empty&&looksLikeImageUrl(normalized)){if(!safeImageUrl(normalized)){setFeedback('Imagens externas precisam usar HTTPS.');return;}void insertRemoteImage(normalized);return;}editor.chain().focus().extendMarkRange('link').setLink({href:normalized}).run();};
  const setImage=()=>{const src=window.prompt('Endereço público da imagem','https://');if(!src?.trim())return;const normalized=src.trim();if(!safeImageUrl(normalized)){setFeedback('A imagem precisa usar um endereço HTTPS válido.');editor.chain().focus().run();return;}void insertRemoteImage(normalized);};
  return <Frame $expanded={expanded} data-description-expanded={expanded ? 'true' : 'false'} aria-label="Editor da descrição">
    <Toolbar role="toolbar" aria-label="Formatação da descrição" aria-orientation="horizontal">
      <div className="toolbarGroup" role="group" aria-label="Histórico">{button('Desfazer',false,()=>editor.chain().focus().undo().run(),<Undo2 size={17}/>,!editor.can().undo())}
      {button('Refazer',false,()=>editor.chain().focus().redo().run(),<Redo2 size={17}/>,!editor.can().redo())}</div><span className="sep"/>
      <select aria-label="Estilo do texto" value={editor.isActive('heading',{level:1})?'h1':editor.isActive('heading',{level:2})?'h2':editor.isActive('heading',{level:3})?'h3':'p'} onChange={e=>{const v=e.target.value;if(v==='p')editor.chain().focus().setParagraph().run();else editor.chain().focus().setHeading({level:Number(v.slice(1)) as 1|2|3}).run();}}><option value="p">Parágrafo</option><option value="h1">Título 1</option><option value="h2">Título 2</option><option value="h3">Título 3</option></select><span className="sep"/>
      <div className="toolbarGroup" role="group" aria-label="Texto">{button('Negrito',editor.isActive('bold'),()=>editor.chain().focus().toggleBold().run(),<Bold size={17}/>)}
      {button('Itálico',editor.isActive('italic'),()=>editor.chain().focus().toggleItalic().run(),<Italic size={17}/>)}
      {button('Sublinhado',editor.isActive('underline'),()=>editor.chain().focus().toggleUnderline().run(),<UnderlineIcon size={17}/>)}
      {button('Tachado',editor.isActive('strike'),()=>editor.chain().focus().toggleStrike().run(),<Strikethrough size={17}/>)}
      {button('Realçar',editor.isActive('highlight'),()=>editor.chain().focus().toggleHighlight().run(),<Highlighter size={17}/>)}
      {button('Inserir link',editor.isActive('link'),setLink,<Link2 size={17}/>)} </div><span className="sep"/>
      <div className="toolbarGroup" role="group" aria-label="Listas e alinhamento">{button('Lista numerada',editor.isActive('orderedList'),()=>editor.chain().focus().toggleOrderedList().run(),<ListOrdered size={17}/>)}
      {button('Lista com marcadores',editor.isActive('bulletList'),()=>editor.chain().focus().toggleBulletList().run(),<List size={17}/>)}
      {button('Alinhar à esquerda',editor.isActive({textAlign:'left'}),()=>editor.chain().focus().setTextAlign('left').run(),<AlignLeft size={17}/>)}
      {button('Centralizar',editor.isActive({textAlign:'center'}),()=>editor.chain().focus().setTextAlign('center').run(),<AlignCenter size={17}/>)}
      {button('Alinhar à direita',editor.isActive({textAlign:'right'}),()=>editor.chain().focus().setTextAlign('right').run(),<AlignRight size={17}/>)}
      {button('Checklist',editor.isActive('taskList'),()=>editor.chain().focus().toggleTaskList().run(),<CheckSquare size={17}/>)} </div><span className="sep"/>
      <div className="toolbarGroup" role="group" aria-label="Blocos">{button('Parágrafo',editor.isActive('paragraph'),()=>editor.chain().focus().setParagraph().run(),<Pilcrow size={17}/>)}
      {button('Citação',editor.isActive('blockquote'),()=>editor.chain().focus().toggleBlockquote().run(),<Quote size={17}/>)}
      {button('Bloco de código',editor.isActive('codeBlock'),()=>editor.chain().focus().toggleCodeBlock().run(),<Code size={17}/>)} </div><div className="toolbarGroup" role="group" aria-label="Inserção">
      {button('Inserir imagem por endereço',false,setImage,<ImageIcon size={17}/>)}
      {button('Abrir anexos',false,onOpenAttachments,<Paperclip size={17}/>)} </div><span className="sep"/>
      {button(expanded?'Sair da tela cheia':'Expandir editor',expanded,()=>setExpanded(v=>!v),expanded?<Minimize2 size={17}/>:<Maximize2 size={17}/>,false,expandButtonRef)}
    </Toolbar>
    <Editor $expanded={expanded} onBlur={()=>void save()}><EditorContent editor={editor}/></Editor>
    <Footer><span role="status" aria-live="polite">{feedback ?? (status==='uploading'?'Enviando imagem...':status==='saving'?'Salvando...':status==='saved'?'Salvo':status==='error'?'Falha ao salvar':'Salvamento automático')}</span><span>Cole com Ctrl+V ou arraste uma imagem</span></Footer>
  </Frame>;
}
