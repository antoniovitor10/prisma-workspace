import { useState, type ChangeEvent, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { methodApi } from './client';
import { methodKey, useMethodAction } from './useMethodAction';
import { Actions, Button, Feedback, Field, Form, Hint, List, Row } from './styles';
import { formatDateTime, gitPollStatusMessage } from './gitLabels';
import type { GitRepository } from './types';
import { ActionButton } from '../../components/ActionButton';
import { HelpButton, HelpTitleRow } from '../help/HelpPanel';

const initial = { repositoryFullName: '', repositoryId: '', defaultBranch: '', appId: '', installationId: '',
  host: 'github.com', appPrivateKeySecretRef: '', webhookSecretRef: '', systemPrincipalId: '' };
type Mode = 'app' | 'poll';

export function RepositoriesPanel({ projectId }: { projectId: string }) {
  const repositories = useQuery({ queryKey: [...methodKey(projectId), 'repositories'], queryFn: () => methodApi.repositories(projectId), retry: false });
  const tokens = useQuery({ queryKey: [...methodKey(projectId), 'tokens'], queryFn: () => methodApi.tokens(projectId), retry: false });
  const credentials = useQuery({ queryKey: [...methodKey(projectId), 'read-credentials'], queryFn: () => methodApi.readCredentials(projectId), retry: false });
  const action = useMethodAction(projectId);
  const [mode, setMode] = useState<Mode>('app');
  const [form, setForm] = useState(initial);
  const [owner, setOwner] = useState('');
  const [readToken, setReadToken] = useState('');
  const [copyMessage, setCopyMessage] = useState('');
  const systems = [...new Map((tokens.data ?? []).filter(token => token.role === 'sistema')
    .map(token => [token.principalId, token.principalDisplayName])).entries()];
  const field = (name: keyof typeof initial) => ({ value: form[name],
    onChange: (event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setForm(current => ({ ...current, [name]: event.target.value })) });
  const activeCredentials = (credentials.data ?? []).filter(credential => !credential.revokedAtUtc);
  const pollOwner = form.repositoryFullName.split('/')[0]?.trim() ?? '';
  const ownerHasToken = activeCredentials.some(credential => credential.gitHubOwner.toLowerCase() === pollOwner.toLowerCase());

  async function register(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = mode === 'app'
      ? await action.run('register', () => methodApi.registerRepository(projectId, {
        ...form, repositoryId: Number(form.repositoryId), appId: Number(form.appId), installationId: Number(form.installationId), enabled: true,
      }), 'Repositório conectado. Configure o webhook no GitHub com a URL abaixo.')
      : await action.run('register', () => methodApi.registerPollRepository(projectId, {
        repositoryFullName: form.repositoryFullName.trim(), defaultBranch: form.defaultBranch.trim(), systemPrincipalId: form.systemPrincipalId, enabled: true,
      }), 'Repositório conectado no modo consulta. A leitura de teste com o token deu certo; o Prisma consulta o GitHub de hora em hora.');
    if (result) setForm(initial);
  }

  async function saveToken(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const value = readToken;
    // O campo é limpo antes da resposta: o valor não fica na tela nem no estado depois do envio.
    setReadToken('');
    const result = await action.run('credential', () => methodApi.saveReadCredential(projectId, owner.trim(), value),
      'Token de leitura salvo e cifrado. Só o começo dele fica visível.');
    if (result) setOwner('');
  }

  return <section aria-labelledby="method-repositories-title">
    <HelpTitleRow><h3 id="method-repositories-title">Repositórios GitHub</h3><HelpButton target="integrationRepositories" label="Ajuda sobre repositórios GitHub" /></HelpTitleRow>
    <Hint>Conecte cada repositório de um jeito: pela GitHub App com webhook (quando você é admin do repositório) ou pela consulta com o token de leitura da organização (sem App nem webhook).</Hint>
    {action.error && <Feedback role="alert">{action.error}</Feedback>}
    {action.success && <Feedback role="status">{action.success}</Feedback>}
    {tokens.isError && <Feedback role="alert">{tokens.error.message}</Feedback>}
    {systems.length === 0 && !tokens.isPending && <Hint>Emita primeiro um token com papel Sistema de integração na seção Tokens.</Hint>}

    <section aria-labelledby="method-read-credentials-title">
      <h4 id="method-read-credentials-title">Tokens de leitura por organização</h4>
      <Hint>Para o modo consulta: um token fine-grained do GitHub por organização, só com leitura de Pull requests e Contents. Ele é guardado cifrado e nunca mais aparece; para trocar, salve um novo.</Hint>
      <Form onSubmit={saveToken} aria-label="Salvar token de leitura">
        <fieldset disabled={action.pending !== null}>
          <Field>Organização GitHub<input required maxLength={39} placeholder="Ex.: minha-organizacao" autoComplete="off" value={owner}
            onChange={event => setOwner(event.target.value)} /></Field>
          <Field>Token de leitura<input required type="password" autoComplete="off" spellCheck={false} placeholder="github_pat_..." value={readToken}
            onChange={event => setReadToken(event.target.value)} />
            <small>Pull requests: read e Contents: read. A organização pode exigir a aprovação de um admin.</small></Field>
          <Actions><Button as={ActionButton} $primary type="submit" loading={action.pending === 'credential'} loadingLabel="Salvando token de leitura">Salvar token</Button></Actions>
        </fieldset>
      </Form>
      {credentials.isError ? <Feedback role="alert">{credentials.error.message}</Feedback>
        : (credentials.data ?? []).length === 0 ? <Hint>Nenhum token de leitura cadastrado.</Hint>
        : <List aria-label="Tokens de leitura">
          {credentials.data!.map(credential => <Row key={credential.id}>
            <div><div><strong>{credential.gitHubOwner}</strong>
              <small>{credential.tokenPrefix}… · cadastrado em <time dateTime={credential.createdAtUtc}>{formatDateTime(credential.createdAtUtc)}</time>
                {credential.createdByName ? ` por ${credential.createdByName}` : ''} · {credential.repositoriesCount} repositório(s)</small>
              {credential.revokedAtUtc && <small>Revogado em <time dateTime={credential.revokedAtUtc}>{formatDateTime(credential.revokedAtUtc)}</time></small>}</div>
              {!credential.revokedAtUtc && <Button as={ActionButton} type="button" disabled={action.pending !== null && action.pending !== credential.id}
                loading={action.pending === credential.id} loadingLabel="Revogando token de leitura" onClick={() => {
                if (window.confirm(`Revogar o token de leitura de ${credential.gitHubOwner}? Os repositórios dela param de ser consultados até um novo token.`))
                  return action.run(credential.id, () => methodApi.revokeReadCredential(projectId, credential.id), 'Token de leitura revogado.');
              }}>Revogar token</Button>}
            </div>
          </Row>)}
        </List>}
    </section>

    <Form onSubmit={register} aria-label="Conectar repositório">
      <fieldset disabled={action.pending !== null || systems.length === 0}>
        <div role="radiogroup" aria-label="Como o Prisma lê este repositório" style={{ gridColumn: '1 / -1', display: 'flex', flexWrap: 'wrap', gap: 16 }}>
          <label><input type="radio" name="repository-mode" value="app" checked={mode === 'app'} onChange={() => setMode('app')} /> GitHub App + webhook</label>
          <label><input type="radio" name="repository-mode" value="poll" checked={mode === 'poll'} onChange={() => setMode('poll')} /> Consulta com token de leitura</label>
        </div>
        <Field>Repositório<input required maxLength={300} placeholder="equipe/projeto" {...field('repositoryFullName')} /></Field>
        {mode === 'app' && <Field>ID do repositório<input required type="number" min={1} step={1} {...field('repositoryId')} /></Field>}
        <Field>Branch principal<input required maxLength={255} placeholder="Ex.: main" {...field('defaultBranch')} /></Field>
        <Field>Sistema de integração<select required {...field('systemPrincipalId')}>
          <option value="">Selecione o sistema</option>{systems.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
        </select></Field>
        {mode === 'app' ? <>
          <Field>GitHub App ID<input required type="number" min={1} step={1} {...field('appId')} /></Field>
          <Field>Installation ID<input required type="number" min={1} step={1} {...field('installationId')} /></Field>
          <Field>Servidor GitHub<input required maxLength={255} {...field('host')} /></Field>
          <Field>Referência da chave privada<input required maxLength={128} pattern="[A-Za-z0-9_-]{1,128}" placeholder="github-app-key" autoComplete="off" {...field('appPrivateKeySecretRef')} />
            <small>Nome da configuração já cadastrada no servidor.</small></Field>
          <Field>Referência do segredo do webhook<input required maxLength={128} pattern="[A-Za-z0-9_-]{1,128}" placeholder="github-webhook-secret" autoComplete="off" {...field('webhookSecretRef')} /></Field>
          <Hint style={{ gridColumn: '1 / -1' }}>No webhook do GitHub, selecione somente os eventos pull_request, pull_request_review e create.</Hint>
        </> : <Hint style={{ gridColumn: '1 / -1' }}>
          {pollOwner && !ownerHasToken ? `Cadastre antes o token de leitura da organização ${pollOwner} (acima).`
            : 'O Prisma lê o repositório com o token da organização para confirmar o acesso e passa a consultá-lo de hora em hora.'}
        </Hint>}
        <Actions><Button as={ActionButton} $primary type="submit" loading={action.pending === 'register'} loadingLabel="Conectando repositório">Conectar repositório</Button></Actions>
      </fieldset>
    </Form>
    {repositories.isPending ? <Hint role="status">Carregando repositórios...</Hint> : repositories.isError ? <Feedback role="alert">
      {repositories.error.message} <Button as={ActionButton} type="button" loadingLabel="Carregando repositórios" onClick={() => repositories.refetch()}>Tentar novamente</Button>
    </Feedback> : repositories.data.length === 0 ? <Hint>Nenhum repositório conectado.</Hint> : <List aria-label="Repositórios conectados">
      {repositories.data.map(repository => <RepositoryRow key={repository.id} projectId={projectId} repository={repository} action={action}
        onCopy={setCopyMessage} />)}
    </List>}
    {copyMessage && <Hint role="status">{copyMessage}</Hint>}
  </section>;
}

function RepositoryRow({ projectId, repository, action, onCopy }: {
  projectId: string; repository: GitRepository; action: ReturnType<typeof useMethodAction>; onCopy: (message: string) => void;
}) {
  const poll = repository.mode === 'poll';
  const pollError = poll ? gitPollStatusMessage(repository.lastPollStatus) : null;
  const [pollMessage, setPollMessage] = useState('');
  async function pollNow() {
    setPollMessage('');
    // A lista é recarregada mesmo quando a consulta falha: o erro aparece no próprio repositório.
    const result = await action.run(`poll:${repository.id}`, () => methodApi.pollRepository(projectId, repository.id));
    if (result?.value.success)
      setPollMessage(`Consulta de ${repository.repositoryFullName} concluída em ${formatDateTime(result.value.lastPolledAtUtc ?? new Date().toISOString())}: ${result.value.pullRequestsEvaluated} PR(s) reavaliado(s). Os cards se movem em instantes, se o GitHub mudou.`);
  }
  return <Row aria-label={`Repositório ${repository.repositoryFullName}`}>
    <div><div><strong>{repository.repositoryFullName}</strong><small>{repository.enabled ? 'Ativo' : 'Pausado'} · Principal: {repository.defaultBranch}</small>
      {poll ? <small>Consulta com token de leitura · {repository.lastPolledAtUtc
        ? <>última consulta com sucesso: <time dateTime={repository.lastPolledAtUtc}>{formatDateTime(repository.lastPolledAtUtc)}</time></>
        : 'nenhuma consulta com sucesso ainda'}</small>
        : <small>App {repository.appId} · Instalação {repository.installationId}</small>}</div>
      <Actions style={{ gridColumn: 'auto' }}>
        {poll && repository.enabled && <Button as={ActionButton} type="button" disabled={action.pending !== null && action.pending !== `poll:${repository.id}`}
          loading={action.pending === `poll:${repository.id}`} loadingLabel="Consultando o GitHub" onClick={() => pollNow()}>Consultar agora</Button>}
        <Button as={ActionButton} type="button" disabled={action.pending !== null && action.pending !== repository.id} loading={action.pending === repository.id}
          loadingLabel={repository.enabled ? 'Pausando integração' : 'Reativando integração'} onClick={() => action.run(repository.id,
          () => methodApi.updateRepository(projectId, repository.id, !repository.enabled), repository.enabled ? 'Integração pausada.' : 'Integração reativada.')}>
          {repository.enabled ? 'Pausar integração' : 'Reativar integração'}</Button>
      </Actions>
    </div>
    {pollMessage && <Hint role="status">{pollMessage}</Hint>}
    {pollError && <Feedback role="alert">{pollError}</Feedback>}
    {repository.operationalError && <Feedback role="alert">{repository.operationalError}</Feedback>}
    {!poll && <>
      <Field>URL do webhook<input readOnly value={methodApi.webhookUrl(repository.id)} /></Field>
      <Actions><Button type="button" onClick={async () => {
        try { await navigator.clipboard.writeText(methodApi.webhookUrl(repository.id)); onCopy('URL do webhook copiada.'); }
        catch { onCopy('Selecione a URL do webhook e copie manualmente.'); }
      }}>Copiar URL do webhook</Button></Actions>
    </>}
  </Row>;
}
