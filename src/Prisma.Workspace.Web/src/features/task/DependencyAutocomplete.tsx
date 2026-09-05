import { useQuery } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import { useEffect, useId, useMemo, useState } from 'react';
import styled from 'styled-components';
import { api } from '../../services/api';

export interface DependencyWorkItemOption {
  id: string;
  projectId: string;
  projectKey: string;
  number: number;
  title: string;
  status: string;
}

interface DependencyAutocompleteProps {
  projectId: string;
  excludeWorkItemId?: string;
  disabled?: boolean;
  onSelect: (item: DependencyWorkItemOption | null) => void;
}

const Root = styled.div`
  position: relative;
  flex: 1;
  min-width: 220px;
`;

const InputBox = styled.label`
  display: flex;
  min-height: 36px;
  align-items: center;
  gap: 7px;
  padding: 0 9px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.textMuted};
  input {
    min-width: 0;
    flex: 1;
    border: 0;
    outline: 0;
    background: transparent;
    color: ${({ theme }) => theme.color.text};
    font-size: 13px;
  }
`;

const Results = styled.ul`
  position: absolute;
  z-index: 30;
  top: calc(100% + 5px);
  right: 0;
  left: 0;
  max-height: 260px;
  overflow-y: auto;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.md};
  list-style: none;
`;

const ResultButton = styled.button`
  display: grid;
  width: 100%;
  gap: 2px;
  padding: 9px 10px;
  border-bottom: 1px solid ${({ theme }) => theme.color.neutral[100]};
  text-align: left;
  strong { color: ${({ theme }) => theme.color.text}; font-size: 13px; }
  small { color: ${({ theme }) => theme.color.textMuted}; font-size: 11.5px; }
  &:hover, &:focus-visible { background: ${({ theme }) => theme.color.neutral[100]}; }
`;

const Message = styled.div`
  padding: 10px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
`;

export function DependencyAutocomplete({
  projectId,
  excludeWorkItemId,
  disabled,
  onSelect,
}: DependencyAutocompleteProps) {
  const listId = useId();
  const [input, setInput] = useState('');
  const [debounced, setDebounced] = useState('');
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const normalized = input.trim();
    if (normalized.length < 2) {
      setDebounced('');
      return;
    }
    const timer = window.setTimeout(() => setDebounced(normalized), 250);
    return () => window.clearTimeout(timer);
  }, [input]);

  const query = useQuery<DependencyWorkItemOption[]>({
    queryKey: ['dependency-search', projectId, debounced],
    queryFn: () => api.searchWorkItems(projectId, debounced),
    enabled: debounced.length >= 2 && !disabled,
    retry: false,
  });
  const options = useMemo(
    () => (query.data ?? []).filter(item => item.id !== excludeWorkItemId),
    [excludeWorkItemId, query.data],
  );
  const showResults = open && input.trim().length >= 2;

  return (
    <Root>
      <InputBox>
        <Search size={13} />
        <input
          type="search"
          role="combobox"
          aria-label="Buscar tarefa relacionada"
          aria-autocomplete="list"
          aria-controls={listId}
          aria-expanded={showResults}
          disabled={disabled}
          value={input}
          placeholder="Código ou título, ex.: DET-42"
          onFocus={() => setOpen(true)}
          onChange={event => {
            setInput(event.target.value);
            setOpen(true);
            onSelect(null);
          }}
        />
      </InputBox>
      {showResults && (
        <Results id={listId} role="listbox" aria-label="Tarefas encontradas">
          {query.isFetching && <Message>Buscando tarefas...</Message>}
          {query.error && <Message role="alert">{(query.error as Error).message}</Message>}
          {!query.isFetching && !query.error && debounced && options.length === 0 && (
            <Message>Nenhuma tarefa visível encontrada.</Message>
          )}
          {options.map(option => (
            <li role="option" aria-selected="false" key={option.id}>
              <ResultButton
                type="button"
                onClick={() => {
                  setInput(`${option.projectKey}-${option.number} · ${option.title}`);
                  setOpen(false);
                  onSelect(option);
                }}
              >
                <strong>{option.projectKey}-{option.number} · {option.title}</strong>
                <small>{option.status}</small>
              </ResultButton>
            </li>
          ))}
        </Results>
      )}
    </Root>
  );
}
