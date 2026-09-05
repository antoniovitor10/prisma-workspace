import { ReportsHub } from '../features/reports/ReportsHub';
import { HeaderLead, Page, PageHeader } from '../components/PageLayout';

export function Reports() {
  return <Page><PageHeader><HeaderLead><span className="eyebrow">Inteligência operacional</span><h1>Relatórios</h1><p>Transforme fluxo, esforço e capacidade em decisões que a equipe consegue executar.</p></HeaderLead></PageHeader><ReportsHub /></Page>;
}
