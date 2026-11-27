import { describe, expect, it } from 'vitest';
import { findActionButtonViolations, type SourceFile, type Violation } from './actionButtonCoverage';

/**
 * Exceções justificadas: `caminho:trecho` (o trecho é procurado no código do botão).
 * Só entra aqui botão que dispara ação no servidor e, por um motivo de produto, não deve
 * mostrar o loading padrão. Escreva o motivo.
 */
const EXCEPTIONS: Array<{ path: string; contains: string; reason: string }> = [
  {
    path: 'src/pages/Kanban.tsx',
    contains: 'setShowTempoModal(true); loadWeekly()',
    reason: '"Meu Tempo" só abre o modal e carrega os dados dele (leitura); o spinner ficaria atrás do modal.',
  },
  {
    path: 'src/pages/Kanban.tsx',
    contains: 'setAdjustDay(day.date)',
    reason: '"Ajustar" só abre o painel de ajuste do dia e carrega os dados dele (leitura).',
  },
];

const sources = import.meta.glob<string>(['../**/*.tsx', '../**/*.ts', '!../**/*.test.ts', '!../**/*.test.tsx'], {
  query: '?raw',
  import: 'default',
  eager: true,
});

function sourceFiles(): SourceFile[] {
  // O glob devolve caminhos relativos a src/components: '../pages/X.tsx' ou './X.tsx'.
  return Object.entries(sources).map(([path, source]) => ({
    path: path.startsWith('./') ? `src/components/${path.slice(2)}` : `src/${path.replace(/^\.\.\//, '')}`,
    source,
  }));
}

const format = (violations: Violation[]) => violations.map(v => `${v.path}:${v.line} (${v.reason}): ${v.snippet}`).join('\n');

describe('cobertura do loading padrão nos botões (HU-0001, CA-6)', () => {
  it('todo botão que chama o servidor usa o ActionButton', () => {
    const violations = findActionButtonViolations(sourceFiles())
      .filter(v => !EXCEPTIONS.some(exception => v.path === exception.path && v.snippet.includes(exception.contains)));
    expect(violations, `Botões de ação sem o loading padrão (use <Botao as={ActionButton} ...>):\n${format(violations)}`).toEqual([]);
  });

  it('as exceções ainda existem no código (lista sem entradas mortas)', () => {
    const all = findActionButtonViolations(sourceFiles());
    const dead = EXCEPTIONS.filter(exception => !all.some(v => v.path === exception.path && v.snippet.includes(exception.contains)));
    expect(dead).toEqual([]);
  });

  it('acusa botões comuns que chamam o servidor (prova de que a verificação pega)', () => {
    const sample = `
      import styled from 'styled-components';
      import { useMutation } from '@tanstack/react-query';
      import { api } from '../services/api';
      import { searchPlatform } from '../features/search/services';
      const Primary = styled.button\`padding: 0;\`;
      const Danger = styled(Primary)\`color: red;\`;
      export function Screen({ id }: { id: string }) {
        const mutation = useMutation({ mutationFn: () => api.deleteItem(id) });
        const query = { refetch: () => undefined };
        async function save() { await api.saveItem(id); }
        const remove = () => mutation.mutate();
        return <>
          <button onClick={() => mutation.mutate()}>Excluir</button>
          <Primary onClick={() => void save()}>Salvar</Primary>
          <Danger onClick={remove}>Remover</Danger>
          <button onClick={() => searchPlatform('x')}>Buscar</button>
          <button onClick={() => query.refetch()}>Tentar novamente</button>
          <form onSubmit={(event) => { event.preventDefault(); mutation.mutate(); }}>
            <Primary type="submit">Enviar</Primary>
          </form>
        </>;
      }`;
    const violations = findActionButtonViolations([{ path: 'src/sample/Screen.tsx', source: sample }]);
    expect(violations.map(v => v.snippet.match(/>([^<>]+)</)?.[1])).toEqual(['Excluir', 'Salvar', 'Remover', 'Buscar', 'Tentar novamente', 'Enviar']);
  });

  it('aceita o ActionButton e ignora botões que não chamam o servidor', () => {
    const sample = `
      import styled from 'styled-components';
      import { ActionButton } from '../components/ActionButton';
      import { Primary } from './styles';
      const Local = styled.button\`padding: 0;\`;
      export function Screen({ mutation }: { mutation: { mutate: () => void; isPending: boolean } }) {
        return <form onSubmit={(event) => { event.preventDefault(); mutation.mutate(); }}>
          <Local as={ActionButton} loading={mutation.isPending} onClick={() => mutation.mutate()}>Salvar</Local>
          <ActionButton onClick={() => mutation.mutate()}>Excluir</ActionButton>
          <Primary as={ActionButton} type="submit" loading={mutation.isPending}>Enviar</Primary>
          <Local type="button" onClick={() => console.log('abrir')}>Abrir</Local>
        </form>;
      }`;
    const styles = `import styled from 'styled-components';\nexport const Primary = styled.button\`padding: 0;\`;`;
    expect(findActionButtonViolations([
      { path: 'src/sample/Screen.tsx', source: sample },
      { path: 'src/sample/styles.ts', source: styles },
    ])).toEqual([]);
  });

  it('reconhece botão estilizado importado de outro arquivo', () => {
    const sample = `
      import { Primary } from './styles';
      export function Screen({ mutation }: { mutation: { mutate: () => void } }) {
        return <Primary onClick={() => mutation.mutate()}>Salvar</Primary>;
      }`;
    const styles = `import styled from 'styled-components';\nexport const Primary = styled.button<{ $x?: boolean }>\`padding: 0;\`;`;
    const violations = findActionButtonViolations([
      { path: 'src/sample/Screen.tsx', source: sample },
      { path: 'src/sample/styles.ts', source: styles },
    ]);
    expect(violations).toHaveLength(1);
    expect(violations[0].line).toBe(4);
  });

  it('reconhece styled(BotaoImportado), inclusive em cadeia entre arquivos', () => {
    const sample = `
      import styled from 'styled-components';
      import { Danger } from './more';
      const Local = styled(Danger)\`margin: 0;\`;
      export function Screen({ mutation }: { mutation: { mutate: () => void } }) {
        return <Local onClick={() => mutation.mutate()}>Excluir</Local>;
      }`;
    const more = `import styled from 'styled-components';\nimport { Primary } from './styles';\nexport const Danger = styled(Primary)\`color: red;\`;`;
    const styles = `import styled from 'styled-components';\nexport const Primary = styled.button\`padding: 0;\`;`;
    const violations = findActionButtonViolations([
      { path: 'src/sample/Screen.tsx', source: sample },
      { path: 'src/sample/more.ts', source: more },
      { path: 'src/sample/styles.ts', source: styles },
    ]);
    expect(violations.map(v => v.line)).toEqual([6]);
  });
});
