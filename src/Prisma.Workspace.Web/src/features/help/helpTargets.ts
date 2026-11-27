/**
 * Alvos dos botões "?" que ficam dentro das seções das telas (`<HelpButton target="..." />`).
 *
 * Formato: `assunto/artigo#ancora`. O teste `helpIndex.test.ts` abre cada alvo e falha se o
 * artigo ou a âncora não existir (CA-8). Ao renomear uma seção de artigo, atualize aqui.
 */
export const helpTargets = {
  organizationInvite: 'organizacao-e-equipes/organizacao#convidar-pessoas',
  projectPeople: 'organizacao-e-equipes/projetos#dar-acesso-ao-projeto',
  integrations: 'integracoes/integracoes#ativar-ou-pausar',
  integrationAccess: 'integracoes/tokens#emitir-um-token',
  integrationRepositories: 'repositorios-e-git/github-app#conectar-o-repositorio',
  integrationPullRequests: 'repositorios-e-git/branch-e-pr#prs-nao-vinculados',
  integrationResume: 'integracoes/integracoes#retomadas',
} as const satisfies Record<string, string>;

export type HelpTargetKey = keyof typeof helpTargets;
