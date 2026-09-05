import styled from 'styled-components';
import type { HoursResponsible, OrganizationHoursReport } from '../../types/reports';

const fmt = (hours: number): string => {
  const total = Math.round((hours ?? 0) * 60);
  return `${Math.floor(total / 60)}h ${String(total % 60).padStart(2, '0')}m`;
};
const ratio = (value: number, base: number): number => (base > 0 ? Math.min(1, value / base) : 0);
const pctLabel = (value: number, base: number): string =>
  `${(base > 0 ? (value / base) * 100 : 0).toFixed(1).replace('.', ',')}%`;
const initials = (name: string): string =>
  name.trim().split(/\s+/).slice(0, 2).map((part) => part[0]?.toUpperCase() ?? '').join('') || '?';
const fmtDate = (iso: string): string => { const [y, m, d] = iso.split('-'); return `${d}/${m}/${y}`; };

const AUTO = '#06B6D4';
const MANUAL = '#F97316';

const Sheet = styled.section`
  overflow: hidden;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: 16px;
  background: ${({ theme }) => theme.color.surface};
  box-shadow: 0 1px 2px rgba(16, 24, 40, .04), 0 10px 30px rgba(16, 24, 40, .05);
`;

const Head = styled.header`
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 22px 24px 18px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  h2 { font-size: 18px; font-weight: 800; letter-spacing: -.01em; color: ${({ theme }) => theme.color.text}; }
  p { margin-top: 5px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; }
  .org { text-align: right; }
  .org strong { font-size: 14px; color: ${({ theme }) => theme.color.brand}; font-weight: 800; }
  .org span { display: block; margin-top: 3px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; }
`;

const Kpis = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 1px;
  background: ${({ theme }) => theme.color.border};
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
`;
const Tile = styled.div<{ $accent?: string }>`
  position: relative;
  padding: 16px 18px 15px;
  background: ${({ theme }) => theme.color.surface};
  &::before { content: ''; position: absolute; inset: 0 auto auto 0; width: 26px; height: 3px; border-radius: 3px; background: ${({ $accent, theme }) => $accent ?? theme.color.brand}; }
  span { color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; font-weight: 750; letter-spacing: .05em; text-transform: uppercase; }
  strong { display: block; margin-top: 9px; font-size: 23px; font-weight: 800; font-variant-numeric: tabular-nums; color: ${({ theme }) => theme.color.text}; }
`;

const Person = styled.article`
  padding: 18px 24px 20px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  &:last-child { border-bottom: none; }
`;
const PersonTop = styled.div`
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 13px;
`;
const Avatar = styled.div`
  display: grid;
  place-items: center;
  width: 36px; height: 36px;
  flex: 0 0 36px;
  border-radius: 50%;
  background: ${({ theme }) => theme.color.brand};
  color: ${({ theme }) => theme.color.onBrand};
  font-size: 14px; font-weight: 800;
`;
const PersonName = styled.div`
  min-width: 0;
  strong { display: block; font-size: 15px; font-weight: 750; color: ${({ theme }) => theme.color.text}; }
  span { color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; }
`;
const Util = styled.div`
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 12px;
  .num { text-align: right; }
  .num strong { display: block; font-size: 15px; font-weight: 800; font-variant-numeric: tabular-nums; color: ${({ theme }) => theme.color.text}; }
  .num span { color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; }
`;
const Bar = styled.div`
  position: relative;
  width: 160px; height: 9px;
  border-radius: 6px;
  overflow: hidden;
  background: ${({ theme }) => theme.color.neutral[100]};
  i { position: absolute; top: 0; bottom: 0; }
  @media (max-width: 620px) { width: 96px; }
`;

const Table = styled.table`
  width: 100%;
  border-collapse: collapse;
  font-size: 13.5px;
  font-variant-numeric: tabular-nums;
  th { padding: 6px 8px; text-align: right; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; font-weight: 750; letter-spacing: .04em; text-transform: uppercase; border-bottom: 1px solid ${({ theme }) => theme.color.border}; }
  th:first-child { text-align: left; }
  td { padding: 8px; text-align: right; color: ${({ theme }) => theme.color.text}; border-bottom: 1px solid ${({ theme }) => theme.color.neutral[50]}; }
  td:first-child { text-align: left; }
  tr.total td { font-weight: 800; border-top: 1px solid ${({ theme }) => theme.color.border}; border-bottom: none; }
  .dot { display: inline-block; width: 7px; height: 7px; border-radius: 50%; margin-right: 6px; vertical-align: middle; }
`;

const Empty = styled.div`
  padding: 48px 24px;
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
  font-size: 13.5px;
`;

function PersonBlock({ person }: { person: HoursResponsible }) {
  const autoWidth = ratio(person.automaticHours, person.businessHours) * 100;
  const manualWidth = ratio(person.manualHours, person.businessHours) * 100;
  return (
    <Person>
      <PersonTop>
        <Avatar>{initials(person.name)}</Avatar>
        <PersonName>
          <strong>{person.name}</strong>
          <span>Horas úteis: {fmt(person.businessHours)}</span>
        </PersonName>
        <Util>
          <Bar title={`${pctLabel(person.totalHours, person.businessHours)} das horas úteis`}>
            <i style={{ left: 0, width: `${autoWidth}%`, background: AUTO }} />
            <i style={{ left: `${autoWidth}%`, width: `${manualWidth}%`, background: MANUAL }} />
          </Bar>
          <div className="num">
            <strong>{fmt(person.totalHours)}</strong>
            <span>{pctLabel(person.totalHours, person.businessHours)} das úteis</span>
          </div>
        </Util>
      </PersonTop>
      <Table>
        <thead>
          <tr>
            <th>Projeto</th>
            <th><span className="dot" style={{ background: AUTO }} />Automáticas</th>
            <th><span className="dot" style={{ background: MANUAL }} />Manuais</th>
            <th>Total</th>
            <th>% das úteis</th>
          </tr>
        </thead>
        <tbody>
          {person.projects.map((project) => (
            <tr key={project.projectKey + project.projectName}>
              <td>{project.projectName}</td>
              <td>{fmt(project.automaticHours)}</td>
              <td>{fmt(project.manualHours)}</td>
              <td>{fmt(project.totalHours)}</td>
              <td>{pctLabel(project.totalHours, person.businessHours)}</td>
            </tr>
          ))}
          <tr className="total">
            <td>Total</td>
            <td>{fmt(person.automaticHours)}</td>
            <td>{fmt(person.manualHours)}</td>
            <td>{fmt(person.totalHours)}</td>
            <td>{pctLabel(person.totalHours, person.businessHours)}</td>
          </tr>
        </tbody>
      </Table>
    </Person>
  );
}

export function HoursReport({
  report, orgName, filterSummary,
}: { report?: OrganizationHoursReport; orgName: string; filterSummary: string }) {
  if (!report) return <Sheet><Empty>Carregando apontamentos...</Empty></Sheet>;
  return (
    <Sheet>
      <Head>
        <div>
          <h2>Horas registradas</h2>
          <p>Período: {fmtDate(report.from)} até {fmtDate(report.to)} · {filterSummary}</p>
        </div>
        <div className="org">
          <strong>{orgName}</strong>
          <span>Relatório por responsável</span>
        </div>
      </Head>
      <Kpis>
        <Tile><span>Horas úteis</span><strong>{fmt(report.businessHours)}</strong></Tile>
        <Tile $accent="#2563EB"><span>Total registrado</span><strong>{fmt(report.totalHours)}</strong></Tile>
        <Tile $accent={AUTO}><span>Automáticas</span><strong>{fmt(report.automaticHours)}</strong></Tile>
        <Tile $accent={MANUAL}><span>Manuais</span><strong>{fmt(report.manualHours)}</strong></Tile>
        <Tile $accent="#10B981"><span>% de aproveitamento</span><strong>{pctLabel(report.totalHours, report.businessHours)}</strong></Tile>
      </Kpis>
      {report.byResponsible.length === 0
        ? <Empty>Nenhuma hora registrada no período para o filtro selecionado.</Empty>
        : report.byResponsible.map((person) => <PersonBlock key={person.userId} person={person} />)}
    </Sheet>
  );
}
