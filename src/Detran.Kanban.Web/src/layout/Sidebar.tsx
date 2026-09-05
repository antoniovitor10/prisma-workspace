import {
  BarChart3,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  FolderKanban,
  Inbox,
  Settings,
  Users,
} from 'lucide-react';
import { NavLink } from 'react-router-dom';
import styled from 'styled-components';
import { useQuery } from '@tanstack/react-query';
import { BrandMark } from '../components/BrandMark';
import { useOrganization } from '../features/organizations/OrganizationState';
import { previewMode } from '../preview';
import { api } from '../services/api';

const Aside = styled.aside<{ $collapsed: boolean }>`
  grid-area: sidebar;
  z-index: 30;
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 14px;
  overflow: hidden;
  padding: 12px 10px;
  background: ${({ theme }) => theme.color.neutral[900]};
  color: ${({ theme }) => theme.color.neutral[300]};

  @media (max-width: 900px) { padding-inline: 8px; }
`;

const Brand = styled.div<{ $collapsed: boolean }>`
  display: flex;
  height: 42px;
  min-width: 0;
  align-items: center;
  gap: 10px;
  padding: 0 8px;
  color: ${({ theme }) => theme.color.onBrand};
  font-family: ${({ theme }) => theme.font.display};

  > svg {
    flex: 0 0 auto;
  }

  > span {
    display: ${({ $collapsed }) => $collapsed ? 'none' : 'block'};
    min-width: 0;
    overflow: hidden;
    font-size: 14px;
    font-weight: 800;
    line-height: 1.1;
    white-space: nowrap;
    small { display: block; margin-top: 3px; color: ${({ theme }) => theme.color.neutral[400]}; font-size: 11px; letter-spacing: .05em; text-transform: uppercase; }
  }

  @media (max-width: 900px) { > span { display: none; } }
`;

const Group = styled.div`
  display: grid;
  gap: 3px;
`;

const Label = styled.div<{ $collapsed: boolean }>`
  display: ${({ $collapsed }) => $collapsed ? 'none' : 'block'};
  padding: 8px 10px 4px;
  color: ${({ theme }) => theme.color.neutral[500]};
  font-size: 12px;
  font-weight: 800;
  letter-spacing: .09em;
  text-transform: uppercase;
  @media (max-width: 900px) { display: none; }
`;

const Item = styled(NavLink)<{ $collapsed: boolean }>`
  position: relative;
  display: flex;
  min-height: 38px;
  align-items: center;
  justify-content: ${({ $collapsed }) => $collapsed ? 'center' : 'flex-start'};
  gap: 10px;
  padding: 0 ${({ $collapsed }) => $collapsed ? '0' : '10px'};
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.neutral[400]};
  font-size: 14px;
  font-weight: 650;
  white-space: nowrap;

  svg { flex: 0 0 auto; }
  span { display: ${({ $collapsed }) => $collapsed ? 'none' : 'inline'}; }
  &:hover { background: ${({ theme }) => `color-mix(in srgb, ${theme.color.surface} 7%, transparent)`}; color: ${({ theme }) => theme.color.onBrand}; }
  &.active {
    background: ${({ theme }) => `color-mix(in srgb, ${theme.color.accentBlue} 22%, transparent)`};
    color: ${({ theme }) => theme.color.onBrand};
    box-shadow: inset 3px 0 ${({ theme }) => theme.color.accentBlue};
  }

  @media (max-width: 900px) { justify-content: center; padding: 0; span { display: none; } }
`;

const Bottom = styled(Group)`margin-top: auto;`;

const Collapse = styled.button<{ $collapsed: boolean }>`
  display: flex;
  min-height: 36px;
  align-items: center;
  justify-content: ${({ $collapsed }) => $collapsed ? 'center' : 'flex-start'};
  gap: 9px;
  padding: 0 ${({ $collapsed }) => $collapsed ? '0' : '10px'};
  border-top: 1px solid ${({ theme }) => theme.color.neutral[800]};
  color: ${({ theme }) => theme.color.neutral[500]};
  font-size: 13px;
  font-weight: 700;

  span { display: ${({ $collapsed }) => $collapsed ? 'none' : 'inline'}; }
  &:hover { color: ${({ theme }) => theme.color.onBrand}; }
  @media (max-width: 900px) { display: none; }
`;

interface SidebarProps {
  collapsed: boolean;
  onToggle: () => void;
}

export function Sidebar({ collapsed, onToggle }: SidebarProps) {
  const { current } = useOrganization();
  const access = useQuery<{ role:number; allowedPermissions:number[] }>({
    queryKey: ['organization', 'access'],
    queryFn: () => api.getOrganizationAccess(),
    enabled: !previewMode,
    initialData: previewMode ? { role: 1, allowedPermissions: [1, 10, 12, 13, 14] } : undefined,
  });
  const role = access.data?.role ?? current.role;
  const allowed = access.data?.allowedPermissions ?? [];
  const canUseWorkspace = role !== 8;
  const canSeeReports = allowed.includes(10) || role === 9 || role === 10;
  const canSeeTeams = allowed.includes(1) && role !== 9;
  const canConfigure = allowed.some(permission => [12, 13, 14].includes(permission));

  return (
    <Aside $collapsed={collapsed}>
      <Brand $collapsed={collapsed}>
        <BrandMark size={26} />
        <span>Prisma WorkSpace<small>Uma visão completa</small></span>
      </Brand>
      <Group>
        <Label $collapsed={collapsed}>Principal</Label>
        {canUseWorkspace && <Item $collapsed={collapsed} to="/me/tasks" title="Meu trabalho"><ClipboardList size={17} /><span>Meu trabalho</span></Item>}
        {canUseWorkspace && <Item $collapsed={collapsed} to="/projects" end title="Projetos"><FolderKanban size={17} /><span>Projetos</span></Item>}
        <Item $collapsed={collapsed} to="/requests" title="Solicitações"><Inbox size={17} /><span>Solicitações</span></Item>
      </Group>
      <Group>
        <Label $collapsed={collapsed}>Gestão</Label>
        {canSeeReports && <Item $collapsed={collapsed} to="/reports" title="Relatórios"><BarChart3 size={17} /><span>Relatórios</span></Item>}
        {canSeeTeams && <Item $collapsed={collapsed} to="/teams" title="Equipes"><Users size={17} /><span>Equipes</span></Item>}
      </Group>
      <Bottom>
        {canConfigure && <Item $collapsed={collapsed} to="/settings" title="Configurações"><Settings size={17} /><span>Configurações</span></Item>}
        <Collapse $collapsed={collapsed} onClick={onToggle} aria-label={collapsed ? 'Expandir menu' : 'Recolher menu'}>
          {collapsed ? <ChevronRight size={16} /> : <ChevronLeft size={16} />}<span>Recolher menu</span>
        </Collapse>
      </Bottom>
    </Aside>
  );
}
