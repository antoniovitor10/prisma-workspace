import {
  BarChart3, ClipboardList, FolderKanban, House, Inbox, Users,
  type LucideIcon,
} from 'lucide-react';

/**
 * Itens da navegação principal e seus portões de permissão.
 *
 * Fonte única para as duas superfícies (D87): a barra superior e o trilho lateral
 * recolhível. Duplicar esta lista faria as duas divergirem — alguém acrescentaria um item
 * em uma e esqueceria a outra, e o portão de permissão ficaria diferente em cada lugar.
 */
export interface NavAccess {
  /** Papel na organização. 8 é solicitante externo; 9 e 10, perfis de leitura. */
  role: number;
  /** Permissões concedidas. 1 = visualizar, 10 = visualizar relatório. */
  allowed: number[];
}

export interface NavEntry {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Casa a rota apenas exata, para não marcar "/projects" dentro de "/projects/:id". */
  end?: boolean;
  isVisible: (access: NavAccess) => boolean;
}

const podeUsarWorkspace = ({ role }: NavAccess) => role !== 8;

export const navEntries: NavEntry[] = [
  { to: '/home', label: 'Início', icon: House, end: true, isVisible: podeUsarWorkspace },
  { to: '/me/tasks', label: 'Meu trabalho', icon: ClipboardList, isVisible: podeUsarWorkspace },
  { to: '/projects', label: 'Projetos', icon: FolderKanban, end: true, isVisible: podeUsarWorkspace },
  { to: '/requests', label: 'Solicitações', icon: Inbox, isVisible: () => true },
  {
    to: '/reports', label: 'Relatórios', icon: BarChart3,
    isVisible: ({ role, allowed }) => allowed.includes(10) || role === 9 || role === 10,
  },
  {
    to: '/teams', label: 'Equipes', icon: Users,
    isVisible: ({ role, allowed }) => allowed.includes(1) && role !== 9,
  },
];

export const visibleNavEntries = (access: NavAccess): NavEntry[] =>
  navEntries.filter((entry) => entry.isVisible(access));
