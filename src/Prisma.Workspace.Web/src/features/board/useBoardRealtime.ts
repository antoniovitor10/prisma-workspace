import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { api } from '../../services/api';

export function useBoardRealtime(boardId:string,onChanged:()=>void){
  const callback=useRef(onChanged);
  useEffect(()=>{callback.current=onChanged;},[onChanged]);
  useEffect(()=>{
    const organizationId=api.getOrganizationId();
    const token=api.getToken();
    if(!boardId||!organizationId||!token)return;
    let disposed=false;
    let debounce:number|undefined;
    const connection=new HubConnectionBuilder()
      .withUrl(`${api.baseUrl}/hubs/boards`,{accessTokenFactory:()=>api.getToken()??''})
      .withAutomaticReconnect([0,1500,5000,10000])
      .configureLogging(LogLevel.Warning)
      .build();
    const join=()=>connection.invoke('JoinBoard',organizationId,boardId).catch(()=>undefined);
    connection.on('boardChanged',()=>{
      window.clearTimeout(debounce);
      debounce=window.setTimeout(()=>callback.current(),120);
    });
    connection.onreconnected(()=>join());
    connection.start().then(()=>{if(!disposed)return join();}).catch(()=>undefined);
    return()=>{disposed=true;window.clearTimeout(debounce);connection.stop().catch(()=>undefined);};
  },[boardId]);
}
