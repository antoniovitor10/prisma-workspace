import { useQuery } from '@tanstack/react-query';
import { PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom';
import styled from 'styled-components';
import { useOrganization } from '../features/organizations/OrganizationState';
import { previewMode } from '../preview';
import { api } from '../services/api';
import { visibleNavEntries } from './navigation';

/**
 * Trilho de navegação recolhível (D87).
 *
 * Recolhido mostra só ícones; expandido mostra os rótulos. O comportamento é o de trilho
 * expansível pedido pelo PO; a identidade visual continua sendo a do Prisma, conforme a
 * SPEC-PRISMA-VISUAL-SYSTEM — nada aqui imita cores, tipografia ou ícones de outro produto.
 *
 * Em telas estreitas o trilho não aparece: o hambúrguer da barra superior já é a navegação
 * de mobile. Dois overlays de navegação na mesma tela seriam nav duplicada, e o trilho
 * comeria a largura útil justamente onde ela é escassa.
 */

const CHAVE_PREFERENCIA = 'prisma_workspace_nav_expandido';
const LARGURA_RECOLHIDO = '56px';
const LARGURA_EXPANDIDO = '224px';

const lerPreferencia = (): boolean => {
  try {
    return localStorage.getItem(CHAVE_PREFERENCIA) === 'true';
  } catch {
    // Janela privada ou armazenamento bloqueado: começa recolhido, sem quebrar a tela.
    return false;
  }
};

const Trilho = styled.nav<{ $expandido: boolean }>`
  position: sticky;
  top: 0;
  z-index: 25;
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 0 0 auto;
  width: ${({ $expandido }) => ($expandido ? LARGURA_EXPANDIDO : LARGURA_RECOLHIDO)};
  height: 100vh;
  padding: 10px 8px;
  border-right: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.surface};
  transition: width .16s ease;

  @media (prefers-reduced-motion: reduce) {
    transition: none;
  }

  /* Abaixo deste ponto quem navega é o hambúrguer da barra superior, que usa o
     mesmo limite. */
  @media (max-width: 768px) {
    display: none;
  }
`;

const Alternar = styled.button<{ $expandido: boolean }>`
  display: flex;
  align-items: center;
  justify-content: ${({ $expandido }) => ($expandido ? 'flex-start' : 'center')};
  gap: 9px;
  min-height: 38px;
  padding: 0 9px;
  margin-bottom: 6px;
  border: 0;
  border-radius: ${({ theme }) => theme.radius.md};
  background: transparent;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  font-weight: 750;
  cursor: pointer;

  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; }
  &:focus-visible { outline: 2px solid ${({ theme }) => theme.color.accentBlue}; outline-offset: 1px; }
`;

const Item = styled(NavLink)<{ $expandido: boolean }>`
  display: flex;
  align-items: center;
  justify-content: ${({ $expandido }) => ($expandido ? 'flex-start' : 'center')};
  gap: 10px;
  min-height: 38px;
  padding: 0 9px;
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13.5px;
  font-weight: 700;
  text-decoration: none;
  white-space: nowrap;

  > svg { flex: 0 0 auto; }

  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; color: ${({ theme }) => theme.color.text}; }
  &:focus-visible { outline: 2px solid ${({ theme }) => theme.color.accentBlue}; outline-offset: 1px; }

  &.active {
    background: ${({ theme }) => `color-mix(in srgb, ${theme.color.brand} 10%, ${theme.color.surface})`};
    color: ${({ theme }) => theme.color.brand};
  }
`;

const Rotulo = styled.span<{ $expandido: boolean }>`
  overflow: hidden;
  /* Recolhido, o rótulo sai da árvore de acessibilidade junto com a largura: o nome
     acessível do item passa a vir do aria-label. */
  display: ${({ $expandido }) => ($expandido ? 'inline' : 'none')};
`;


export function Sidebar() {
  const [expandido, setExpandido] = useState(lerPreferencia);

  useEffect(() => {
    try {
      localStorage.setItem(CHAVE_PREFERENCIA, String(expandido));
    } catch {
      // Preferência é conveniência: não poder guardar não impede usar.
    }
  }, [expandido]);

  const { current } = useOrganization();
  const access = useQuery<{ role: number; allowedPermissions: number[] }>({
    queryKey: ['organization', 'access'],
    queryFn: () => api.getOrganizationAccess(),
    enabled: !previewMode,
    initialData: previewMode ? { role: 1, allowedPermissions: [1, 10, 12, 13, 14] } : undefined,
  });

  const entries = visibleNavEntries({
    role: access.data?.role ?? current.role,
    allowed: access.data?.allowedPermissions ?? [],
  });

  return (
    <Trilho $expandido={expandido} aria-label="Navegação lateral">
      <Alternar
        type="button"
        $expandido={expandido}
        aria-expanded={expandido}
        aria-label={expandido ? 'Recolher navegação' : 'Expandir navegação'}
        onClick={() => setExpandido((atual) => !atual)}
      >
        {expandido ? <PanelLeftClose size={16} /> : <PanelLeftOpen size={16} />}
        <Rotulo $expandido={expandido}>Recolher</Rotulo>
      </Alternar>

      {entries.map(({ to, label, icon: Icone, end }) => (
        <Item key={to} to={to} end={end} $expandido={expandido} aria-label={label} title={label}>
          <Icone size={16} />
          <Rotulo $expandido={expandido}>{label}</Rotulo>
        </Item>
      ))}
    </Trilho>
  );
}
