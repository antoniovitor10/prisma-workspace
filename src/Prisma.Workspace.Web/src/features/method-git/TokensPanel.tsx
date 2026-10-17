import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { methodApi } from './client';
import { methodKey, useMethodAction } from './useMethodAction';
import type { IssuedToken, MethodRole, MethodToken } from './types';
import { Actions, Button, Feedback, Field, Form, Hint, List, Row, Secret } from './styles';

const roleNames: Record<MethodRole, string> = { agente: 'Agente', humano: 'Humano pessoal', sistema: 'Sistema de integração' };
const date = (value: string) => new Date(value).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

export function TokensPanel({ projectId }: { projectId: string }) {
  const tokens = useQuery({ queryKey: [...methodKey(projectId), 'tokens'], queryFn: () => methodApi.tokens(projectId), retry: false });
  const action = useMethodAction(projectId);
  const [name, setName] = useState('');
  const [role, setRole] = useState<MethodRole>('agente');
  const [days, setDays] = useState(30);
  const [issued, setIssued] = useState<IssuedToken | null>(null);
  const [copyMessage, setCopyMessage] = useState('');
  const [revoke, setRevoke] = useState<MethodToken | null>(null);

  async function issue(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setIssued(null); setCopyMessage('');
    const result = await action.run('issue', () => methodApi.issueToken(projectId, { name: name.trim(), role, validDays: days }));
    if (result) { setIssued(result.value); setName(''); }
  }

  async function rotate(token: MethodToken) {
    setIssued(null); setCopyMessage('');
    const result = await action.run(`rotate-${token.id}`, () => methodApi.issueToken(projectId,
      { name: token.name, role: token.role, validDays: days, principalId: token.principalId }));
    if (result) setIssued(result.value);
  }

  return <section aria-labelledby="method-tokens-title">
    <h3 id="method-tokens-title">Tokens do projeto</h3>
    <Hint>Emita um token de agente para o MCP. Tokens humanos são pessoais e também precisam da autorização pela UI para iniciar uma task.</Hint>
    <Hint>Reemitir preserva a identidade e pode reduzir as permissões de todos os tokens desse agente ou sistema, conforme o acesso atual do emissor.</Hint>
    {action.error && <Feedback role="alert">{action.error}</Feedback>}
    {action.success && <Feedback role="status">{action.success}</Feedback>}
    {issued && <Secret>
      <h3>Salve o token agora</h3>
      <Hint>O valor completo aparece somente nesta emissão. Guarde-o em local privado para configurar a integração.</Hint>
      <Field>Token emitido<textarea readOnly rows={3} value={issued.token} spellCheck={false} autoComplete="off" /></Field>
      <Actions>
        <Button type="button" onClick={async () => {
          try { await navigator.clipboard.writeText(issued.token); setCopyMessage('Token copiado.'); }
          catch { setCopyMessage('Não foi possível copiar. Selecione o token e copie manualmente.'); }
        }}>Copiar token</Button>
        <Button type="button" onClick={() => { setIssued(null); setCopyMessage(''); }}>Já salvei o token</Button>
        {copyMessage && <span role="status">{copyMessage}</span>}
      </Actions>
    </Secret>}
    <Form onSubmit={issue}>
      <fieldset disabled={action.pending !== null}>
        <Field>Nome do token<input required maxLength={120} value={name} onChange={event => setName(event.target.value)} autoComplete="off" /></Field>
        <Field>Papel<select value={role} onChange={event => setRole(event.target.value as MethodRole)}>
          {Object.entries(roleNames).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></Field>
        <Field>Validade em dias<input required type="number" min={1} max={90} value={days} onChange={event => setDays(Number(event.target.value))} />
          <small>Entre 1 e 90 dias. Reemissões usam esta mesma validade.</small></Field>
        <Actions><Button $primary type="submit">{action.pending === 'issue' ? 'Emitindo token...' : 'Emitir token'}</Button></Actions>
      </fieldset>
    </Form>
    {tokens.isPending ? <Hint role="status">Carregando tokens...</Hint> : tokens.isError ? <Feedback role="alert">
      {tokens.error.message} <Button type="button" onClick={() => void tokens.refetch()}>Tentar novamente</Button>
    </Feedback> : tokens.data.length === 0 ? <Hint>Nenhum token emitido neste projeto.</Hint> : <List>
      {tokens.data.map(token => <Row key={token.id}>
        <div><div><strong>{token.name}</strong><small>{roleNames[token.role]} · {token.principalDisplayName}</small>
          <small>{token.revokedAtUtc ? 'Revogado' : new Date(token.expiresAtUtc) <= new Date() ? 'Expirado' : 'Ativo'} · {token.tokenPrefix}...</small>
          <small>Validade: <time dateTime={token.expiresAtUtc}>{date(token.expiresAtUtc)}</time></small>
          <small>Último uso: {token.lastUsedAtUtc ? date(token.lastUsedAtUtc) : 'ainda não utilizado'}</small></div>
          {!token.revokedAtUtc && <Actions>
            {token.role !== 'humano' && <Button type="button" disabled={action.pending !== null} onClick={() => void rotate(token)}>Reemitir {token.name}</Button>}
            <Button type="button" disabled={action.pending !== null} onClick={() => setRevoke(token)}>Revogar {token.name}</Button>
          </Actions>}
        </div>
        {revoke?.id === token.id && <Feedback>
          <p>Revogar {token.name}? A integração que usa este token perderá o acesso.</p>
          <Actions><Button type="button" disabled={action.pending !== null} onClick={() => void action.run(`revoke-${token.id}`,
            async () => { await methodApi.revokeToken(projectId, token.id); setRevoke(null); }, 'Token revogado.')}>
            Confirmar revogação</Button><Button type="button" disabled={action.pending !== null} onClick={() => setRevoke(null)}>Cancelar</Button></Actions>
        </Feedback>}
      </Row>)}
    </List>}
  </section>;
}
