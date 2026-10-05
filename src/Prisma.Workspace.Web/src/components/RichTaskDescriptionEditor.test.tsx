import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { theme } from '../styles/theme';
import { RichTaskDescriptionEditor } from './RichTaskDescriptionEditor';

afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); cleanup(); });

function renderEditor(onUploadImage?: (file:File)=>Promise<{attachmentId:string;alt:string}>) {
  const onSave = vi.fn();
  const onOpenAttachments = vi.fn();
  render(
    <ThemeProvider theme={theme}>
      <RichTaskDescriptionEditor
        value="<p>Descrição inicial</p>"
        onSave={onSave}
        onOpenAttachments={onOpenAttachments}
        onUploadImage={onUploadImage}
      />
    </ThemeProvider>,
  );
  return { onSave, onOpenAttachments };
}

describe('RichTaskDescriptionEditor', () => {
  it('oferece os controles de formatação essenciais em português', () => {
    renderEditor();

    expect(screen.getByRole('textbox', { name: 'Descrição da tarefa' })).toHaveTextContent('Descrição inicial');
    for (const control of [
      'Negrito', 'Itálico', 'Sublinhado', 'Tachado', 'Realçar', 'Inserir link',
      'Lista numerada', 'Lista com marcadores', 'Checklist', 'Citação',
      'Bloco de código', 'Inserir imagem por endereço', 'Abrir anexos',
    ]) {
      expect(screen.getByRole('button', { name: control })).toBeInTheDocument();
    }
  });

  it('abre anexos e alterna o modo de tela cheia', () => {
    const { onOpenAttachments } = renderEditor();

    fireEvent.click(screen.getByRole('button', { name: 'Abrir anexos' }));
    expect(onOpenAttachments).toHaveBeenCalledOnce();

    fireEvent.click(screen.getByRole('button', { name: 'Expandir editor' }));
    expect(screen.getByRole('button', { name: 'Sair da tela cheia' })).toBeInTheDocument();

    fireEvent.keyDown(window, { key: 'Escape' });
    expect(screen.getByRole('button', { name: 'Expandir editor' })).toBeInTheDocument();
  });

  it('recusa URLs inseguras sem perder o foco da edição', () => {
    renderEditor();
    vi.spyOn(window, 'prompt').mockReturnValue('javascript:alert(1)');
    fireEvent.click(screen.getByRole('button', { name: 'Inserir imagem por endereço' }));
    expect(screen.getByRole('status')).toHaveTextContent('HTTPS');
  });

  it('renderiza uma URL de imagem válida em vez de inserir somente um link', async () => {
    const onUploadImage=vi.fn().mockResolvedValue({attachmentId:'attachment-url',alt:'captura.png'});
    vi.stubGlobal('fetch',vi.fn().mockResolvedValue({
      ok:true,
      headers:new Headers({'content-type':'image/png','content-length':'6'}),
      blob:()=>Promise.resolve(new Blob(['imagem'],{type:'image/png'})),
    }));
    renderEditor(onUploadImage);
    vi.spyOn(window,'prompt').mockReturnValue('https://cdn.example.com/captura.png');

    fireEvent.click(screen.getByRole('button',{name:'Inserir link'}));

    const image=await screen.findByRole('img',{name:'captura.png'});
    expect(onUploadImage).toHaveBeenCalledWith(expect.objectContaining({name:'captura.png',type:'image/png'}));
    expect(image).toHaveAttribute('data-attachment-id','attachment-url');
    expect(image).toHaveAttribute('src','/image-placeholder.svg');
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Imagem anexada e inserida');
  });

  it('não insere imagem quebrada quando o endereço não pode ser carregado', async () => {
    const onUploadImage=vi.fn();
    vi.stubGlobal('fetch',vi.fn().mockResolvedValue({
      ok:true,
      headers:new Headers({'content-type':'text/html'}),
      blob:()=>Promise.resolve(new Blob(['erro'],{type:'text/html'})),
    }));
    renderEditor(onUploadImage);
    vi.spyOn(window,'prompt').mockReturnValue('https://cdn.example.com/ausente.png');

    fireEvent.click(screen.getByRole('button',{name:'Inserir imagem por endereço'}));

    await waitFor(()=>expect(screen.getByRole('status')).toHaveTextContent('não retornou uma imagem'));
    expect(onUploadImage).not.toHaveBeenCalled();
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('envia e insere uma imagem colada sem incorporar o arquivo no HTML', async () => {
    const onUploadImage=vi.fn().mockResolvedValue({attachmentId:'attachment-1',alt:'captura.png'});
    renderEditor(onUploadImage);
    const textbox=screen.getByRole('textbox',{name:'Descrição da tarefa'});
    const file=new File(['imagem'],'captura.png',{type:'image/png'});

    fireEvent.paste(textbox,{clipboardData:{files:[file],getData:()=>''}});

    await waitFor(()=>expect(onUploadImage).toHaveBeenCalledWith(file));
    await waitFor(()=>expect(screen.getByRole('status')).toHaveTextContent('Imagem colada e anexada'));
    const image=screen.getByRole('img',{name:'captura.png'});
    expect(image).toHaveAttribute('data-attachment-id','attachment-1');
    expect(image.getAttribute('src')).not.toMatch(/^data:/);
  });

  it('envia e insere uma imagem arrastada sem incorporar o arquivo no HTML', async () => {
    const onUploadImage=vi.fn().mockResolvedValue({attachmentId:'attachment-drop',alt:'diagrama.png'});
    renderEditor(onUploadImage);
    const textbox=screen.getByRole('textbox',{name:'Descrição da tarefa'});
    const file=new File(['imagem'],'diagrama.png',{type:'image/png'});
    Object.defineProperty(document,'elementFromPoint',{configurable:true,value:()=>textbox});

    fireEvent.drop(textbox,{dataTransfer:{files:[file],getData:()=>''},clientX:0,clientY:0});

    await waitFor(()=>expect(onUploadImage).toHaveBeenCalledWith(file));
    await waitFor(()=>expect(screen.getByRole('status')).toHaveTextContent('Imagem arrastada e anexada'));
    const image=screen.getByRole('img',{name:'diagrama.png'});
    expect(image).toHaveAttribute('data-attachment-id','attachment-drop');
    expect(image.getAttribute('src')).not.toMatch(/^data:/);
  });
});
