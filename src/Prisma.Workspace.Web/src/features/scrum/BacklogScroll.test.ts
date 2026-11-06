import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { subscribeBacklogScroll } from './BacklogScroll';
const owner = '/projects/project-1/backlog';
let originalUrl: string;
let originalScroll: PropertyDescriptor | undefined;
let stop = () => {};
const frames: FrameRequestCallback[] = [];
beforeEach(() => {
 originalUrl=window.location.href;originalScroll=Object.getOwnPropertyDescriptor(window,'scrollY');frames.length=0;
 window.history.replaceState({},'',owner);
 Object.defineProperty(window,'scrollY',{configurable:true,value:600});
 vi.stubGlobal('requestAnimationFrame',vi.fn((callback:FrameRequestCallback)=>{frames.push(callback);return frames.length;}));
 vi.stubGlobal('cancelAnimationFrame',vi.fn());
});
afterEach(()=>{stop();stop=()=>{};vi.unstubAllGlobals();window.history.replaceState({},'',originalUrl);if(originalScroll)Object.defineProperty(window,'scrollY',originalScroll);});
const write = () => vi.fn((position:number)=>window.history.replaceState({},'',`${owner}?bl.y=${position}`));
it('grava a posição enquanto a localização continua sendo a dona do listener',()=>{
 const persist=write();stop=subscribeBacklogScroll(owner,persist);
 window.dispatchEvent(new Event('scroll'));frames[0](0);
 expect(persist).toHaveBeenCalledWith(600);expect(window.location.search).toBe('?bl.y=600');
});
it('um frame já agendado não devolve o navegador ao backlog depois de voltar ao quadro',()=>{
 const persist=write();stop=subscribeBacklogScroll(owner,persist);
 window.dispatchEvent(new Event('scroll'));expect(frames).toHaveLength(1);
 window.history.replaceState({},'','/projects/project-1/boards');frames[0](0);
 expect(persist).not.toHaveBeenCalled();expect(window.location.pathname).toBe('/projects/project-1/boards');
});
it('ignora scroll emitido depois da troca de rota antes do cleanup React',()=>{
 const persist=write();stop=subscribeBacklogScroll(owner,persist);
 window.history.replaceState({},'','/projects/project-1/boards');window.dispatchEvent(new Event('scroll'));
 expect(frames).toHaveLength(0);expect(persist).not.toHaveBeenCalled();
});
it('preserva também uma entrada de histórico com outra query na mesma rota',()=>{
 const persist=write();stop=subscribeBacklogScroll(owner,persist);
 window.dispatchEvent(new Event('scroll'));
 window.history.replaceState({},'',`${owner}?bl.kind=5`);frames[0](0);
 expect(persist).not.toHaveBeenCalled();expect(window.location.search).toBe('?bl.kind=5');
});
