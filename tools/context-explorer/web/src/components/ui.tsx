import styled from 'styled-components'

export const Page = styled.section`
  display: grid;
  gap: 20px;
  min-width: 0;
  animation: page-in .24s ease-out;

  @keyframes page-in {
    from { opacity: 0; transform: translateY(4px); }
    to { opacity: 1; transform: translateY(0); }
  }

  @media (prefers-reduced-motion: reduce) { animation: none; }
`

export const PageHeader = styled.header`
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;

  h1 {
    margin: 2px 0 7px;
    color: #102f4f;
    font-size: clamp(25px, 2vw, 34px);
    font-weight: 720;
    letter-spacing: -.035em;
  }

  p { max-width: 760px; margin: 0; color: #5f6f82; line-height: 1.55; }
`

export const Kicker = styled.span`
  color: #1671b8;
  font-size: 11px;
  font-weight: 760;
  letter-spacing: .12em;
  text-transform: uppercase;
`

export const Grid = styled.div<{ $columns?: number }>`
  display: grid;
  grid-template-columns: repeat(${({ $columns = 3 }) => $columns}, minmax(0, 1fr));
  gap: 14px;

  @media (max-width: 1450px) { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  @media (max-width: 900px) { grid-template-columns: 1fr; }
`

export const Card = styled.button`
  appearance: none;
  width: 100%;
  border: 1px solid #d9e1e8;
  border-radius: 14px;
  background: #fff;
  color: #17283a;
  padding: 17px;
  text-align: left;
  box-shadow: 0 1px 2px rgba(16, 47, 79, .03);
  cursor: pointer;
  transition: border-color .16s, transform .16s, box-shadow .16s;

  &:hover {
    border-color: #76acd2;
    transform: translateY(-2px);
    box-shadow: 0 10px 28px rgba(16, 47, 79, .08);
  }

  &:focus-visible { outline: 3px solid rgba(22, 113, 184, .22); outline-offset: 2px; }
  h2, h3 { margin: 0 0 8px; color: #122f4d; font-size: 16px; line-height: 1.35; }
  p { margin: 0; color: #607185; line-height: 1.5; }
`

export const Badge = styled.span<{ $tone?: 'blue' | 'success' | 'warning' | 'error' | 'neutral' }>`
  display: inline-flex;
  align-items: center;
  min-height: 24px;
  padding: 3px 9px;
  border: 1px solid ${({ $tone }) => $tone === 'success' ? '#bce6cd' : $tone === 'warning' ? '#f2d68a' : $tone === 'error' ? '#f3c0bb' : $tone === 'neutral' ? '#d9e1e8' : '#bdd9ed'};
  border-radius: 999px;
  background: ${({ $tone }) => $tone === 'success' ? '#edf9f2' : $tone === 'warning' ? '#fff9e8' : $tone === 'error' ? '#fff1ef' : $tone === 'neutral' ? '#f5f7f9' : '#eef7fd'};
  color: ${({ $tone }) => $tone === 'success' ? '#147144' : $tone === 'warning' ? '#8a6300' : $tone === 'error' ? '#a9362b' : $tone === 'neutral' ? '#536579' : '#176ca8'};
  font-size: 11px;
  font-weight: 720;
  white-space: nowrap;
`

export const Meta = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 7px;
  margin-bottom: 11px;
`

export const GraphShell = styled.div<{ $height?: number }>`
  height: ${({ $height = 590 }) => $height}px;
  min-width: 0;
  border: 1px solid #d7e0e8;
  border-radius: 16px;
  overflow: hidden;
  background: #fbfcfd;
  box-shadow: 0 8px 34px rgba(16, 47, 79, .055);
`

export const Panel = styled.div`
  min-width: 0;
  border: 1px solid #d9e1e8;
  border-radius: 14px;
  background: #fff;
  padding: 18px;
  box-shadow: 0 1px 2px rgba(16, 47, 79, .03);

  h2, h3 { margin-top: 0; color: #122f4d; }
  p { color: #607185; line-height: 1.55; }
`

export const Split = styled.div<{ $aside?: number }>`
  display: grid;
  grid-template-columns: minmax(0, 1fr) ${({ $aside = 320 }) => $aside}px;
  gap: 16px;
  min-width: 0;

  @media (max-width: 1080px) { grid-template-columns: 1fr; }
`

export const FilterRow = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 9px;

  select, input {
    min-height: 40px;
    border: 1px solid #ccd7e0;
    border-radius: 9px;
    background: #fff;
    color: #17283a;
    padding: 0 12px;
  }

  input { min-width: min(320px, 100%); }
  select:focus, input:focus { outline: 3px solid rgba(22, 113, 184, .16); border-color: #4b92c4; }
`

export const Button = styled.button<{ $primary?: boolean; $danger?: boolean }>`
  display: inline-flex;
  min-height: 38px;
  align-items: center;
  justify-content: center;
  gap: 7px;
  border: 1px solid ${({ $primary, $danger }) => $primary ? '#166eab' : $danger ? '#e2a39c' : '#ccd7e0'};
  border-radius: 9px;
  background: ${({ $primary, $danger }) => $primary ? '#166eab' : $danger ? '#fff1ef' : '#fff'};
  color: ${({ $primary, $danger }) => $primary ? '#fff' : $danger ? '#a9362b' : '#27445f'};
  padding: 0 13px;
  font-weight: 680;
  cursor: pointer;
  transition: background .15s, border-color .15s;

  &:hover { filter: brightness(.97); border-color: ${({ $primary }) => $primary ? '#166eab' : '#7ca8c7'}; }
  &:focus-visible { outline: 3px solid rgba(22, 113, 184, .2); outline-offset: 2px; }
`

export const Segmented = styled.div`
  display: inline-flex;
  gap: 3px;
  padding: 3px;
  border: 1px solid #d7e0e8;
  border-radius: 10px;
  background: #eef2f5;

  button {
    min-height: 32px;
    border: 0;
    border-radius: 7px;
    background: transparent;
    color: #607185;
    padding: 0 11px;
    font-size: 12px;
    font-weight: 680;
    cursor: pointer;
  }

  button[aria-pressed="true"] { background: #fff; color: #123a63; box-shadow: 0 1px 3px rgba(16, 47, 79, .1); }
`

export const Stat = styled.div`
  min-width: 0;
  border: 1px solid #dce4eb;
  border-radius: 13px;
  background: #fff;
  padding: 15px 16px;

  span { display: block; color: #718094; font-size: 11px; font-weight: 700; letter-spacing: .06em; text-transform: uppercase; }
  strong { display: block; margin-top: 5px; color: #123a63; font-size: 25px; letter-spacing: -.04em; }
  small { display: block; margin-top: 3px; color: #718094; }
`

export const Progress = styled.div<{ $value: number }>`
  height: 6px;
  overflow: hidden;
  border-radius: 999px;
  background: #e8edf1;

  &::after {
    display: block;
    width: ${({ $value }) => Math.max(0, Math.min(100, $value))}%;
    height: 100%;
    border-radius: inherit;
    background: ${({ $value }) => $value >= 80 ? '#25935a' : $value >= 55 ? '#e0a500' : '#d05a4d'};
    content: '';
  }
`

export const EmptyState = styled.div`
  display: grid;
  min-height: 180px;
  place-items: center;
  border: 1px dashed #cbd6df;
  border-radius: 14px;
  background: #fafcfd;
  color: #718094;
  padding: 24px;
  text-align: center;
`
