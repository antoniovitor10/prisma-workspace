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
import {
  AlignCenter, AlignLeft, AlignRight, Bold, CheckSquare, Code, Highlighter,
  Image as ImageIcon, Italic, Link2, List, ListOrdered, Maximize2, Minimize2,
  Paperclip, Pilcrow, Quote, Redo2, Strikethrough, Underline as UnderlineIcon, Undo2,
} from 'lucide-react';
import styled from 'styled-components';
import { api } from '../services/api';

const PASTED_IMAGE_PLACEHOLDER = '/image-placeholder.svg';

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
        if(!images.length||!onUploadImageRef.current)return false;
        event.preventDefault();
        const insertionPosition=view.state.selection.from;
        void (async()=>{
          setStatus('uploading');
          setFeedback(images.length===1?'Enviando imagem colada...':`Enviando ${images.length} imagens coladas...`);
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
            setFeedback(images.length===1?'Imagem colada e anexada à tarefa.':`${images.length} imagens coladas e anexadas à tarefa.`);
            window.requestAnimationFrame(()=>void hydrateImagesRef.current());
          }catch{
            setStatus('error');
            setFeedback('Não foi possível enviar a imagem colada. Tente novamente.');
          }
        })();
        return true;
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
  const setLink=()=>{const previous=editor.getAttributes('link').href as string|undefined;const href=window.prompt('Endereço do link',previous??'https://');if(href===null)return;if(!href.trim()){editor.chain().focus().unsetLink().run();return;}if(!safeUrl(href.trim())){setFeedback('Use um endereço http:// ou https:// válido.');editor.chain().focus().run();return;}editor.chain().focus().extendMarkRange('link').setLink({href:href.trim()}).run();};
  const setImage=()=>{const src=window.prompt('Endereço público da imagem','https://');if(!src?.trim())return;if(!safeUrl(src.trim())){setFeedback('A imagem precisa usar um endereço http:// ou https:// válido.');editor.chain().focus().run();return;}editor.chain().focus().setImage({src:src.trim()}).run();};
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
    <Footer><span role="status" aria-live="polite">{feedback ?? (status==='uploading'?'Enviando imagem...':status==='saving'?'Salvando...':status==='saved'?'Salvo':status==='error'?'Falha ao salvar':'Salvamento automático')}</span><span>Cole uma imagem com Ctrl+V</span></Footer>
  </Frame>;
}
