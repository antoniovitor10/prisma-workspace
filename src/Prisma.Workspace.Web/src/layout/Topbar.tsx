import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Building2,
  ChevronDown,
  Clock,
  LogOut,
  Menu,
  Moon,
  Plus,
  Search,
  Settings,
  Square,
  Sun,
  X,
} from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import styled from 'styled-components';
import { BrandMark } from '../components/BrandMark';
import { GlobalSearchDialog, QuickCreateDialog } from '../components/GlobalActions';
import { NotificationCenter } from '../features/notifications/NotificationCenter';
import { useOrganization } from '../features/organizations/OrganizationState';
import { visibleNavEntries } from './navigation';
import { previewMode } from '../preview';
import { api } from '../services/api';
import { useThemeMode } from '../styles/ThemeMode';

/* ─── layout ─── */

const Bar = styled.header`
  position: relative;
  z-index: 20;
  display: flex;
  height: 64px;
  align-items: center;
  gap: 6px;
  padding: 0 clamp(12px, 2vw, 28px);
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  background: color-mix(in srgb, ${({ theme }) => theme.color.surface} 92%, transparent);
  backdrop-filter: blur(18px);

  &::before {
    content: '';
    position: absolute;
    inset: 0 0 auto;
    height: 2px;
    background: ${({ theme }) => theme.color.gradient};
  }

  @media (max-width: 640px) { padding: 0 10px; }
`;

const BrandLink = styled(Link)`
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 0 0 auto;
  margin-right: 12px;
  color: ${({ theme }) => theme.color.text};
  font-family: ${({ theme }) => theme.font.display};
  font-size: 14px;
  font-weight: 800;
  white-space: nowrap;
  text-decoration: none;

  > .label {
    @media (max-width: 900px) { display: none; }
  }

  > .label em {
    font-style: normal;
    font-weight: 500;
    color: ${({ theme }) => theme.color.textMuted};
  }
`;

const ThemeButton = styled.button`
  display: grid;
  width: 34px;
  height: 34px;
  place-items: center;
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.textMuted};
  border: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.surface};

  &:hover {
    color: ${({ theme }) => theme.color.text};
    border-color: ${({ theme }) => theme.color.accentBlue};
  }
`;

/* ─── nav global (desktop) ─── */



const Spacer = styled.div`flex: 1;`;

/* ─── utilitários ─── */

const SearchButton = styled.button`
  display: flex;
  width: min(360px, 36vw);
  min-width: 140px;
  align-items: center;
  gap: 8px;
  min-height: 38px;
  padding: 0 10px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surfaceSubtle};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  text-align: left;

  span { min-width: 0; flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  kbd {
    padding: 2px 5px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.sm};
    background: ${({ theme }) => theme.color.surface};
    font-size: 11px;
    font-weight: 800;
  }

  &:hover { border-color: ${({ theme }) => theme.color.borderStrong}; box-shadow: ${({ theme }) => theme.shadow.sm}; }
  @media (max-width: 900px) { width: 34px; min-width: 34px; span, kbd { display: none; } }
`;

const OrganizationPicker = styled.label`
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 6px;
  color: ${({ theme }) => theme.color.textMuted};

  select {
    width: min(190px, 16vw);
    min-height: 32px;
    padding: 0 26px 0 8px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-size: 13px;
    font-weight: 750;
  }

  @media (max-width: 860px) { svg { display: none; } select { width: 110px; } }

  @media (max-width: 600px) {
    position: relative;
    display: grid;
    width: 34px;
    height: 34px;
    flex: 0 0 34px;
    place-items: center;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};

    svg {
      display: block;
      pointer-events: none;
    }

    select {
      position: absolute;
      inset: 0;
      width: 34px;
      min-height: 32px;
      cursor: pointer;
      opacity: 0;
    }

    &:focus-within {
      border-color: ${({ theme }) => theme.color.accentBlue};
      box-shadow: 0 0 0 3px color-mix(in srgb, ${({ theme }) => theme.color.accentBlue} 18%, transparent);
    }
  }
`;

const Primary = styled.button`
  display: inline-flex;
  min-height: 38px;
  align-items: center;
  gap: 6px;
  padding: 0 11px;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.gradient};
  color: ${({ theme }) => theme.color.onBrand};
  font-size: 13px;
  font-weight: 800;
  white-space: nowrap;

  box-shadow: 0 6px 16px rgba(79,70,229,.2);
  &:hover { transform: translateY(-1px); box-shadow: 0 9px 20px rgba(219,39,119,.24); }
  @media (max-width: 480px) { padding: 0 9px; span { display: none; } }
`;


const ActiveTimer = styled.button`
  display: flex;
  max-width: 220px;
  min-height: 32px;
  align-items: center;
  gap: 6px;
  padding: 0 9px;
  border: 1px solid ${({ theme }) => theme.color.danger};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) =>
    `color-mix(in srgb, ${theme.color.danger} 7%, ${theme.color.surface})`};
  color: ${({ theme }) => theme.color.danger};
  font-size: 13px;
  font-weight: 800;

  span { max-width: 90px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  @media (max-width: 820px) { span { display: none; } }
  @media (max-width: 520px) { display: none; }
`;

/* ─── mobile menu button ─── */

const HamburgerButton = styled.button`
  display: none;
  width: 34px;
  height: 34px;
  place-items: center;
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.textMuted};
  flex: 0 0 auto;

  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; color: ${({ theme }) => theme.color.text}; }

  @media (max-width: 768px) { display: grid; }
`;

/* ─── mobile menu overlay ─── */

const MobileOverlay = styled.div`
  position: fixed;
  inset: 64px 0 0 0;
  z-index: 29;
  background: rgba(15, 23, 42, 0.25);
`;

const MobileMenu = styled.div`
  position: absolute;
  inset: 0 0 auto 0;
  padding: 12px 16px 20px;
  background: ${({ theme }) => theme.color.surface};
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  box-shadow: ${({ theme }) => theme.shadow.lg};
  display: flex;
  flex-direction: column;
  gap: 2px;
`;

const MobileNavItem = styled(NavLink)`
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 42px;
  padding: 0 10px;
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.text};
  font-size: 14px;
  font-weight: 700;
  text-decoration: none;

  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; }
  &.active {
    background: ${({ theme }) =>
      `color-mix(in srgb, ${theme.color.brand} 10%, transparent)`};
    color: ${({ theme }) => theme.color.brand};
  }
`;

/* ─── account dropdown ─── */

const AccountWrap = styled.div`
  position: relative;
`;

const Avatar = styled.button`
  display: grid;
  width: 32px;
  height: 32px;
  place-items: center;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.gradient};
  color: ${({ theme }) => theme.color.onBrand};
  font-size: 12px;
  font-weight: 900;
  cursor: pointer;
  flex: 0 0 auto;
`;

const AccountDropdown = styled.div`
  position: absolute;
  right: 0;
  top: calc(100% + 6px);
  z-index: 50;
  min-width: 180px;
  padding: 6px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.lg};
`;

const DropItem = styled.button`
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  min-height: 36px;
  padding: 0 10px;
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.text};
  font-size: 13.5px;
  font-weight: 700;
  text-align: left;
  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; }
`;

const DropDivider = styled.div`
  height: 1px;
  margin: 4px 0;
  background: ${({ theme }) => theme.color.border};
`;

/* ─── toast ─── */

const Toast = styled.div`
  position: fixed;
  right: 18px;
  bottom: 18px;
  z-index: 80;
  max-width: min(420px, calc(100vw - 36px));
  padding: 12px 15px;
  border-left: 4px solid ${({ theme }) => theme.color.success};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  box-shadow: ${({ theme }) => theme.shadow.lg};
  font-size: 13.5px;
  font-weight: 700;
`;

/* ─── types ─── */

interface ActiveTimerDto {
  workItemId: string;
  workItemTitle?: string;
  startedAt: string;
}

const formatTime = (seconds: number) => {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return [h, m, s].map((v) => String(v).padStart(2, '0')).join(':');
};

/* ─── component ─── */

export function Topbar() {
  const location = useLocation();
  const navigate = useNavigate();
  const { organizations, current, switchOrganization } = useOrganization();
  const queryClient = useQueryClient();
  const { mode, toggleMode } = useThemeMode();

  const [searchOpen, setSearchOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const [toast, setToast] = useState('');
  const [menuOpen, setMenuOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);

  const menuRef = useRef<HTMLDivElement>(null);
  const accountRef = useRef<HTMLDivElement>(null);
  const hamburgerRef = useRef<HTMLButtonElement>(null);
  const avatarRef = useRef<HTMLButtonElement>(null);
  const firstMenuLinkRef = useRef<HTMLAnchorElement>(null);

  /* permissions (same logic as former Sidebar) */
  const access = useQuery<{ role: number; allowedPermissions: number[] }>({
    queryKey: ['organization', 'access'],
    queryFn: () => api.getOrganizationAccess(),
    enabled: !previewMode,
    initialData: previewMode
      ? { role: 1, allowedPermissions: [1, 10, 12, 13, 14] }
      : undefined,
  });
  const role = access.data?.role ?? current.role;
  const allowed = access.data?.allowedPermissions ?? [];
  const canUseWorkspace = role !== 8;
  const canConfigure = allowed.some((p) => [12, 13, 14].includes(p));
  // Mesma fonte do trilho lateral (D87): os dois nunca divergem.
  const navItems = visibleNavEntries({ role, allowed });

  const currentProjectId = location.pathname.match(/^\/projects\/([^/]+)/)?.[1];

  /* timer */
  const { data: timer = null } = useQuery<ActiveTimerDto | null>({
    queryKey: ['active-timer'],
    queryFn: () => api.getMyActiveTimer(),
    retry: false,
    enabled: !previewMode,
    refetchInterval: 20_000,
  });
  const stopTimer = useMutation({
    mutationFn: (workItemId: string) => api.stopTimer(workItemId),
    onSuccess: () => queryClient.setQueryData(['active-timer'], null),
  });

  /* Ctrl/Cmd+K → search */
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setSearchOpen(true);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []);

  /* timer-change event */
  useEffect(() => {
    const refresh = () =>
      queryClient.invalidateQueries({ queryKey: ['active-timer'] });
    window.addEventListener('timer-change', refresh);
    return () => window.removeEventListener('timer-change', refresh);
  }, [queryClient]);

  /* timer tick */
  useEffect(() => {
    if (!timer) { setElapsed(0); return; }
    const tick = () =>
      setElapsed(
        Math.max(0, Math.floor((Date.now() - new Date(timer.startedAt).getTime()) / 1000)),
      );
    tick();
    const id = window.setInterval(tick, 1000);
    return () => window.clearInterval(id);
  }, [timer]);

  /* toast auto-hide */
  useEffect(() => {
    if (!toast) return;
    const id = window.setTimeout(() => setToast(''), 3500);
    return () => window.clearTimeout(id);
  }, [toast]);

  /* close mobile menu on route change */
  useEffect(() => { setMenuOpen(false); }, [location.pathname]);

  /* close mobile menu on Escape */
  useEffect(() => {
    if (!menuOpen) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setMenuOpen(false);
        hamburgerRef.current?.focus();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [menuOpen]);

  /* focus first link when mobile menu opens */
  useEffect(() => {
    if (menuOpen) {
      requestAnimationFrame(() => firstMenuLinkRef.current?.focus());
    }
  }, [menuOpen]);

  /* close account dropdown on Escape / outside click */
  useEffect(() => {
    if (!accountOpen) return;
    const handler = (e: KeyboardEvent | MouseEvent) => {
      if (e instanceof KeyboardEvent && e.key === 'Escape') {
        setAccountOpen(false);
        avatarRef.current?.focus();
        return;
      }
      if (e instanceof MouseEvent && accountRef.current && !accountRef.current.contains(e.target as Node)) {
        setAccountOpen(false);
      }
    };
    window.addEventListener('keydown', handler);
    window.addEventListener('mousedown', handler);
    return () => {
      window.removeEventListener('keydown', handler);
      window.removeEventListener('mousedown', handler);
    };
  }, [accountOpen]);

  /* close mobile overlay on outside click (on backdrop, not menu) */
  const handleOverlayClick = useCallback((e: React.MouseEvent<HTMLDivElement>) => {
    if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
      setMenuOpen(false);
    }
  }, []);

  const logout = () => { setAccountOpen(false); void api.logout(); };

  return (
    <>
      <Bar>
        {/* mobile hamburger */}
        <HamburgerButton
          ref={hamburgerRef}
          aria-label={menuOpen ? 'Fechar menu' : 'Abrir menu'}
          aria-expanded={menuOpen}
          aria-haspopup="menu"
          onClick={() => setMenuOpen((v) => !v)}
        >
          {menuOpen ? <X size={18} /> : <Menu size={18} />}
        </HamburgerButton>

        {/* brand */}
        <BrandLink to={canUseWorkspace ? '/home' : '/requests'} aria-label="Prisma WorkSpace — página inicial">
          <BrandMark size={28} />
          <span className="label">Prisma<em> WorkSpace</em></span>
        </BrandLink>

        {/* desktop nav */}
        <Spacer />

        {/* search */}
        <SearchButton onClick={() => setSearchOpen(true)} aria-label="Abrir pesquisa global (Ctrl K)">
          <Search size={14} /><span>Buscar...</span><kbd>Ctrl K</kbd>
        </SearchButton>

        {/* org picker */}
        <OrganizationPicker title="Organização ativa">
          <Building2 size={14} />
          <select
            value={current.id}
            aria-label="Selecionar organização"
            onChange={(e) => {
              switchOrganization(e.target.value);
              navigate(current.role === 8 ? '/requests' : '/home');
            }}
          >
            {organizations.map((org) => (
              <option key={org.id} value={org.id}>{org.name}</option>
            ))}
          </select>
        </OrganizationPicker>

        {/* active timer */}
        {timer && (
          <ActiveTimer
            onClick={() => stopTimer.mutate(timer.workItemId)}
            title="Parar apontamento"
          >
            <Clock size={13} />
            <span>{timer.workItemTitle ?? 'Tarefa'}</span>
            {formatTime(elapsed)}
            <Square size={11} />
          </ActiveTimer>
        )}

        {/* novo item */}
        <Primary aria-label="Novo item" onClick={() => setCreateOpen(true)}>
          <Plus size={14} /><span>Novo item</span>
        </Primary>

        {/* notifications */}
        <NotificationCenter />

        <ThemeButton
          type="button"
          aria-label={mode === 'light' ? 'Ativar modo escuro' : 'Ativar modo claro'}
          title={mode === 'light' ? 'Modo escuro' : 'Modo claro'}
          onClick={toggleMode}
        >
          {mode === 'light' ? <Moon size={15} /> : <Sun size={15} />}
        </ThemeButton>

        {/* account menu */}
        <AccountWrap ref={accountRef}>
          <Avatar
            ref={avatarRef}
            aria-label="Menu da conta"
            aria-expanded={accountOpen}
            aria-haspopup="menu"
            onClick={() => setAccountOpen((v) => !v)}
          >
            EU<ChevronDown size={10} />
          </Avatar>
          {accountOpen && (
            <AccountDropdown role="menu">
              {canConfigure && (
                <DropItem
                  role="menuitem"
                  onClick={() => { setAccountOpen(false); navigate('/settings'); }}
                >
                  <Settings size={14} />Configurações
                </DropItem>
              )}
              <DropDivider />
              <DropItem role="menuitem" onClick={logout}>
                <LogOut size={14} />Sair
              </DropItem>
            </AccountDropdown>
          )}
        </AccountWrap>
      </Bar>

      {/* mobile menu overlay */}
      {menuOpen && (
        <MobileOverlay role="dialog" aria-modal="true" aria-label="Menu de navegação" onClick={handleOverlayClick}>
          <MobileMenu ref={menuRef} role="menu">
            {navItems.map(({ to, label, icon: Icone, end }, indice) => (
              <MobileNavItem
                key={to}
                to={to}
                end={end}
                role="menuitem"
                /* O primeiro item visível recebe o foco ao abrir o menu, qualquer que seja
                   ele: para o solicitante externo a lista começa em "Solicitações". */
                ref={indice === 0 ? firstMenuLinkRef : undefined}
              >
                <Icone size={17} />{label}
              </MobileNavItem>
            ))}
            {canConfigure && (
              <MobileNavItem to="/settings" role="menuitem">
                <Settings size={17} />Configurações
              </MobileNavItem>
            )}
          </MobileMenu>
        </MobileOverlay>
      )}

      {/* dialogs */}
      <GlobalSearchDialog
        open={searchOpen}
        currentProjectId={currentProjectId}
        onOpenChange={setSearchOpen}
        onCreateTask={() => setCreateOpen(true)}
      />
      <QuickCreateDialog
        open={createOpen}
        currentProjectId={currentProjectId}
        onOpenChange={setCreateOpen}
        onCreated={setToast}
      />
      {toast && <Toast role="status">{toast}</Toast>}
    </>
  );
}
