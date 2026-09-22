// Exportação de relatório para PDF (download direto).
// jspdf e html2canvas-pro são carregados sob demanda para não pesar o bundle inicial.

const MARGIN_MM = 12;
const HEADER_MM = 16;

export interface ReportPdfHeader {
  /** Nome da organização, impresso à esquerda. */
  organization: string;
  /** Título do relatório exportado. */
  title: string;
  /** Filtro aplicado, impresso abaixo do título. */
  subtitle?: string;
}

/**
 * Converte o nó do relatório em PDF A4 respeitando margens e repetindo cabeçalho e
 * número de página em todas as folhas.
 *
 * O conteúdo continua rasterizado porque os gráficos são canvas; o que muda é que ele
 * deixa de ocupar a folha inteira sem margem nem identificação, que era a queixa.
 * Relatórios tabulares devem preferir uma geração vetorial, como `hoursPdf.ts`.
 */
export async function exportNodeToPdf(
  node: HTMLElement,
  fileName: string,
  header: ReportPdfHeader,
): Promise<void> {
  const [{ default: jsPDF }, { default: html2canvas }] = await Promise.all([
    import('jspdf'),
    import('html2canvas-pro'),
  ]);

  const canvas = await html2canvas(node, {
    scale: 2,
    backgroundColor: '#ffffff',
    useCORS: true,
    windowWidth: node.scrollWidth,
  });

  const pdf = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
  const pageWidth = pdf.internal.pageSize.getWidth();
  const pageHeight = pdf.internal.pageSize.getHeight();

  const contentWidth = pageWidth - MARGIN_MM * 2;
  const contentTop = MARGIN_MM + HEADER_MM;
  const contentHeight = pageHeight - contentTop - MARGIN_MM;
  const imgHeight = (canvas.height * contentWidth) / canvas.width;
  const imgData = canvas.toDataURL('image/png');
  const emitido = new Date().toLocaleString('pt-BR');

  const drawHeader = (pageNumber: number, pageCount: number) => {
    pdf.setFontSize(11);
    pdf.setTextColor(30, 41, 59);
    pdf.text(header.title, MARGIN_MM, MARGIN_MM + 4);
    pdf.setFontSize(8);
    pdf.setTextColor(100, 116, 139);
    pdf.text(header.organization, pageWidth - MARGIN_MM, MARGIN_MM + 4, { align: 'right' });
    if (header.subtitle) pdf.text(header.subtitle, MARGIN_MM, MARGIN_MM + 9);
    pdf.text(`Emitido em ${emitido}`, MARGIN_MM, pageHeight - MARGIN_MM + 4);
    pdf.text(`Página ${pageNumber} de ${pageCount}`, pageWidth - MARGIN_MM, pageHeight - MARGIN_MM + 4,
      { align: 'right' });
    pdf.setDrawColor(226, 232, 240);
    pdf.line(MARGIN_MM, contentTop - 3, pageWidth - MARGIN_MM, contentTop - 3);
  };

  const pageCount = Math.max(1, Math.ceil(imgHeight / contentHeight));
  for (let page = 0; page < pageCount; page += 1) {
    if (page > 0) pdf.addPage();
    drawHeader(page + 1, pageCount);
    // Desloca a imagem para cima a cada folha e recorta pela área útil.
    pdf.addImage(imgData, 'PNG', MARGIN_MM, contentTop - page * contentHeight, contentWidth, imgHeight);
    // Cobre o que ultrapassa a área útil para não invadir margem nem rodapé.
    pdf.setFillColor(255, 255, 255);
    pdf.rect(0, 0, pageWidth, contentTop - 3.5, 'F');
    pdf.rect(0, contentTop + contentHeight, pageWidth, pageHeight - contentTop - contentHeight, 'F');
    drawHeader(page + 1, pageCount);
  }

  pdf.save(fileName.endsWith('.pdf') ? fileName : `${fileName}.pdf`);
}
