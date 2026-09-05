import styled from 'styled-components'

const Shell = styled.aside`
  position: fixed;
  inset: 0 auto 0 0;
  z-index: 30;
  display: flex;
  width: 252px;
  flex-direction: column;
  background: #102f4f;
  color: #fff;
  overflow-y: auto;

  @media (max-width: 820px) { width: 82px; }
`

const Brand = styled.div`
  padding: 25px 19px 18px;
  border-bottom: 1px solid rgba(255,255,255,.1);

  .mark {
    display: grid;
    width: 38px;
    height: 38px;
    place-items: center;
    margin-bottom: 12px;
    border: 1px solid rgba(255,255,255,.3);
    border-radius: 11px;
    background: #fff;
    color: #102f4f;
    font-size: 13px;
    font-weight: 820;
  }

  strong { display: block; font-size: 17px; letter-spacing: -.02em; }
  span { display: block; margin-top: 4px; color: #b9cbda; font-size: 11px; }

  @media (max-width: 820px) {
    padding-inline: 21px;
    strong, span { display: none; }
    .mark { margin: 0; }
  }
`

const Scroll = styled.div`padding: 13px 10px 24px;`
const Group = styled.div`
  & + & { margin-top: 14px; }
  h2 { margin: 0 10px 7px; color: #7fa1bd; font-size: 10px; font-weight: 760; letter-spacing: .1em; text-transform: uppercase; }
  @media (max-width: 820px) { h2 { display: none; } }
`
const Nav = styled.nav`display: grid; gap: 3px;`
const NavButton = styled.button<{ $active: boolean }>`
  display: grid;
  grid-template-columns: 30px minmax(0, 1fr);
  align-items: center;
  gap: 9px;
  width: 100%;
  border: 0;
  border-radius: 9px;
  background: ${({ $active }) => $active ? '#fff' : 'transparent'};
  color: ${({ $active }) => $active ? '#123a63' : '#e5edf4'};
  padding: 7px 9px;
  text-align: left;
  cursor: pointer;

  .icon {
    display: grid;
    width: 28px;
    height: 28px;
    place-items: center;
    border: 1px solid ${({ $active }) => $active ? '#c9d9e5' : 'rgba(255,255,255,.14)'};
    border-radius: 7px;
    background: ${({ $active }) => $active ? '#eef6fb' : 'rgba(255,255,255,.045)'};
    font-size: 9px;
    font-weight: 820;
    letter-spacing: .04em;
  }

  .label { overflow: hidden; font-size: 12.5px; font-weight: ${({ $active }) => $active ? 720 : 540}; text-overflow: ellipsis; white-space: nowrap; }
  &:hover { background: ${({ $active }) => $active ? '#fff' : 'rgba(255,255,255,.08)'}; }
  &:focus-visible { outline: 3px solid #79b9e4; outline-offset: -2px; }

  @media (max-width: 820px) {
    grid-template-columns: 1fr;
    justify-items: center;
    padding-inline: 0;
    .label { display: none; }
  }
`

type NavigationItem = readonly [string, string, string]
type NavigationGroup = { label: string; items: readonly NavigationItem[] }

export const navigationGroups: readonly NavigationGroup[] = [
  { label: 'Trabalho', items: [
    ['home', 'Visão geral', 'VG'],
    ['stories', 'Histórias do produto', 'HU'],
    ['specs', 'Especificações', 'ES'],
    ['execution', 'Trabalho dos agentes', 'AG'],
    ['validation', 'Homologação', 'HO']
  ] },
  { label: 'Governança', items: [
    ['gaps', 'Lacunas e decisões', 'LD'],
    ['gates', 'Aprovações', 'AP']
  ] },
  { label: 'Entender', items: [
    ['process', 'Processos', 'PR'],
    ['architecture', 'Arquitetura de IA', 'IA']
  ] }
] as const

export const navigation: NavigationItem[] = navigationGroups.flatMap(group => [...group.items])

export function Sidebar({ active, onNavigate }: { active: string; onNavigate: (id: string) => void }) {
  return <Shell>
    <Brand><div className="mark">EC</div><strong>Explorador de contexto</strong><span>Central de controle AI-Native</span></Brand>
    <Scroll>{navigationGroups.map(group => <Group key={group.label}><h2>{group.label}</h2><Nav aria-label={group.label}>{group.items.map(([id, label, icon]) => <NavButton key={id} title={label} $active={active === id} onClick={() => onNavigate(id)}><span className="icon">{icon}</span><span className="label">{label}</span></NavButton>)}</Nav></Group>)}</Scroll>
  </Shell>
}
