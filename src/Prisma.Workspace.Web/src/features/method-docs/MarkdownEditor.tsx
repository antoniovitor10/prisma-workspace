import { useId, useState, type KeyboardEvent } from 'react';
import styled from 'styled-components';
import { MarkdownView } from './MarkdownView';
import { Alert, Button } from './styles';

/**
 * Editor do documento (D10): textarea monoespaçado com "Escrever" e "Pré-visualizar". Não usa o
 * TipTap, que converte para HTML e de volta e mudaria o texto. O que a pessoa digita é o que grava.
 */
const Wrap = styled.div`display: grid; gap: 10px;`;

const TabList = styled.div`
  display: flex;
  gap: 4px;
  padding: 3px;
  width: fit-content;
  max-width: 100%;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.neutral[50]};
`;

const EditorTab = styled.button<{ $active: boolean }>`
  min-height: 32px;
  padding: 0 12px;
  border-radius: ${({ theme }) => theme.radius.sm};
  background: ${({ theme, $active }) => ($active ? theme.color.surface : 'transparent')};
  color: ${({ theme, $active }) => ($active ? theme.color.text : theme.color.textMutedAccessible)};
  box-shadow: ${({ theme, $active }) => ($active ? theme.shadow.sm : 'none')};
  font-size: 13px;
  font-weight: 800;
  cursor: pointer;
  &:focus-visible { outline: 2px solid ${({ theme }) => theme.color.accentBlueAccessible}; outline-offset: 1px; }
`;

const TextArea = styled.textarea`
  display: block;
  width: 100%;
  min-height: 320px;
  height: 55vh;
  padding: 12px;
  border: 1px solid ${({ theme }) => theme.color.borderStrong};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  font-family: ${({ theme }) => theme.font.mono};
  font-size: 13.5px;
  line-height: 1.55;
  resize: vertical;
  tab-size: 2;
  outline: none;
  &:focus { border-color: ${({ theme }) => theme.color.accentBlue}; box-shadow: 0 0 0 2px color-mix(in srgb, ${({ theme }) => theme.color.accentBlue} 14%, transparent); }
  @media (max-width: 640px) { font-size: 16px; height: 50vh; }
`;

const Preview = styled.div`
  min-height: 320px;
  padding: 12px 14px;
  border: 1px dashed ${({ theme }) => theme.color.borderStrong};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
`;

/** No celular a barra fica fixa embaixo da área rolável do detalhe, visível com o teclado aberto. */
const ActionBar = styled.div`
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
  @media (max-width: 640px) {
    position: sticky;
    bottom: 0;
    z-index: 3;
    margin: 0 -16px;
    padding: 10px 16px calc(10px + env(safe-area-inset-bottom));
    border-top: 1px solid ${({ theme }) => theme.color.border};
    background: ${({ theme }) => theme.color.surface};
    box-shadow: ${({ theme }) => theme.shadow.md};
    > button { flex: 1 1 0; min-height: 44px; }
  }
`;

interface MarkdownEditorProps {
  initialValue: string;
  /** Nome do documento nos rótulos: "PRD" → "Salvar PRD". */
  documentLabel: string;
  saving: boolean;
  error: string | null;
  onSave: (value: string) => void;
  onCancel: () => void;
}

export function MarkdownEditor({ initialValue, documentLabel, saving, error, onSave, onCancel }: MarkdownEditorProps) {
  const [value, setValue] = useState(initialValue);
  const [mode, setMode] = useState<'write' | 'preview'>('write');
  const id = useId();
  const writeId = `${id}-write`;
  const previewId = `${id}-preview`;

  const onTabKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    event.preventDefault();
    const next = mode === 'write' ? 'preview' : 'write';
    setMode(next);
    document.getElementById(`${next === 'write' ? writeId : previewId}-tab`)?.focus();
  };

  return (
    <Wrap>
      <TabList role="tablist" aria-label={`Modo de edição do ${documentLabel}`}>
        <EditorTab type="button" role="tab" id={`${writeId}-tab`} aria-selected={mode === 'write'} aria-controls={writeId}
          tabIndex={mode === 'write' ? 0 : -1} $active={mode === 'write'} onClick={() => setMode('write')} onKeyDown={onTabKeyDown}>
          Escrever
        </EditorTab>
        <EditorTab type="button" role="tab" id={`${previewId}-tab`} aria-selected={mode === 'preview'} aria-controls={previewId}
          tabIndex={mode === 'preview' ? 0 : -1} $active={mode === 'preview'} onClick={() => setMode('preview')} onKeyDown={onTabKeyDown}>
          Pré-visualizar
        </EditorTab>
      </TabList>
      {mode === 'write'
        ? <div role="tabpanel" id={writeId} aria-labelledby={`${writeId}-tab`}>
          <TextArea aria-label={`Texto do ${documentLabel} em markdown`} value={value} spellCheck={false}
            onChange={event => setValue(event.target.value)} />
        </div>
        : <Preview role="tabpanel" id={previewId} aria-labelledby={`${previewId}-tab`}>
          <MarkdownView content={value} aria-label={`Pré-visualização do ${documentLabel}`} />
        </Preview>}
      {error && <Alert>{error}</Alert>}
      <ActionBar>
        <Button type="button" $secondary aria-label="Cancelar edição" disabled={saving} onClick={onCancel}>Cancelar</Button>
        <Button type="button" aria-label={`Salvar ${documentLabel}`} disabled={saving} onClick={() => onSave(value)}>
          {saving ? 'Salvando...' : 'Salvar'}
        </Button>
      </ActionBar>
    </Wrap>
  );
}
