import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bell, CheckCheck, Mail, Settings2 } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import styled from 'styled-components';
import { previewMode } from '../../preview';
import { notificationService } from './services';
import type { NotificationPage, NotificationPreference } from './types';

const Root = styled.div`position:relative;`;
const Trigger = styled.button`
  position:relative;display:grid;width:34px;height:34px;place-items:center;border-radius:${({theme})=>theme.radius.md};
  color:${({theme})=>theme.color.textMuted};&:hover{background:${({theme})=>theme.color.neutral[100]};color:${({theme})=>theme.color.text};}
`;
const Badge = styled.span`
  position:absolute;top:1px;right:0;display:grid;min-width:16px;height:16px;place-items:center;padding:0 4px;
  border:2px solid ${({theme})=>theme.color.surface};border-radius:10px;background:${({theme})=>theme.color.danger};color:white;font-size:11px;font-weight:900;
`;
const Panel = styled.section`
  position:absolute;top:42px;right:-38px;z-index:70;width:min(390px,calc(100vw - 20px));max-height:min(620px,78vh);overflow:hidden;
  border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.lg};background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.lg};
`;
const Header = styled.header`
  display:flex;align-items:center;gap:8px;padding:13px 14px;border-bottom:1px solid ${({theme})=>theme.color.border};
  h2{font-size:15px;}button{margin-left:auto;display:flex;align-items:center;gap:5px;color:${({theme})=>theme.color.brand};font-size:12px;font-weight:800;}
`;
const Tabs = styled.div`
  display:flex;padding:7px;border-bottom:1px solid ${({theme})=>theme.color.border};
  button{flex:1;min-height:30px;border-radius:${({theme})=>theme.radius.md};font-size:13px;font-weight:800;&[aria-selected="true"]{background:${({theme})=>theme.color.neutral[100]};color:${({theme})=>theme.color.brand};}}
`;
const List = styled.div`max-height:480px;overflow:auto;`;
const Item = styled.button<{ $unread:boolean }>`
  display:grid;width:100%;gap:4px;padding:12px 14px;border-bottom:1px solid ${({theme})=>theme.color.neutral[100]};text-align:left;
  background:${({$unread,theme})=>$unread?`color-mix(in srgb, ${theme.color.accentBlue} 6%, ${theme.color.surface})`:'transparent'};
  strong{font-size:13.5px;color:${({theme})=>theme.color.text};}p{font-size:13px;line-height:1.45;color:${({theme})=>theme.color.textMuted};}small{font-size:12px;color:${({theme})=>theme.color.textMuted};}
  &:hover{background:${({theme})=>theme.color.neutral[50]};}
`;
const PreferenceRow = styled.div`
  display:grid;grid-template-columns:minmax(0,1fr) 74px 74px;align-items:center;gap:7px;padding:10px 14px;border-bottom:1px solid ${({theme})=>theme.color.neutral[100]};
  strong{font-size:13px;}label{display:flex;align-items:center;gap:5px;color:${({theme})=>theme.color.textMuted};font-size:12px;}
`;
const Empty = styled.p`padding:34px 16px;text-align:center;color:${({theme})=>theme.color.textMuted};font-size:13.5px;`;

const formatDate = (value:string) => new Intl.DateTimeFormat('pt-BR', {
  day:'2-digit',month:'2-digit',hour:'2-digit',minute:'2-digit'
}).format(new Date(value));

export function NotificationCenter() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const rootRef = useRef<HTMLDivElement>(null);
  const [open,setOpen] = useState(false);
  const [tab,setTab] = useState<'items'|'preferences'>('items');
  const notifications = useQuery<NotificationPage>({
    queryKey:['notifications'],queryFn:notificationService.list,enabled:!previewMode,
    refetchInterval:30_000,initialData:previewMode?{items:[],total:0,unread:0,page:1,pageSize:30}:undefined,
  });
  const preferences = useQuery<NotificationPreference[]>({
    queryKey:['notification-preferences'],queryFn:notificationService.preferences,
    enabled:open&&tab==='preferences'&&!previewMode,
  });
  const refresh = () => queryClient.invalidateQueries({queryKey:['notifications']});
  const markAll = useMutation({mutationFn:notificationService.markAllRead,onSuccess:refresh});
  const savePreference = useMutation({
    mutationFn:notificationService.setPreference,
    onSuccess:()=>queryClient.invalidateQueries({queryKey:['notification-preferences']}),
  });

  useEffect(()=>{
    if(!open)return;
    const close=(event:MouseEvent)=>{if(!rootRef.current?.contains(event.target as Node))setOpen(false);};
    document.addEventListener('mousedown',close);
    return()=>document.removeEventListener('mousedown',close);
  },[open]);

  const openItem=async(id:string,link?:string|null)=>{
    if(!previewMode)await notificationService.markRead(id);
    await refresh();setOpen(false);if(link)navigate(link);
  };
  const update=(item:NotificationPreference,field:'inAppEnabled'|'emailEnabled',checked:boolean)=>
    savePreference.mutate({...item,[field]:checked});

  return <Root ref={rootRef}>
    <Trigger aria-label={`Notificações${notifications.data?.unread?` (${notifications.data.unread} não lidas)`:''}`} aria-expanded={open} onClick={()=>setOpen(value=>!value)}>
      <Bell size={17}/>{Boolean(notifications.data?.unread)&&<Badge>{Math.min(99,notifications.data!.unread)}</Badge>}
    </Trigger>
    {open&&<Panel aria-label="Central de notificações">
      <Header><Bell size={15}/><h2>Notificações</h2>{tab==='items'&&Boolean(notifications.data?.unread)&&<button onClick={()=>markAll.mutate()}><CheckCheck size={12}/>Marcar lidas</button>}</Header>
      <Tabs><button aria-selected={tab==='items'} onClick={()=>setTab('items')}><Bell size={11}/> Central</button><button aria-selected={tab==='preferences'} onClick={()=>setTab('preferences')}><Settings2 size={11}/> Preferências</button></Tabs>
      {tab==='items'?<List>{notifications.data?.items.map(item=><Item key={item.id} $unread={!item.isRead} onClick={()=>void openItem(item.id,item.link)}>
        <strong>{item.title}</strong><p>{item.message}</p><small>{item.typeName} · {formatDate(item.createdAt)}</small>
      </Item>)}{!notifications.data?.items.length&&<Empty>Nenhuma notificação por enquanto.</Empty>}</List>
      :<List>{preferences.data?.map(item=><PreferenceRow key={item.type}><strong>{item.name}</strong>
        <label><input type="checkbox" checked={item.inAppEnabled} onChange={event=>update(item,'inAppEnabled',event.target.checked)}/><Bell size={10}/>In-app</label>
        <label><input type="checkbox" checked={item.emailEnabled} onChange={event=>update(item,'emailEnabled',event.target.checked)}/><Mail size={10}/>E-mail</label>
      </PreferenceRow>)}{preferences.isLoading&&<Empty>Carregando preferências...</Empty>}</List>}
    </Panel>}
  </Root>;
}
