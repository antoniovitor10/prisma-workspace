import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { methodApi } from './client';
import { methodKey, useMethodAction } from './useMethodAction';
import { TokensPanel } from './TokensPanel';
import { RepositoriesPanel } from './RepositoriesPanel';
import { PullRequestsPanel } from './PullRequestsPanel';
import { ResumePanel } from './ResumePanel';
import { Actions, Button, Feedback, Field, Form, Header, Hint, Navigation, Surface } from './styles';

type View = 'tokens' | 'repositories' | 'unlinked' | 'resume';
interface Props { projectId: string; boards?: { id: string; name: string }[] }

export function MethodSettingsPanel({ projectId, boards = [] }: Props) {
  const identity = useQuery({ queryKey: [...methodKey(projectId), 'identity'], queryFn: () => methodApi.identity(projectId), retry: false });
  const settings = useQuery({ queryKey: [...methodKey(projectId), 'settings'], queryFn: () => methodApi.settings(projectId), retry: false });
  const action = useMethodAction(projectId);
  const [boardId, setBoardId] = useState('');
  const [view, setView] = useState<View>('tokens');
  const interactive = identity.data?.authenticationKind === 'uiSession';
  const canManage = interactive && identity.data?.capabilities.includes('ManagePermissions') === true;
  const canMove = interactive && identity.data?.capabilities.includes('ChangeStatus') === true;
  const canEdit = interactive && identity.data?.capabilities.includes('Edit') === true;

  async function provision(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await action.run('provision', () => methodApi.provision(projectId, boardId || undefined), 'Método configurado no projeto.');
  }

  const tabs: { id: View; label: string; visible: boolean }[] = [
    { id: 'tokens', label: 'Tokens', visible: canManage },
    { id: 'repositories', label: 'Repositórios', visible: canManage },
    { id: 'unlinked', label: 'PRs não vinculados', visible: true },
    { id: 'resume', label: 'Retomadas', visible: canMove },
  ];
  const currentView = tabs.find(tab => tab.id === view && tab.visible)?.id ?? 'unlinked';

  return <Surface aria-labelledby="method-settings-title">
    <Header><div><h2 id="method-settings-title">Método de trabalho</h2>
      <Hint>Acompanhe as tasks com agentes e evidências do GitHub. As pessoas continuam movimentando os cards pelo quadro.</Hint></div>
      {settings.data && canManage && <Button type="button" disabled={action.pending !== null} onClick={() => void action.run('toggle',
        () => methodApi.updateSettings(projectId, !settings.data!.enabled, settings.data!.autonomyLevel),
        settings.data!.enabled ? 'Método pausado para integrações e agentes.' : 'Método reativado.')}>
        {settings.data.enabled ? 'Pausar método' : 'Reativar método'}</Button>}
    </Header>
    {action.error && <Feedback role="alert">{action.error}</Feedback>}
    {action.success && <Feedback role="status">{action.success}</Feedback>}
    {identity.isPending || settings.isPending ? <Hint role="status">Carregando configuração do método...</Hint>
      : identity.isError || settings.isError ? <Feedback role="alert">
        {identity.error?.message ?? settings.error?.message ?? 'Não foi possível consultar a configuração.'}
        <Actions><Button type="button" onClick={() => { void identity.refetch(); void settings.refetch(); }}>Tentar novamente</Button></Actions>
      </Feedback> : !settings.data ? <>
        <h3>Método ainda não configurado</h3>
        <Hint>O quadro terá as sete colunas do método e cada task será um card, identificado pela HU.</Hint>
        {canManage ? <Form onSubmit={provision}>
          <Field>Quadro do método<select value={boardId} onChange={event => setBoardId(event.target.value)} disabled={action.pending !== null}>
            <option value="">Criar quadro Método</option>{boards.map(board => <option value={board.id} key={board.id}>{board.name}</option>)}
          </select></Field>
          <Actions><Button $primary type="submit" disabled={action.pending !== null}>
            {action.pending === 'provision' ? 'Configurando método...' : 'Configurar método neste projeto'}</Button></Actions>
        </Form> : <Hint>Peça a um administrador com permissão de gerenciar o projeto para configurar o método.</Hint>}
      </> : <>
        <Hint>Quadro: <strong>{settings.data.boardName}</strong> · {settings.data.enabled ? 'Método ativo' : 'Método pausado'}</Hint>
        {!settings.data.enabled && <Feedback>Integrações e agentes estão pausados. Administradores podem configurar os acessos e reativar o método.</Feedback>}
        <Navigation aria-label="Seções do método">
          {tabs.filter(tab => tab.visible).map(tab => <Button key={tab.id} type="button" aria-pressed={currentView === tab.id}
            onClick={() => setView(tab.id)}>{tab.label}</Button>)}
        </Navigation>
        {currentView === 'tokens' && <TokensPanel key={projectId} projectId={projectId} />}
        {currentView === 'repositories' && <RepositoriesPanel key={projectId} projectId={projectId} />}
        {currentView === 'unlinked' && <PullRequestsPanel key={projectId} projectId={projectId} canEdit={canEdit} />}
        {currentView === 'resume' && <ResumePanel key={projectId} projectId={projectId} enabled={settings.data.enabled} />}
      </>}
  </Surface>;
}
