import type { OrganizationHoursReport, HoursResponsible } from '../../types/reports';

// PDF profissional do relatório de horas registradas, agrupado por responsável,
// com quebra por projeto. Texto vetorial (jsPDF), tema claro.

const fmt = (hours: number): string => {
  const total = Math.round((hours ?? 0) * 60);
  return `${Math.floor(total / 60)}h ${String(total % 60).padStart(2, '0')}m`;
};
const pct = (value: number, base: number): string =>
  `${(base > 0 ? (value / base) * 100 : 0).toFixed(1).replace('.', ',')}%`;
const fmtDate = (iso: string): string => { const [y, m, d] = iso.split('-'); return `${d}/${m}/${y}`; };

const MONTHS = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho',
  'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

const INK: [number, number, number] = [31, 41, 55];
const MUTED: [number, number, number] = [123, 132, 148];
const LINE: [number, number, number] = [226, 230, 236];
const BRAND: [number, number, number] = [22, 65, 148];

export async function exportHoursReportPdf(
  report: OrganizationHoursReport,
  orgName: string,
  filterSummary: string,
): Promise<void> {
  const { default: jsPDF } = await import('jspdf');
  const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
  const pageW = doc.internal.pageSize.getWidth();
  const pageH = doc.internal.pageSize.getHeight();
  const margin = 16;
  const right = pageW - margin;
  const cols = [right - 76, right - 51, right - 26, right]; // Auto, Manual, Total, %

  const setColor = (rgb: [number, number, number]) => doc.setTextColor(rgb[0], rgb[1], rgb[2]);

  // ── Cabeçalho ──────────────────────────────────────────────────────────
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(11);
  setColor(BRAND);
  doc.text('Prisma WorkSpace', right, 15, { align: 'right' });

  setColor(INK);
  doc.setFontSize(21);
  doc.text('Horas registradas', margin, 27);

  const now = new Date();
  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8);
  setColor(MUTED);
  doc.text(
    `Relatório gerado em ${now.getDate()} de ${MONTHS[now.getMonth()]} de ${now.getFullYear()}, `
    + `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`,
    margin, 33);

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(9.5);
  setColor(INK);
  doc.text('Filtro aplicado', margin, 43);
  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8);
  setColor(MUTED);
  doc.text(doc.splitTextToSize(filterSummary, right - margin), margin, 48);

  // ── Bloco da organização + KPIs ────────────────────────────────────────
  let y = 60;
  doc.setDrawColor(...LINE);
  doc.setLineWidth(0.2);
  doc.line(margin, y - 4, right, y - 4);
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(13);
  setColor(INK);
  doc.text(orgName, margin, y);
  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8);
  setColor(MUTED);
  doc.text(`Período: De ${fmtDate(report.from)} Até ${fmtDate(report.to)}`, margin, y + 5);

  const kpis: Array<[string, string]> = [
    ['Horas úteis', fmt(report.businessHours)],
    ['Total registrado', fmt(report.totalHours)],
    ['% aproveitamento', pct(report.totalHours, report.businessHours)],
  ];
  const kpiEdges = [right - 74, right - 37, right];
  kpis.forEach(([label, value], index) => {
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7.5);
    setColor(MUTED);
    doc.text(label, kpiEdges[index], y - 1, { align: 'right' });
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    setColor(INK);
    doc.text(value, kpiEdges[index], y + 5, { align: 'right' });
  });

  y += 14;

  const ensureSpace = (needed: number) => {
    if (y + needed > pageH - 18) { doc.addPage(); y = 22; }
  };

  const drawColumnHeader = () => {
    doc.setFillColor(244, 246, 249);
    doc.rect(margin, y - 4.5, right - margin, 7, 'F');
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(7);
    setColor(MUTED);
    doc.text('PROJETO', margin + 2, y);
    ['AUTOMÁTICAS', 'MANUAIS', 'TOTAL', '% ÚTEIS'].forEach((label, index) =>
      doc.text(label, cols[index], y, { align: 'right' }));
    y += 6.5;
  };

  const drawPerson = (person: HoursResponsible) => {
    const rows = person.projects.length + 1;
    ensureSpace(12 + rows * 6.5 + 8);

    // Cabeçalho do responsável
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    setColor(INK);
    doc.text(person.name, margin, y);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7.5);
    setColor(MUTED);
    doc.text(`Horas úteis: ${fmt(person.businessHours)}`, margin, y + 4.5);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(12);
    setColor(BRAND);
    doc.text(fmt(person.totalHours), right, y, { align: 'right' });
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7.5);
    setColor(MUTED);
    doc.text(`${pct(person.totalHours, person.businessHours)} das horas úteis`, right, y + 4.5, { align: 'right' });

    y += 10;
    drawColumnHeader();

    doc.setFontSize(8.5);
    person.projects.forEach((project) => {
      doc.setFont('helvetica', 'normal');
      setColor(INK);
      doc.text(project.projectName, margin + 2, y, { maxWidth: cols[0] - margin - 6 });
      [fmt(project.automaticHours), fmt(project.manualHours), fmt(project.totalHours),
        pct(project.totalHours, person.businessHours)]
        .forEach((value, index) => doc.text(value, cols[index], y, { align: 'right' }));
      y += 6.5;
      doc.setDrawColor(240, 242, 245);
      doc.line(margin, y - 2.4, right, y - 2.4);
    });

    // Total do responsável
    doc.setDrawColor(...LINE);
    doc.line(margin, y - 2.4, right, y - 2.4);
    doc.setFont('helvetica', 'bold');
    setColor(INK);
    doc.text('Total', margin + 2, y + 1.5);
    [fmt(person.automaticHours), fmt(person.manualHours), fmt(person.totalHours),
      pct(person.totalHours, person.businessHours)]
      .forEach((value, index) => doc.text(value, cols[index], y + 1.5, { align: 'right' }));
    y += 12;
  };

  if (report.byResponsible.length === 0) {
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9);
    setColor(MUTED);
    doc.text('Nenhuma hora registrada no período para o filtro selecionado.', margin, y + 4);
  } else {
    report.byResponsible.forEach(drawPerson);
  }

  // ── Rodapé ─────────────────────────────────────────────────────────────
  const pages = doc.getNumberOfPages();
  for (let page = 1; page <= pages; page++) {
    doc.setPage(page);
    doc.setDrawColor(...LINE);
    doc.line(margin, pageH - 13, right, pageH - 13);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7);
    setColor(MUTED);
    doc.text('Relatório gerado pelo Prisma WorkSpace', margin, pageH - 8.5);
    doc.text(`Página ${page}/${pages}`, right, pageH - 8.5, { align: 'right' });
  }

  doc.save(`horas-registradas-${report.to}.pdf`);
}
