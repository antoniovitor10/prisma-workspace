/**
 * Assuntos da central de ajuda, na ordem em que aparecem.
 *
 * Cada assunto é uma pasta em `src/features/help/articles/<slug>/`. Um artigo numa pasta que
 * não está nesta lista faz o teste da ajuda falhar.
 */
export interface HelpSubject {
  slug: string;
  title: string;
  description: string;
}

export const helpSubjects: HelpSubject[] = [
  {
    slug: 'organizacao-e-equipes',
    title: 'Organização, projetos e equipes',
    description: 'Criar a organização e os projetos, convidar pessoas, equipes e permissões.',
  },
  {
    slug: 'quadro-backlog-e-sprints',
    title: 'Quadro, backlog e sprints',
    description: 'Kanban, colunas, backlog, hierarquia de itens e ciclo das sprints.',
  },
  {
    slug: 'integracoes',
    title: 'Integrações',
    description: 'Ativar ou pausar, tokens de agente, humano e sistema, e o servidor MCP.',
  },
  {
    slug: 'repositorios-e-git',
    title: 'Repositórios e Git',
    description: 'GitHub App, webhook, vínculo de branch e PR e o que move o card.',
  },
  {
    slug: 'administracao',
    title: 'Administração da instalação',
    description: 'Instalação, primeiro acesso e configurações do servidor, sem valores de segredo.',
  },
];
