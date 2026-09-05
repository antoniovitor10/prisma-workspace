import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { CheckCircle2, FileUp, Inbox, KeyRound, LoaderCircle, Mail, MessageSquare, Paperclip, Search, Send, ShieldCheck, Star } from 'lucide-react';
import styled from 'styled-components';
import { api } from '../services/api';
import type { CreatedExternalRequest, ExternalForm, ExternalFormField, ExternalPortal, PublicExternalRequest } from '../types/portal';

const Shell = styled.main`
  min-height: 100vh;
  padding: 34px 18px 60px;
  background: linear-gradient(145deg, ${({ theme }) => theme.color.neutral[50]}, ${({ theme }) => theme.color.surface});
`;
const Header = styled.header`
  max-width: 920px; margin: 0 auto 18px; padding: 22px; border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.brand}; color: ${({ theme }) => theme.color.onBrand};
  h1{font-size:22px;} p{margin-top:6px;font-size:13.5px;opacity:.82;}
`;
const Card = styled.section`
  max-width: 920px; margin: 0 auto; overflow:hidden; border:1px solid ${({ theme }) => theme.color.border};
  border-radius:${({ theme }) => theme.radius.lg}; background:${({ theme }) => theme.color.surface}; box-shadow:${({ theme }) => theme.shadow.md};
`;
const Tabs = styled.nav`display:flex;border-bottom:1px solid ${({ theme }) => theme.color.border};`;
const Tab = styled.button<{ $active:boolean }>`
  display:flex;align-items:center;gap:7px;min-height:46px;padding:0 18px;border-bottom:2px solid ${({ $active, theme }) => $active ? theme.color.brand : 'transparent'};
  color:${({ $active, theme }) => $active ? theme.color.brand : theme.color.textMuted};font-size:13.5px;font-weight:800;
`;
const Content = styled.div`padding:22px;`;
const Form = styled.form`
  display:grid;grid-template-columns:1fr 1fr;gap:13px;
  label{display:grid;gap:5px;color:${({ theme }) => theme.color.textMuted};font-size:13px;font-weight:800;}
  label.wide{grid-column:1/-1;} input,textarea,select{width:100%;min-height:40px;padding:9px 11px;border:1px solid ${({ theme }) => theme.color.border};border-radius:${({ theme }) => theme.radius.md};background:${({ theme }) => theme.color.surface};color:${({ theme }) => theme.color.text};font:inherit;}textarea{min-height:120px;resize:vertical;}
  footer{grid-column:1/-1;display:flex;flex-wrap:wrap;align-items:center;justify-content:flex-end;gap:8px;}
  @media(max-width:650px){grid-template-columns:1fr;label.wide,footer{grid-column:auto;}}
`;
const Button = styled.button<{ $secondary?:boolean }>`
  display:inline-flex;min-height:37px;align-items:center;justify-content:center;gap:6px;padding:0 12px;border:1px solid ${({ $secondary, theme }) => $secondary ? theme.color.border : 'transparent'};border-radius:${({ theme }) => theme.radius.md};background:${({ $secondary, theme }) => $secondary ? theme.color.surface : theme.color.brand};color:${({ $secondary, theme }) => $secondary ? theme.color.text : theme.color.onBrand};font-size:13px;font-weight:850;&:disabled{opacity:.55;}
`;
const Notice = styled.div<{ $error?:boolean }>`
  display:flex;align-items:flex-start;gap:8px;margin-bottom:14px;padding:11px 12px;border-radius:${({ theme }) => theme.radius.md};background:${({ $error, theme }) => $error ? `color-mix(in srgb, ${theme.color.danger} 8%, white)` : `color-mix(in srgb, ${theme.color.accentBlue} 8%, white)`};color:${({ $error, theme }) => $error ? theme.color.danger : theme.color.neutral[700]};font-size:13px;line-height:1.5;
`;
const Credentials = styled.div`
  display:grid;gap:12px;padding:18px;border:1px solid ${({ theme }) => theme.color.success};border-radius:${({ theme }) => theme.radius.lg};background:color-mix(in srgb, ${({ theme }) => theme.color.success} 6%, white);
  h2{display:flex;align-items:center;gap:7px;font-size:15px;} p{font-size:13px;line-height:1.6;} code{display:block;width:fit-content;padding:7px 9px;border-radius:6px;background:white;font-size:15px;font-weight:850;letter-spacing:.05em;}
`;
const RequestHeader = styled.div`
  display:flex;flex-wrap:wrap;align-items:flex-start;justify-content:space-between;gap:12px;padding-bottom:16px;border-bottom:1px solid ${({ theme }) => theme.color.border};
  h2{font-size:16px;}p{margin-top:5px;color:${({ theme }) => theme.color.textMuted};font-size:13px;}
`;
const Status = styled.span`display:inline-flex;padding:6px 9px;border-radius:999px;background:color-mix(in srgb, ${({ theme }) => theme.color.accentBlue} 11%, white);color:${({ theme }) => theme.color.brand};font-size:12px;font-weight:850;`;
const Timeline = styled.div`display:grid;gap:9px;margin-top:18px;`;
const Message = styled.article<{ $agent:boolean }>`
  max-width:78%;padding:11px 12px;border-radius:${({ theme }) => theme.radius.lg};background:${({ $agent, theme }) => $agent ? theme.color.neutral[100] : `color-mix(in srgb, ${theme.color.accentBlue} 10%, white)`};justify-self:${({ $agent }) => $agent ? 'start' : 'end'};
  strong{display:block;font-size:12px;}p{margin-top:4px;font-size:13.5px;line-height:1.55;white-space:pre-wrap;}small{display:block;margin-top:5px;color:${({ theme }) => theme.color.textMuted};font-size:11px;}
`;
const Attachments = styled.div`display:flex;flex-wrap:wrap;gap:7px;margin-top:14px;a{display:inline-flex;align-items:center;gap:5px;padding:7px 9px;border:1px solid ${({ theme }) => theme.color.border};border-radius:${({ theme }) => theme.radius.md};color:${({ theme }) => theme.color.brand};font-size:12px;font-weight:750;}`;
const Actions = styled.div`display:grid;gap:12px;margin-top:18px;padding-top:16px;border-top:1px solid ${({ theme }) => theme.color.border};`;
const Inline = styled.form`display:flex;flex-wrap:wrap;gap:8px;input,textarea{flex:1;min-width:210px;min-height:38px;padding:9px 10px;border:1px solid ${({ theme }) => theme.color.border};border-radius:${({ theme }) => theme.radius.md};font:inherit;}textarea{min-height:70px;}`;
const Rating = styled.div`display:flex;flex-wrap:wrap;align-items:center;gap:8px;span{font-size:13px;font-weight:800;}button{color:${({ theme }) => theme.color.warning};}`;
const Empty = styled.div`padding:36px;text-align:center;color:${({ theme }) => theme.color.textMuted};font-size:13.5px;`;

const accessStorageKey = (protocol:string) => `detran_portal_${protocol}`;

export function PublicPortal() {
  const { slug = '' } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const trackingView = location.pathname.endsWith('/acompanhar');
  const query = useMemo(()=>new URLSearchParams(location.search),[location.search]);
  const [portal, setPortal] = useState<ExternalPortal | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [created, setCreated] = useState<CreatedExternalRequest | null>(null);
  const [definition, setDefinition] = useState<ExternalForm | null>(null);
  const [values, setValues] = useState<Record<string,string>>({});
  const [files, setFiles] = useState<File[]>([]);
  const [startedAt, setStartedAt] = useState(new Date().toISOString());
  const [website, setWebsite] = useState('');
  const [auth, setAuth] = useState({invitationToken:query.get('invite') ?? '',verificationCode:''});
  const [protocol, setProtocol] = useState(query.get('protocol') ?? '');
  const [accessKey, setAccessKey] = useState(query.get('key') ?? (query.get('protocol') ? localStorage.getItem(accessStorageKey(query.get('protocol')!)) ?? '' : ''));
  const [request, setRequest] = useState<PublicExternalRequest | null>(null);
  const [reply, setReply] = useState('');
  const [ratingComment, setRatingComment] = useState('');
  const [loginPassword, setLoginPassword] = useState('');
  const requesterEmailKey = definition?.fields.find(field=>field.kind===6)?.key;
  const email = requesterEmailKey ? values[requesterEmailKey] ?? query.get('email') ?? '' : query.get('email') ?? '';

  useEffect(() => {
    let active = true;
    api.getPublicPortal(slug).then((value) => { if(active)setPortal(value); }).catch((caught) => { if(active)setError(caught instanceof Error ? caught.message : 'Portal indisponível.'); }).finally(() => { if(active)setLoading(false); });
    return () => { active = false; };
  }, [slug]);

  useEffect(() => {
    if(!portal||trackingView)return;
    const requested=query.get('form');
    const summary=portal.forms.find(item=>item.publicSlug===requested)
      ?? portal.forms.find(item=>item.isDefault)
      ?? portal.forms[0];
    if(!summary){setDefinition(null);return;}
    let active=true;
    api.getPublicExternalForm(slug,summary.publicSlug).then(form=>{
      if(!active)return;
      setDefinition(form);setStartedAt(new Date().toISOString());setFiles([]);setCreated(null);
      const next:Record<string,string>={};
      const emailField=form.fields.find(field=>field.kind===6);
      if(emailField&&query.get('email'))next[emailField.key]=query.get('email')!;
      setValues(next);
    }).catch(caught=>{if(active)setError(caught instanceof Error?caught.message:'Formulário indisponível.');});
    return()=>{active=false;};
  },[portal,trackingView,query,slug]);

  const loadRequest = async (nextProtocol = protocol, nextKey = accessKey) => {
    if (!nextProtocol.trim()) return;
    setBusy(true); setError('');
    try {
      const value = await api.trackExternalRequest(nextProtocol.trim(), nextKey.trim()) as PublicExternalRequest;
      setRequest(value); setProtocol(nextProtocol.trim()); setAccessKey(nextKey.trim());
      if(nextKey.trim())localStorage.setItem(accessStorageKey(nextProtocol.trim()),nextKey.trim());
    } catch(caught) { setError(caught instanceof Error ? caught.message : 'Não foi possível localizar a solicitação.'); }
    finally { setBusy(false); }
  };

  useEffect(() => {
    if(trackingView && protocol) void loadRequest(protocol, accessKey);
    // Executa apenas ao abrir um link de acompanhamento.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const requestCode = async () => {
    if(!email)return;
    setBusy(true);setError('');setNotice('');
    try {
      const result = await api.requestPortalVerificationCode(slug,email);
      setNotice(result.developmentCode ? `Código local: ${result.developmentCode}` : 'Código enviado. Consulte seu e-mail.');
    } catch(caught){setError(caught instanceof Error?caught.message:'Falha ao enviar o código.');}
    finally{setBusy(false);}
  };

  const login = async () => {
    if(!email||!loginPassword)return;
    setBusy(true);setError('');setNotice('');
    try{await api.login(email,loginPassword);setNotice('Login confirmado. Você já pode enviar a solicitação.');setLoginPassword('');}
    catch(caught){setError(caught instanceof Error?caught.message:'Não foi possível entrar.');}
    finally{setBusy(false);}
  };

  const submitRequest = async (event:FormEvent) => {
    event.preventDefault();if(!definition)return;setBusy(true);setError('');
    try {
      const result = await api.submitExternalForm(slug,definition.publicSlug,{
        values,startedAt,website,invitationToken:auth.invitationToken,verificationCode:auth.verificationCode,
      },files) as CreatedExternalRequest;
      setCreated(result);localStorage.setItem(accessStorageKey(result.protocol),result.accessKey);
    } catch(caught){setError(caught instanceof Error?caught.message:'Não foi possível abrir a solicitação.');}
    finally{setBusy(false);}
  };

  const sendReply = async (event:FormEvent) => {
    event.preventDefault();if(!request||!reply.trim())return;setBusy(true);setError('');
    try{await api.addExternalRequestReply(request.protocol,accessKey,reply);setReply('');await loadRequest(request.protocol,accessKey);}catch(caught){setError(caught instanceof Error?caught.message:'Falha ao enviar resposta.');}finally{setBusy(false);}
  };

  const upload = async (file?:File) => {
    if(!request||!file)return;setBusy(true);setError('');
    try{await api.uploadExternalRequestAttachment(request.protocol,accessKey,file);await loadRequest(request.protocol,accessKey);}catch(caught){setError(caught instanceof Error?caught.message:'Falha no anexo.');}finally{setBusy(false);}
  };

  const rate = async (value:number) => {
    if(!request)return;setBusy(true);setError('');
    try{await api.rateExternalRequest(request.protocol,accessKey,value,ratingComment);await loadRequest(request.protocol,accessKey);}catch(caught){setError(caught instanceof Error?caught.message:'Falha ao avaliar.');}finally{setBusy(false);}
  };

  const visibleFields=(definition?.fields??[]).filter(field=>!field.conditionalFieldKey
    || values[field.conditionalFieldKey]===field.conditionalValue);
  const updateValue=(field:ExternalFormField,value:string)=>setValues(current=>({...current,[field.key]:value}));
  const selectFiles=(selected:FileList|null)=>{
    if(!definition||!selected)return;
    const next=Array.from(selected);
    const invalid=next.find(file=>file.size>definition.maxFileSizeBytes);
    if(invalid){setError(`O arquivo ${invalid.name} excede o limite de ${Math.round(definition.maxFileSizeBytes/1_000_000)} MB.`);return;}
    setError('');setFiles(next.slice(0,definition.maxFiles));
  };

  if(loading)return <Shell><Empty>Carregando portal...</Empty></Shell>;
  if(!portal)return <Shell><Empty>{error || 'Portal não encontrado.'}</Empty></Shell>;

  return <Shell>
    <Header><h1>{portal.projectName}</h1><p>Abra uma solicitação ou acompanhe o atendimento com seu protocolo.</p></Header>
    <Card>
      <Tabs><Tab $active={!trackingView} onClick={()=>navigate(`/portal/${slug}`)}><Inbox size={14}/>Nova solicitação</Tab><Tab $active={trackingView} onClick={()=>navigate(`/portal/${slug}/acompanhar`)}><Search size={14}/>Acompanhar</Tab></Tabs>
      <Content>
        {error&&<Notice $error><ShieldCheck size={15}/>{error}</Notice>}
        {notice&&<Notice><KeyRound size={15}/>{notice}</Notice>}
        {!trackingView && (created ? <Credentials><h2><CheckCircle2 size={18}/>Solicitação registrada</h2><p>{created.confirmationMessage||'Guarde o protocolo e a chave. A chave protege seus dados e não será exibida novamente.'}</p><div><small>Protocolo</small><code>{created.protocol}</code></div><div><small>Chave de acompanhamento</small><code>{created.accessKey}</code></div>{created.confirmationDelivered&&<Notice><Mail size={14}/>A confirmação também foi enviada por e-mail.</Notice>}<Button onClick={()=>navigate(created.trackingPath)}>Acompanhar agora</Button></Credentials> : definition ? <Form onSubmit={submitRequest}>
          <label className="wide">Formulário<select value={definition.publicSlug} onChange={event=>{const next=new URLSearchParams(location.search);next.set('form',event.target.value);navigate(`/portal/${slug}?${next.toString()}`);}}>{portal.forms.map(item=><option key={item.id} value={item.publicSlug}>{item.title}</option>)}</select></label>
          {definition.description&&<Notice><FileUp size={15}/>{definition.description}</Notice>}
          {portal.requiresAuthentication&&<Notice><ShieldCheck size={15}/>Este formulário exige {portal.accessModes&8?'código por e-mail':portal.accessModes&4?'convite válido':'login'}.</Notice>}
          {visibleFields.map(field=>{
            const wide=field.type===2||field.type===9;
            if(field.type===9||field.kind===9)return <label key={field.key} className={wide?'wide':undefined}>{field.label}<input required={field.isRequired} type="file" multiple={definition.maxFiles>1} accept={definition.allowedExtensions} onChange={event=>selectFiles(event.target.files)}/>{field.helpText&&<small>{field.helpText}</small>}{files.length>0&&<small>{files.length} arquivo(s) selecionado(s)</small>}</label>;
            if(field.type===2)return <label key={field.key} className="wide">{field.label}<textarea required={field.isRequired} minLength={field.minLength} maxLength={field.maxLength} placeholder={field.placeholder} value={values[field.key]??''} onChange={event=>updateValue(field,event.target.value)}/>{field.helpText&&<small>{field.helpText}</small>}</label>;
            if(field.type===5)return <label key={field.key}>{field.label}<select required={field.isRequired} value={values[field.key]??''} onChange={event=>updateValue(field,event.target.value)}><option value="">Selecione...</option>{field.options?.map(option=><option key={option} value={option}>{option}</option>)}</select>{field.helpText&&<small>{field.helpText}</small>}</label>;
            if(field.type===7)return <label key={field.key}><span><input type="checkbox" checked={values[field.key]==='true'} onChange={event=>updateValue(field,String(event.target.checked))}/> {field.label}</span>{field.helpText&&<small>{field.helpText}</small>}</label>;
            const inputType=field.type===3?'email':field.type===4?'tel':field.type===6?'number':field.type===8?'date':'text';
            return <label key={field.key}>{field.label}<input required={field.isRequired} type={inputType} minLength={field.minLength} maxLength={field.maxLength} pattern={field.validationPattern} placeholder={field.placeholder} value={values[field.key]??''} onChange={event=>updateValue(field,event.target.value)}/>{field.helpText&&<small>{field.helpText}</small>}</label>;
          })}
          <label aria-hidden="true" style={{position:'absolute',left:'-10000px'}}>Website<input tabIndex={-1} autoComplete="off" value={website} onChange={event=>setWebsite(event.target.value)}/></label>
          {portal.requiresAuthentication&&Boolean(portal.accessModes&8)&&<label>Código de verificação<input inputMode="numeric" maxLength={6} value={auth.verificationCode} onChange={event=>setAuth({...auth,verificationCode:event.target.value})}/></label>}
          {portal.requiresAuthentication&&Boolean(portal.accessModes&2)&&!api.getToken()&&<label>Senha para login<input type="password" value={loginPassword} onChange={event=>setLoginPassword(event.target.value)}/></label>}
          {auth.invitationToken&&<label>Convite<input readOnly value="Convite identificado"/></label>}
          <footer>{portal.requiresAuthentication&&Boolean(portal.accessModes&2)&&!api.getToken()&&<Button type="button" $secondary disabled={busy||!email||!loginPassword} onClick={login}><ShieldCheck size={13}/>Entrar</Button>}{portal.requiresAuthentication&&Boolean(portal.accessModes&8)&&<Button type="button" $secondary disabled={busy||!email} onClick={requestCode}><KeyRound size={13}/>Enviar código</Button>}<Button disabled={busy}>{busy?<LoaderCircle size={13}/>:<Send size={13}/>}Gerar protocolo</Button></footer>
        </Form> : <Empty>Nenhum formulário publicado neste portal.</Empty>)}
        {trackingView && <>
          <Inline onSubmit={event=>{event.preventDefault();void loadRequest();}}><input aria-label="Protocolo" placeholder="Protocolo" value={protocol} onChange={event=>setProtocol(event.target.value)}/><input aria-label="Chave" placeholder="Chave de acompanhamento" value={accessKey} onChange={event=>setAccessKey(event.target.value)}/><Button disabled={busy||!protocol}><Search size={13}/>Consultar</Button></Inline>
          {request&&<>
            <RequestHeader><div><h2>{request.title}</h2><p>{request.protocol} · aberta em {new Date(request.createdAt).toLocaleString('pt-BR')}</p></div><Status>{request.status}</Status></RequestHeader>
            {request.description&&<Notice><Inbox size={14}/>{request.description}</Notice>}
            <Timeline>{request.messages.map(message=><Message key={message.id} $agent={message.authorType===2}><strong>{message.authorName}</strong><p>{message.content}</p><small>{new Date(message.createdAt).toLocaleString('pt-BR')}</small></Message>)}{request.messages.length===0&&<Empty>A conversa aparecerá aqui quando houver uma resposta.</Empty>}</Timeline>
            <Attachments>{request.attachments.map(attachment=><a key={attachment.id} href={api.externalAttachmentUrl(request.protocol,accessKey,attachment.id)}><Paperclip size={11}/>{attachment.fileName}</a>)}</Attachments>
            <Actions>
              <Inline onSubmit={sendReply}><textarea aria-label="Resposta" placeholder="Escreva uma resposta..." value={reply} onChange={event=>setReply(event.target.value)}/><Button disabled={busy||!reply.trim()}><MessageSquare size={13}/>Responder</Button></Inline>
              <label><Button as="span" $secondary><FileUp size={13}/>Enviar anexo</Button><input hidden type="file" onChange={event=>void upload(event.target.files?.[0])}/></label>
              {request.completedAt&&!request.rating&&<Rating><span>Avalie o atendimento:</span>{[1,2,3,4,5].map(value=><button key={value} aria-label={`${value} estrelas`} onClick={()=>void rate(value)}><Star size={18}/></button>)}<input placeholder="Comentário opcional" value={ratingComment} onChange={event=>setRatingComment(event.target.value)}/></Rating>}
              {request.completedAt&&!request.completionConfirmedAt&&<Button onClick={async()=>{await api.confirmExternalRequestCompletion(request.protocol,accessKey);await loadRequest(request.protocol,accessKey);}}><CheckCircle2 size={13}/>Confirmar conclusão</Button>}
              {request.completionConfirmedAt&&<Notice><CheckCircle2 size={14}/>Conclusão confirmada pelo solicitante.</Notice>}
            </Actions>
          </>}
        </>}
      </Content>
    </Card>
  </Shell>;
}
