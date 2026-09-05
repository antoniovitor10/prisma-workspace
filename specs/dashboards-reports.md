# SPEC-DASHBOARDS-REPORTS: Dashboards e Relatórios

**Status:** approved

**Revisão funcional:** aprovada por PO durante a revisão módulo a módulo em 2026-08-24

**Homologação manual:** pendente

**Natureza:** contrato funcional desejado comparado ao estado atual do produto

## Propósito

Definir o catálogo oficial de dashboards e relatórios da plataforma, incluindo indicadores, filtros,
permissões, exportações, atualização, importação de dados do Runrun.it e rastreabilidade até os dados
de origem. A aprovação desta spec confirma o requisito funcional; não afirma que o produto atual já
o atende integralmente.

## Autoridade e compatibilidade

- Esta revisão registra decisões explícitas de PO e permanece `approved`.
- O construtor continua declarativo e limitado a um catálogo seguro executado no servidor, conforme D31;
  não aceita SQL fornecido pelo usuário.
- A decisão de incluir relatórios de **Custos** e **Orçado versus realizado** amplia o contrato além da
  D30, que atualmente proíbe valor-hora, salário, custo, faturamento e rentabilidade no modelo, DTOs e
  telas. Antes de implementar dados financeiros, a D30 deve ser formalmente sucedida ou ajustada em
  `DECISIONS.md` e o escopo/dados sensíveis precisam de aprovação específica. Até isso ocorrer, esses
  dois relatórios são requisitos aprovados, mas permanecem bloqueados para implementação.
- Tempo por etapa e lead time continuam sendo capturados internamente quando já houver suporte, mas
  ficam ocultos nas interfaces e relatórios por decisão de PO.
- Burnup e previsibilidade ficam ocultos por enquanto.

## Contrato funcional aprovado

### Dashboard completo

O dashboard deve oferecer, no mínimo:

- tarefas por status (nome da coluna atual, conforme D65), projeto, quadro, equipe e pessoa;
- tarefas atrasadas, urgentes, bloqueadas e concluídas;
- horas registradas por pessoa, equipe, tarefa e projeto;
- carga e distribuição de trabalho por pessoa e equipe;
- entregas no período por projeto, quadro, equipe e pessoa;
- comparação de horas estimadas versus realizadas;
- filtros coerentes com o escopo de acesso do usuário;
- drill-down de cada agregado até as tarefas ou apontamentos que formam o total.

Cada indicador deve informar sua definição, unidade, período considerado e instante da última
atualização. O total e o drill-down devem usar a mesma população autorizada.

### Catálogo de relatórios

O produto deve oferecer um catálogo equivalente aos relatórios relevantes do Runrun.it, adaptado ao
modelo interno:

1. **Controle Geral de Tarefas** — inventário filtrável de tarefas e seus principais atributos.
2. **Status Report Geral** — distribuição e evolução das tarefas por situação operacional.
3. **Timesheet por cliente** — horas registradas agrupadas por cliente.
4. **Horas por projeto** — horas estimadas e realizadas por projeto.
5. **Horas registradas** — apontamentos detalhados por período, pessoa, equipe e tarefa.
6. **Custos** — valores financeiros autorizados, visíveis somente a administradores e gestores;
   implementação bloqueada pela incompatibilidade atual com D30.
7. **Orçado versus realizado** — comparação financeira autorizada, visível somente a administradores
   e gestores; implementação bloqueada pela incompatibilidade atual com D30.
8. **Esforço por tipo de tarefa** — horas e volume agrupados por tipo.
9. **Alocação de pessoas** — distribuição de tarefas e horas por pessoa e equipe.
10. **Horas disponíveis** — disponibilidade conforme capacidade cadastrada e período.
11. **Gantt** — planejamento temporal conforme a spec específica de Gantt.
12. **Entregas e tarefas por pessoa/equipe** — volume aberto, concluído, atrasado e entregue.

O construtor declarativo permanece disponível como complemento ao catálogo, sem substituir a
necessidade de relatórios preparados com significado funcional claro.

### Funcionalidades ocultas

- Tempo por etapa, lead time e cycle time não devem aparecer no dashboard, catálogo, construtor ou
  exportações enquanto estiverem ocultos.
- Burnup e indicadores de previsibilidade não devem aparecer na interface ou exportações enquanto
  estiverem ocultos.
- Ocultar significa não oferecer a opção ao usuário; não exige apagar histórico já capturado.

### Filtros, período e atualização

- Os filtros devem afetar cartões, gráficos, tabelas, drill-down e exportações de forma consistente.
- O limite máximo de período é **configurável por relatório**; cada definição deve declarar e validar
  seu próprio limite.
- Períodos inválidos ou superiores ao limite do relatório devem ser rejeitados com mensagem clara.
- Dashboards devem atualizar automaticamente em tempo real quando os dados de origem mudarem.
- A atualização em tempo real não pode contornar autorização nem exibir resultados de outro tenant.
- A interface deve distinguir carregamento, ausência de dados, erro e dados aguardando correção.

### Exportação

- Todos os relatórios autorizados devem poder ser exportados em **CSV, XLSX e PDF**.
- A exportação deve usar o mesmo recorte, filtros, período e autorização da visualização.
- O arquivo deve identificar o relatório, período, filtros aplicados e data/hora de geração.
- Colunas ocultas ou proibidas não podem reaparecer no arquivo exportado.
- Exportações financeiras obedecem à restrição adicional de administradores e gestores.

## Atores e permissões

### Usuário comum

- Visualiza somente suas próprias tarefas e horas.
- Não recebe agregados de outras pessoas, mesmo que pertençam ao mesmo projeto, quadro ou equipe.
- Drill-down e exportação preservam exatamente essa restrição.

### Gestor

- Visualiza tarefas, horas e agregados das equipes que gerencia.
- Visualiza custos e orçamento somente depois da implementação autorizada do modelo financeiro.
- Não acessa equipes fora do seu escopo de gestão.

### Administrador

- Visualiza os dados autorizados de toda a organização ativa.
- Visualiza custos e orçamento somente depois da implementação autorizada do modelo financeiro.
- Não acessa dados de outra organização.

### Regras comuns

- A autorização deve acontecer antes da agregação.
- Acesso à página, ao drill-down, ao endpoint e à exportação deve produzir o mesmo escopo.
- Relatórios compartilhados não ampliam a permissão sobre os dados.
- Arquivos exportados não podem conter segredos, tokens ou campos sensíveis não autorizados.

## Importação de dados do Runrun.it

### Objetivo

Os relatórios devem considerar os dados importados do Runrun.it somente depois que entidades e
relações obrigatórias estiverem remapeadas e validadas no modelo interno. Nenhum valor ausente deve
ser inventado, estimado ou substituído silenciosamente.

### Fluxo obrigatório

1. Receber os dados brutos em área de **staging** isolada do modelo operacional.
2. Preservar temporariamente o identificador original do Runrun.it e a chave da execução de importação.
3. Construir um mapa transitório entre identificador de origem e identificador interno para projetos,
   clientes, usuários, equipes, quadros, tarefas, tipos, sprints, horas e demais relações importadas.
4. Validar unicidade, referências, tenant, vínculos e contagens antes da promoção.
5. Manter fora dos dashboards e relatórios qualquer registro incompleto ou com relação inválida.
6. Exigir correção manual dos registros incompletos e nova validação antes de promovê-los.
7. Confirmar que a carga é íntegra, reconciliada e não precisa ser repetida.
8. Somente depois dessa confirmação, descartar o identificador original do Runrun.it do modelo
   operacional, conforme decisão de PO.

### Descarte do identificador original

- O identificador original do Runrun.it não deve permanecer visível, pesquisável nem persistido no
  modelo operacional definitivo depois da reconciliação aprovada.
- O descarte não pode ocorrer enquanto houver carga parcial, correção pendente, reconciliação aberta,
  possibilidade de reprocessamento ou necessidade de comprovar equivalência entre origem e destino.
- A staging e o mapa transitório devem possuir retenção e acesso administrativo definidos antes da
  importação. A decisão de descarte final deve ser auditável sem expor dados brutos a usuários comuns.
- O mecanismo concreto de staging, retenção e descarte exige decisão arquitetural e, se persistido no
  SQL Server, `G-MIGRATION` antes da implementação.

### Risco crítico de idempotência e rastreabilidade

Descartar cedo o ID original elimina a chave mais segura para:

- repetir uma carga sem duplicar registros;
- diagnosticar divergências entre Runrun.it e plataforma interna;
- reconciliar comentários, horas, participantes e histórico que cheguem em lotes diferentes;
- provar qual registro interno corresponde ao registro de origem;
- corrigir uma migração parcial ou executar rollback controlado.

Por isso, o descarte aprovado é uma **etapa final e irreversível do processo de migração**, nunca uma
transformação de entrada. Antes dele, deve existir evidência de reconciliação, contagens, ausência de
órfãos e conclusão de todos os lotes dependentes. Sem essas garantias, a importação deve permanecer
pausada.

## Estado atual comprovado no código

### Backend

- `DashboardsController` expõe dashboards de colaborador, gestor e projeto.
- `ReportsController` expõe relatórios preparados, horas da organização, tempo por projeto e campos
  personalizados.
- `ReportBuilderController` oferece catálogo, criação, edição, exclusão, duplicação, execução,
  pré-visualização e exportação de relatórios salvos.
- `SavedReport` persiste definições declarativas validadas em JSON.
- O catálogo técnico possui fontes para tarefas, solicitações externas, projetos, equipes, usuários,
  sprints, apontamentos de horas e SLA.
- O executor suporta tabela, indicador, barras, colunas, linha, pizza e rosca, com operações de contagem,
  soma, média, percentual, mínimo, máximo e estimado versus realizado.
- A exportação do construtor é CSV.

### Interface

- A área de relatórios possui abas de relatórios prontos, construtor, apontamento de horas e campos
  personalizados.
- A visão preparada mostra tarefas abertas, concluídas, atrasadas e bloqueadas, solicitações externas,
  SLA, horas previstas/realizadas e distribuições por status, prioridade, origem, responsável, equipe e
  projeto.
- A interface atual oferece exportação PDF para superfícies de relatórios e CSV para relatórios salvos.
- Existem dashboards com KPIs, séries e lista de itens que exigem atenção.
- O relatório atual ainda exibe Burndown; esta revisão oculta apenas Burnup/previsibilidade, não altera
  automaticamente o comportamento existente.

### Importação

- Há evidência operacional de migração de dados do Runrun.it para a organização utilizada no piloto.
- As entidades operacionais auditadas não possuem um contrato uniforme de identificador original do
  Runrun.it.
- Não foi comprovada no produto uma staging persistente, mapa transitório genérico, fluxo de correção de
  registros incompletos ou descarte auditável do identificador de origem.

## Gaps entre a spec e o produto atual

1. **Catálogo funcional:** os relatórios preparados atuais não cobrem integralmente os 12 relatórios
   definidos nesta revisão.
2. **Tarefas por quadro:** o dashboard atual não apresenta uma dimensão consolidada de tarefas por quadro.
3. **Urgentes:** não há indicador preparado comprovado específico para tarefas urgentes.
4. **Carga e disponibilidade:** há recortes de carga/equipe e capacidade no catálogo técnico, mas não há
   comprovação do fluxo completo de distribuição e horas disponíveis conforme esta spec.
5. **Drill-down:** nem todo indicador atual permite navegar até exatamente os registros que formam o total.
6. **Permissões:** a regra atual de `ViewReport`/acesso ao projeto não comprova a matriz exigida de usuário
   comum somente próprio, gestor somente suas equipes e administrador toda a organização.
7. **Custos e orçamento:** não existem no modelo atual e conflitam com D30; exigem decisão formal, segurança,
   modelo de dados e possivelmente migration.
8. **Exportação:** CSV e PDF existem em fluxos distintos; XLSX e equivalência CSV/XLSX/PDF para todos os
   relatórios não estão implementados.
9. **Tempo real:** não há invalidação automática comprovada dos dashboards quando os dados mudam.
10. **Período configurável:** não existe contrato comprovado de limite individual configurável por relatório.
11. **Ocultação:** tempo por etapa/lead time e Burnup/previsibilidade precisam ser removidos das superfícies
    onde possam aparecer; a presença atual de métricas relacionadas deve ser auditada módulo a módulo.
12. **Dados incompletos:** não há estado consolidado que exclua dados importados incompletos até correção manual.
13. **Migração Runrun.it:** não há staging/mapa transitório genérico e auditável comprovado antes do descarte
    do identificador original.
14. **Homologação:** o catálogo completo, as permissões, as exportações e a atualização em tempo real ainda
    não foram homologados manualmente por PO.

## Regras e invariantes

1. Nenhum agregado ou exportação revela dados fora do escopo autorizado.
2. Totais e drill-down usam a mesma definição e população.
3. A mesma métrica com os mesmos filtros e dados produz o mesmo resultado.
4. Filtros afetam cartões, gráficos, tabelas e exportações de forma consistente.
5. Dados importados incompletos não participam dos resultados até correção e validação.
6. Resultados sem dados são diferentes de erro de carregamento.
7. Falhas parciais nunca são apresentadas como zero.
8. A ordenação e paginação das tabelas são determinísticas.
9. Unidades, timezone e arredondamento são consistentes entre API, tela e exportação.
10. Atualização em tempo real invalida apenas recortes autorizados da organização ativa.
11. O descarte do ID original só ocorre após reconciliação completa e encerramento formal da migração.

## Interface esperada

- Página responsiva com filtros, cartões, gráficos acessíveis, tabelas e drill-down.
- Alternância clara entre catálogo preparado, dashboards e construtor declarativo.
- Indicadores exibem definição, unidade, período e última atualização.
- Filtros ativos permanecem visíveis e são preservados durante drill-down e exportação.
- Estados de carregamento, vazio, erro e correção pendente são distintos.
- Relatórios e indicadores ocultos não aparecem em menus, catálogo, construtor ou exportação.
- Cores não são o único meio de comunicar estado ou variação.

## Contratos de API existentes

- `GET /api/dashboards/collaborator`
- `GET /api/dashboards/manager`
- `GET /api/dashboards/projects/{projectId}`
- `GET /api/reports/prepared`
- `GET /api/reports/hours`
- `GET /api/reports/projects/{projectId}/time`
- `GET /api/reports/projects/{projectId}/custom-fields`
- `GET /api/reports/catalog`
- `GET|POST|PUT|DELETE /api/reports/definitions...`
- `POST /api/reports/preview`
- `GET /api/reports/definitions/{id}/export`

Novos endpoints ou alterações de DTO necessários para catálogo completo, XLSX, atualização em tempo real,
correção de importados ou dados financeiros devem ser especificados e testados antes da implementação.

## Validações e erros

- `400 Bad Request`: período invertido, limite excedido, dimensão ou ordenação inválida.
- `401 Unauthorized`: autenticação ausente ou inválida.
- `403 Forbidden`: relatório, escopo, drill-down ou exportação não autorizados.
- `404 Not Found`: recurso inexistente ou invisível ao ator.
- `413 Payload Too Large` ou limite funcional equivalente: exportação acima do máximo permitido.
- `422 Unprocessable Entity`: filtros incompatíveis ou dado importado ainda não reconciliado.
- `429 Too Many Requests`: proteção de consultas/exportações custosas, se adotada.
- Falha de atualização em tempo real deve preservar o último resultado identificado como desatualizado e
  oferecer nova tentativa, sem misturar tenants.

## Critérios de aceite

- **Dado** um usuário comum, **quando** abre dashboards ou relatórios, **então** vê somente suas próprias
  tarefas e horas.
- **Dado** um gestor, **quando** consulta um relatório, **então** vê somente as equipes que gerencia.
- **Dado** um administrador, **quando** consulta um relatório, **então** vê a organização ativa inteira e
  nenhuma outra organização.
- **Dado** um indicador agregado, **quando** o usuário abre o drill-down, **então** os registros detalhados
  correspondem exatamente ao total autorizado.
- **Dado** um relatório com limite de período configurado, **quando** o usuário excede esse limite, **então**
  a consulta é bloqueada com explicação clara.
- **Dado** um dado operacional alterado, **quando** ele afeta o recorte aberto, **então** o dashboard se
  atualiza automaticamente sem recarregar a página.
- **Dado** um relatório autorizado, **quando** exportado em CSV, XLSX ou PDF, **então** os três formatos
  preservam filtros, período, escopo e valores.
- **Dado** um registro importado incompleto, **quando** um relatório é calculado, **então** esse registro não
  participa do resultado até a correção manual e nova validação.
- **Dado** uma carga do Runrun.it ainda não reconciliada, **quando** houver tentativa de descartar os IDs de
  origem, **então** a operação é bloqueada.
- **Dado** tempo por etapa, lead time, Burnup ou previsibilidade, **quando** o usuário navega por relatórios,
  **então** essas opções não aparecem.

## Homologação manual

Pendente. A revisão deve ser executada por perfil e módulo:

1. usuário comum: dados próprios, drill-down e exportações;
2. gestor: isolamento por equipes gerenciadas;
3. administrador: visão da organização e isolamento entre tenants;
4. catálogo completo e filtros de cada relatório;
5. equivalência entre tela, CSV, XLSX e PDF;
6. atualização em tempo real após alteração de tarefa ou horas;
7. ocultação de lead time/tempo por etapa e Burnup/previsibilidade;
8. exclusão de importados incompletos dos cálculos;
9. reconciliação da importação antes do descarte do identificador Runrun.it;
10. restrição de custos/orçamento a administradores e gestores após desbloqueio da D30.

## Test Gate Mapping

- Testes unitários para fórmulas, intervalos, timezone, dados ausentes e limites por relatório.
- Testes de integração para autorização antes da agregação, isolamento entre organizações e consistência
  entre resumo e drill-down.
- Testes de contrato para catálogo, metadados, filtros e formatos de exportação.
- Testes de interface para carregamento, vazio, erro, atualização, dados bloqueados e recursos ocultos.
- Testes CSV/XLSX/PDF para equivalência, segurança e ausência de colunas proibidas.
- Testes de tempo real para invalidação autorizada e reconexão.
- Testes de importação para idempotência, relações, órfãos, lotes incompletos, correção e bloqueio de descarte.
- Testes de desempenho com volume institucional aprovado.

## Dependências e gates

- `specs/audit-leadtime-history.md`.
- `specs/work-item-management.md`.
- `specs/boards-stages-wip.md`.
- `specs/time-tracking.md`.
- `specs/gantt-planning.md`.
- `specs/search-saved-filters.md`.
- `specs/user-access-permissions.md`.
- D30 deve ser formalmente ajustada antes de implementar custos e orçamento.
- Persistência de staging, mapa de importação, dados financeiros, cache ou materializações exige avaliação de
  `G-MIGRATION`.
- Mudanças de escopo/arquitetura para financeiro e importação exigem `G-SCOPE` antes da implementação.
- Promoção de dados importados ou mudanças em produção exige `G-DEPLOY`.

## Riscos

- Fórmulas ambíguas produzirem números conflitantes.
- Autorização aplicada depois da agregação revelar informações indevidas.
- Consultas e atualizações em tempo real degradarem o SQL Server.
- Divergência entre dashboard, drill-down e exportações.
- Dados financeiros ampliarem sensibilidade, autorização e obrigações de auditoria.
- Dados importados incompletos distorcerem resultados.
- Descartar IDs do Runrun.it cedo demais impedir idempotência, reconciliação, diagnóstico e rollback.
- Ausência de referência de origem tornar impossível provar equivalência após a migração.

## Decisões de implementação ainda necessárias

Estas decisões não reabrem o contrato funcional aprovado, mas precisam ser detalhadas antes de codificar:

- fórmulas e população exatas de cada indicador e relatório;
- limite máximo específico de cada relatório;
- mecanismo de atualização em tempo real e estratégia de reconexão;
- modelo financeiro que sucederá a restrição da D30;
- arquitetura, retenção e descarte da staging e do mapa transitório da importação;
- limites de volume, latência e expiração dos arquivos exportados.

## Rollback

- Desabilitar módulos, relatórios ou exportações novos sem alterar os dados operacionais.
- Interromper promoção/importação ao detectar divergência, preservando staging e mapa transitório.
- Não descartar IDs de origem nem staging durante rollback ou investigação.
- Reverter cache/materializações somente após preservar compatibilidade e aprovar eventual reversão de schema.

## Rastreabilidade

- `AGENTS.md`.
- `DECISIONS.md` — D30 e D31.
- `ROADMAP.md` — Fases 7 e 8.
- `src/Prisma.Workspace.Api/Controllers/DashboardsController.cs`.
- `src/Prisma.Workspace.Api/Controllers/ReportsController.cs`.
- `src/Prisma.Workspace.Api/Controllers/ReportBuilderController.cs`.
- `src/Prisma.Workspace.Application/Features/Reports/PreparedReportsAndDashboardsFeature.cs`.
- `src/Prisma.Workspace.Application/Features/Reports/OrganizationHoursReportFeature.cs`.
- `src/Prisma.Workspace.Application/Features/Reports/ReportBuilderFeature.cs`.
- `src/Prisma.Workspace.Web/src/features/reports/ReportsHub.tsx`.
- [Runrun.it — Como funciona](https://www.runrun.it/pt-BR/como-o-runrun-it-funciona).
- Materiais oficiais do blog do Runrun.it consultados durante a revisão funcional.
- CAND-DASHBOARDS-REPORTS -> SPEC-DASHBOARDS-REPORTS -> implementação futura -> testes -> homologação.
