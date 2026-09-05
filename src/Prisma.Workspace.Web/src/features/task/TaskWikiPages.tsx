import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import styled from 'styled-components';
import { BookText, ChevronRight } from 'lucide-react';
import { api } from '../../services/api';

interface WikiPageLink { pageId: string; title: string; }

const Wrap = styled.section`
  margin-top: 24px;
  > h2 { display: flex; align-items: center; gap: 7px; margin-bottom: 10px; color: ${({ theme }) => theme.color.neutral[700]}; font-size: 13.5px; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
`;
const Row = styled.button`
  display: flex; align-items: center; gap: 8px; width: 100%; text-align: left;
  padding: 9px 11px; margin-bottom: 5px;
  border: 1px solid ${({ theme }) => theme.color.border}; border-radius: 9px;
  background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
  font-size: 14px; font-weight: 600;
  &:hover { border-color: ${({ theme }) => theme.color.accentBlue}; }
  span { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  svg:last-child { color: ${({ theme }) => theme.color.textMuted}; }
`;
const Empty = styled.p`color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px;`;

export function TaskWikiPages({ projectId, workItemId, onNavigate }: { projectId: string; workItemId: string; onNavigate: () => void }) {
  const navigate = useNavigate();
  const query = useQuery<WikiPageLink[]>({
    queryKey: ['task-wiki-pages', projectId, workItemId],
    queryFn: () => api.getTaskWikiPages(projectId, workItemId),
  });
  const pages = query.data ?? [];
  const open = (pageId: string) => { onNavigate(); navigate(`/projects/${projectId}/wiki/${pageId}`); };
  return (
    <Wrap>
      <h2><BookText size={14} />Páginas do wiki</h2>
      {pages.length === 0
        ? <Empty>Nenhuma página do wiki vinculada a esta tarefa.</Empty>
        : pages.map((page) => (
          <Row key={page.pageId} onClick={() => open(page.pageId)}>
            <BookText size={13} /><span>{page.title}</span><ChevronRight size={14} />
          </Row>
        ))}
    </Wrap>
  );
}
