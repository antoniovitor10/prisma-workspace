import { FolderGit2, GitBranch, GitPullRequest } from 'lucide-react';
import styled, { css, type DefaultTheme } from 'styled-components';
import { gitBranchStateLabel, gitPrStateLabel, type GitPrState, type WorkItemGitSummary } from '../method-git/gitLabels';

const List = styled.ul`
  display:flex;flex-wrap:wrap;gap:5px;margin:6px 0 0;padding:0;list-style:none;min-width:0;
`;
const prTone = (theme: DefaultTheme, state: GitPrState | null) => {
  switch (state) {
    case 'approved': return theme.color.success;
    case 'changes_requested': return theme.color.warning;
    case 'merged': return theme.color.accentViolet;
    case 'closed': return theme.color.danger;
    case 'draft': return theme.color.neutral[500];
    default: return theme.color.info;
  }
};
const Badge = styled.li<{ $tone?: string; $dashed?: boolean }>`
  display:inline-flex;align-items:center;gap:4px;min-width:0;max-width:100%;min-height:22px;padding:1px 7px;
  border:1px ${({ $dashed }) => ($dashed ? 'dashed' : 'solid')} ${({ theme, $tone }) => `color-mix(in srgb, ${$tone ?? theme.color.neutral[500]} 45%, ${theme.color.border})`};
  border-radius:${({ theme }) => theme.radius.pill};
  background:${({ theme, $tone }) => `color-mix(in srgb, ${$tone ?? theme.color.neutral[500]} 12%, ${theme.color.surface})`};
  color:${({ theme }) => theme.color.text};font-size:12px;font-weight:700;line-height:1.3;
  svg{flex:0 0 auto;color:${({ theme, $tone }) => $tone ?? theme.color.textMuted};}
  ${({ $dashed }) => $dashed && css`font-style:italic;`}
`;
const PrBadge = styled(Badge)<{ $state: GitPrState | null }>`
  border-color:${({ theme, $state }) => `color-mix(in srgb, ${prTone(theme, $state)} 45%, ${theme.color.border})`};
  background:${({ theme, $state }) => `color-mix(in srgb, ${prTone(theme, $state)} 12%, ${theme.color.surface})`};
  svg{color:${({ theme, $state }) => prTone(theme, $state)};}
`;
const Text = styled.span`min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;`;
const Branch = styled(Text)`max-width:150px;font-family:${({ theme }) => theme.font.mono};font-weight:600;`;
const Hidden = styled.span`position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap;`;
const Note = styled.span`flex:0 0 auto;color:${({ theme }) => theme.color.textMuted};font-weight:600;`;

/** Selos de repositório, branch e PR do cartão (HU-0002, CA-1 a CA-3); não dependem só de cor. */
export function GitCardBadges({ git }: { git: WorkItemGitSummary }) {
  const local = git.branchState === 'local';
  return (
    <List aria-label="Git">
      <Badge title={git.repoFullName}>
        <FolderGit2 size={12} aria-hidden="true" />
        <Text><Hidden>Repositório: </Hidden>{git.repoName}</Text>
      </Badge>
      {git.branch && (
        <Badge $dashed={local} title={local ? `${git.branch} (${gitBranchStateLabel('local')})` : git.branch}>
          <GitBranch size={12} aria-hidden="true" />
          <Branch><Hidden>Branch: </Hidden>{git.branch}</Branch>
          {local && <Note>{gitBranchStateLabel('local')}</Note>}
        </Badge>
      )}
      {git.prNumber != null && (
        <PrBadge $state={git.prState} title={`PR #${git.prNumber}: ${gitPrStateLabel(git.prState)}`}>
          <GitPullRequest size={12} aria-hidden="true" />
          <Text><Hidden>PR </Hidden>#{git.prNumber} · {gitPrStateLabel(git.prState)}</Text>
        </PrBadge>
      )}
    </List>
  );
}

