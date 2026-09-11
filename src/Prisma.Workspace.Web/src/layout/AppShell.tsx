import { Outlet } from 'react-router-dom';
import styled from 'styled-components';
import { ContextBarProvider, ContextBarSlot } from './ContextBar';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';

/* Lateral contextual (só no projeto) e coluna de conteúdo são irmãos (D88). */
const Shell = styled.div`
  display: flex;
  align-items: flex-start;
  min-height: 100vh;
`;

const Coluna = styled.div`
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  min-height: 100vh;
`;

const StickyTop = styled.div`
  position: sticky;
  top: 0;
  z-index: 30;
`;

const Content = styled.main`
  flex: 1;
  min-width: 0;
  background:
    radial-gradient(circle at 0 0, color-mix(in srgb, ${({ theme }) => theme.color.accentViolet} 5%, transparent), transparent 26rem),
    radial-gradient(circle at 100% 20%, color-mix(in srgb, ${({ theme }) => theme.color.accentBlue} 4%, transparent), transparent 24rem),
    ${({ theme }) => theme.color.bg};
`;

export function AppShell() {
  return (
    <ContextBarProvider>
      <Shell>
        <Sidebar />
        <Coluna>
          <StickyTop>
            <Topbar />
            <ContextBarSlot />
          </StickyTop>
          <Content>
            <Outlet />
          </Content>
        </Coluna>
      </Shell>
    </ContextBarProvider>
  );
}
