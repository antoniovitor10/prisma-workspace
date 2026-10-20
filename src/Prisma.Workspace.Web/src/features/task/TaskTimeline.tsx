import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import styled from 'styled-components';
import { api } from '../../services/api';
import { TaskStateGraph } from './TaskStateGraph';

type Movement = {
  id: string; kind: 'created_in_column' | 'column_entered' | 'column_moved'; occurredAt: string;
  actorName?: string | null; fromColumnName?: string | null; toColumnName: string;
};
type Timeline = { items: Movement[]; usesCurrentColumnNames: boolean };
const Entries = styled.ol`
  list-style:none;padding:0;margin:16px 0;display:grid;gap:12px;
  li{min-width:0;padding:12px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};}
  p{margin:0;line-height:1.6;overflow-wrap:anywhere;}
  time,small{display:block;margin-top:6px;color:${({theme})=>theme.color.textMuted};font-size:12px;}
`;
const Toggle = styled.button`
  min-height:40px;padding:8px 12px;border:1px solid ${({theme})=>theme.color.border};
  border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.text};cursor:pointer;
`;

export function TaskTimeline({ workItemId, createdAt }: { workItemId: string; createdAt?: string }) {
  const [graph, setGraph] = useState(false);
  const query = useQuery<Timeline>({ queryKey: ['task-timeline', workItemId], queryFn: () => api.getTaskTimeline(workItemId) });
  const creator = query.data?.items.find(item => item.kind === 'created_in_column')?.actorName;
  return <div>
    <Toggle type="button" onClick={() => setGraph(previous => !previous)}>
      {graph ? 'Voltar à linha do tempo' : 'Ver grafo de estados'}
    </Toggle>
    {graph ? <><h3>Grafo de estados</h3><TaskStateGraph workItemId={workItemId} createdAt={createdAt} createdByName={creator ?? undefined} /></> : <>
      {query.isLoading && <p role="status">Carregando movimentações...</p>}
      {query.isError && <div role="alert"><p>Não foi possível carregar a linha do tempo.</p><Toggle type="button" onClick={() => void query.refetch()}>Tentar novamente</Toggle></div>}
      {!query.isLoading && !query.isError && !query.data?.items.length && <p>Nenhuma movimentação registrada.</p>}
      {!!query.data?.items.length && <Entries aria-label="Movimentações entre colunas">
        {query.data.items.map(item => {
          const actor = item.actorName?.trim() || 'Pessoa não registrada';
          return <li key={item.id}>
            <p>{item.kind === 'created_in_column'
              ? `Tarefa criada na coluna ${item.toColumnName}`
              : item.kind === 'column_entered' ? `${actor} entrou na coluna ${item.toColumnName}`
              : `${actor} moveu de ${item.fromColumnName ?? 'Coluna não disponível'} para ${item.toColumnName}`}</p>
            {item.kind === 'created_in_column' && <small>{actor}</small>}
            <time dateTime={item.occurredAt}>{new Date(item.occurredAt).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' })}</time>
          </li>;
        })}
      </Entries>}
      {!!query.data?.items.length && query.data.usesCurrentColumnNames && <p>Os nomes exibidos refletem os nomes atuais das colunas.</p>}
    </>}
  </div>;
}
