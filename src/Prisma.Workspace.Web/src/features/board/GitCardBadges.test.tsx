import { cleanup, render, screen } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it } from 'vitest';
import { theme } from '../../styles/theme';
import { gitPrState, gitPrStateLabel, type WorkItemGitSummary } from '../method-git/gitLabels';
import { GitCardBadges } from './GitCardBadges';

const base: WorkItemGitSummary = {
  repoName: 'canais-mobile', repoFullName: 'detranse/canais-mobile',
  branch: 'task/T-0005-git-no-card-com-um-nome-de-branch-bem-comprido', branchState: 'remote',
  prNumber: 42, prState: 'approved', prUrl: 'https://github.com/detranse/canais-mobile/pull/42',
};
afterEach(cleanup);
const renderBadges = (git: WorkItemGitSummary) => render(<ThemeProvider theme={theme}><GitCardBadges git={git} /></ThemeProvider>);

describe('selos de Git no cartão (HU-0002, T-0005)', () => {
  it('CA-1: branch longa fica cortada, com o nome inteiro no tooltip', () => {
    renderBadges(base);
    const badge = screen.getByTitle(base.branch!);
    expect(badge).toHaveTextContent(`Branch: ${base.branch}`);
    const text = screen.getByText(base.branch!);
    // Corte visual por CSS (reticências) sem perder o nome no texto acessível.
    expect(getComputedStyle(text).textOverflow).toBe('ellipsis');
    expect(getComputedStyle(text).whiteSpace).toBe('nowrap');
    expect(screen.queryByText('não enviada')).not.toBeInTheDocument();
  });

  it('CA-1: branch local aparece como "não enviada"', () => {
    renderBadges({ ...base, branch: 'feat/login', branchState: 'local', prNumber: null, prState: null, prUrl: null });
    expect(screen.getByTitle('feat/login (não enviada)')).toHaveTextContent('Branch: feat/loginnão enviada');
    expect(screen.getByText('não enviada')).toBeInTheDocument();
    expect(screen.queryByTitle(/^PR #/)).not.toBeInTheDocument();
  });

  it('CA-2: selo do PR com número e estado', () => {
    renderBadges(base);
    expect(screen.getByTitle('PR #42: aprovado')).toHaveTextContent('PR #42 · aprovado');
  });

  it('CA-3: selo do repositório com o nome curto e o nome completo no tooltip', () => {
    renderBadges(base);
    const repo = screen.getByTitle('detranse/canais-mobile');
    expect(repo).toHaveTextContent('Repositório: canais-mobile');
    expect(repo).not.toHaveTextContent('detranse/');
    expect(screen.getByRole('list', { name: 'Git' })).toBeInTheDocument();
  });
});

describe('estado do PR (CA-2)', () => {
  const cases: Array<[Parameters<typeof gitPrState>[0], string]> = [
    [{ state: 'open', isDraft: true, merged: false, approvalsCount: 2, changesRequested: true }, 'rascunho'],
    [{ state: 'open', isDraft: false, merged: false, approvalsCount: 1, changesRequested: false }, 'aprovado'],
    [{ state: 'open', isDraft: false, merged: false, approvalsCount: 1, changesRequested: true }, 'mudanças pedidas'],
    [{ state: 'open', isDraft: false, merged: false, approvalsCount: 0, changesRequested: false }, 'aberto'],
    [{ state: 'closed', isDraft: false, merged: true, approvalsCount: 1, changesRequested: false }, 'mergeado'],
    [{ state: 'closed', isDraft: true, merged: false, approvalsCount: 0, changesRequested: true }, 'fechado'],
  ];
  it.each(cases)('%o vira "%s"', (input, label) => {
    expect(gitPrStateLabel(gitPrState(input))).toBe(label);
  });
});
