import { useState, type ChangeEvent, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { methodApi } from './client';
import { methodKey, useMethodAction } from './useMethodAction';
import { Actions, Button, Feedback, Field, Form, Hint, List, Row } from './styles';

const initial = { repositoryFullName: '', repositoryId: '', defaultBranch: '', appId: '', installationId: '',
  host: 'github.com', appPrivateKeySecretRef: '', webhookSecretRef: '', systemPrincipalId: '' };

export function RepositoriesPanel({ projectId }: { projectId: string }) {
  const repositories = useQuery({ queryKey: [...methodKey(projectId), 'repositories'], queryFn: () => methodApi.repositories(projectId), retry: false });
  const tokens = useQuery({ queryKey: [...methodKey(projectId), 'tokens'], queryFn: () => methodApi.tokens(projectId), retry: false });
  const action = useMethodAction(projectId);
  const [form, setForm] = useState(initial);
  const [copyMessage, setCopyMessage] = useState('');
  const systems = [...new Map((tokens.data ?? []).filter(token => token.role === 'sistema')
    .map(token => [token.principalId, token.principalDisplayName])).entries()];
  const field = (name: keyof typeof initial) => ({ value: form[name],
    onChange: (event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setForm(current => ({ ...current, [name]: event.target.value })) });

  async function register(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = await action.run('register', () => methodApi.registerRepository(projectId, {
      ...form, repositoryId: Number(form.repositoryId), appId: Number(form.appId), installationId: Number(form.installationId), enabled: true,
    }), 'Repositório conectado. Configure o webhook no GitHub com a URL abaixo.');
    if (result) setForm(initial);
  }

  return <section aria-labelledby="method-repositories-title">
    <h3 id="method-repositories-title">Repositórios GitHub</h3>
    <Hint>Conecte a instalação da GitHub App ao repositório e ao sistema que registrará os movimentos.</Hint>
    <Hint>No webhook do GitHub, selecione somente os eventos pull_request, pull_request_review e create.</Hint>
    {action.error && <Feedback role="alert">{action.error}</Feedback>}
    {action.success && <Feedback role="status">{action.success}</Feedback>}
    {tokens.isError && <Feedback role="alert">{tokens.error.message}</Feedback>}
    {systems.length === 0 && !tokens.isPending && <Hint>Emita primeiro um token com papel Sistema de integração na seção Tokens.</Hint>}
    <Form onSubmit={register}>
      <fieldset disabled={action.pending !== null || systems.length === 0}>
        <Field>Repositório<input required maxLength={300} placeholder="equipe/projeto" {...field('repositoryFullName')} /></Field>
        <Field>ID do repositório<input required type="number" min={1} step={1} {...field('repositoryId')} /></Field>
        <Field>Branch principal<input required maxLength={255} placeholder="Ex.: main" {...field('defaultBranch')} /></Field>
        <Field>Sistema de integração<select required {...field('systemPrincipalId')}>
          <option value="">Selecione o sistema</option>{systems.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
        </select></Field>
        <Field>GitHub App ID<input required type="number" min={1} step={1} {...field('appId')} /></Field>
        <Field>Installation ID<input required type="number" min={1} step={1} {...field('installationId')} /></Field>
        <Field>Servidor GitHub<input required maxLength={255} {...field('host')} /></Field>
        <Field>Referência da chave privada<input required maxLength={128} pattern="[A-Za-z0-9_-]{1,128}" placeholder="github-app-key" autoComplete="off" {...field('appPrivateKeySecretRef')} />
          <small>Nome da configuração já cadastrada no servidor.</small></Field>
        <Field>Referência do segredo do webhook<input required maxLength={128} pattern="[A-Za-z0-9_-]{1,128}" placeholder="github-webhook-secret" autoComplete="off" {...field('webhookSecretRef')} /></Field>
        <Actions><Button $primary type="submit">{action.pending === 'register' ? 'Conectando...' : 'Conectar repositório'}</Button></Actions>
      </fieldset>
    </Form>
    {repositories.isPending ? <Hint role="status">Carregando repositórios...</Hint> : repositories.isError ? <Feedback role="alert">
      {repositories.error.message} <Button type="button" onClick={() => void repositories.refetch()}>Tentar novamente</Button>
    </Feedback> : repositories.data.length === 0 ? <Hint>Nenhum repositório conectado.</Hint> : <List>
      {repositories.data.map(repository => <Row key={repository.id}>
        <div><div><strong>{repository.repositoryFullName}</strong><small>{repository.enabled ? 'Ativo' : 'Pausado'} · Principal: {repository.defaultBranch}</small>
          <small>App {repository.appId} · Instalação {repository.installationId}</small></div>
          <Button type="button" disabled={action.pending !== null} onClick={() => void action.run(repository.id,
            () => methodApi.updateRepository(projectId, repository.id, !repository.enabled), repository.enabled ? 'Integração pausada.' : 'Integração reativada.')}>
            {repository.enabled ? 'Pausar integração' : 'Reativar integração'}</Button>
        </div>
        {repository.operationalError && <Feedback role="alert">{repository.operationalError}</Feedback>}
        <Field>URL do webhook<input readOnly value={methodApi.webhookUrl(repository.id)} /></Field>
        <Actions><Button type="button" onClick={async () => {
          try { await navigator.clipboard.writeText(methodApi.webhookUrl(repository.id)); setCopyMessage('URL do webhook copiada.'); }
          catch { setCopyMessage('Selecione a URL do webhook e copie manualmente.'); }
        }}>Copiar URL do webhook</Button></Actions>
      </Row>)}
    </List>}
    {copyMessage && <Hint role="status">{copyMessage}</Hint>}
  </section>;
}
