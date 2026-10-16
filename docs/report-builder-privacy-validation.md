# Horas-3: privacidade B07 em todos os somatórios de horas

Estado: B07 ampliado passou na matriz HTTP inicial; correção de desempenho da leitura de projeto passou em xUnit384/384 e aguarda imagem/reprodução/full. Não é declaração PRONTO-PROD.

Um leitor podia consultar apontamentos de colegas pelo construtor de relatórios, apesar da restrição correta no relatório organizacional. A prova também confirmou o problema nos relatórios preparados e de tempo por projeto: com 1h própria e 2h de colega, um Viewer recebia 3h e duas pessoas. O Maestro encaminhou nominalmente esses handlers e o inventário completo das leituras de horas em 2026-10-16.

## Contrato e implementação

`IHoursReadAccessService` aplica o recorte de [dashboards-reports.md](../specs/dashboards-reports.md), linhas 101–124, antes de agregar ou serializar. Usuários comuns recebem apenas suas horas; gestores, as equipes que lideram; administradores, a organização ativa. A equipe vem do item ou, na ausência dela, do quadro. Projeto acessível e negações explícitas de organização, projeto, equipe, tarefa ou definição de relatório continuam obrigatórios. Acesso a um quadro ou compartilhamento de relatório não concede acesso às horas de terceiros.

Nas superfícies pessoais, o dono preserva seu histórico, inclusive quando é gestor e trabalhou em tarefa sem equipe liderada. Horas adicionais de terceiros exigem papel/equipe autorizados. Relatórios gerenciais usam estritamente sua população autorizada. A regra está explicitada em [time-tracking.md](../specs/time-tracking.md). Os portões de acesso ao recurso continuam aplicados; filtrar horas não concede acesso a uma tarefa ou projeto.

A política comum tem predicados traduzíveis para SQL e filtros para populações já carregadas. O ator da execução é obrigatório; uma definição compartilhada não herda as permissões do autor. A fonte Usuários preserva pessoas autorizadas sem lançamentos, com zero. Não há migration, alteração de ModelSnapshot, estrutura de auditoria, rota ou formato de DTO público.

## Inventário

A decisão abaixo usa DR = `specs/dashboards-reports.md:101–124`, TT = `specs/time-tracking.md:6–28`, UP = `specs/user-access-permissions.md:64,164–171`, WI = `specs/work-item-management.md:40–48`.

| Canal | Decisão e implementação |
| --- | --- |
| Relatório organizacional de horas | Já restrito; passa a compartilhar a política e conferir projetos/negações antes de somar. `OrganizationHoursReportFeature.cs:57`. DR/UP. |
| Construtor, todas as fontes e métricas | Restringir antes de linhas/métricas e horas embutidas. `ReportBuilderFeature.cs:319`. DR. |
| Relatório salvo e CSV | Recorte do ator autenticado em RunSaved/Export; negação da definição prevalece. `ReportBuilderFeature.cs:215`. DR122–124. |
| Preparados | Restringir itens, solicitações e horas próprias na mesma tarefa antes de Build. `PreparedReportsAndDashboardsFeature.cs:67`. DR. |
| Dashboard gerencial | Delega aos preparados com o mesmo ator. `PreparedReportsAndDashboardsFeature.cs:236`. DR. |
| Dashboard de projeto | Mesmo recorte dos preparados e da segunda consulta de tarefas. `PreparedReportsAndDashboardsFeature.cs:248`. DR. |
| Dashboard do colaborador | Já usa somente horas próprias; agora restringe projetos e recorta a interseção do período. `PreparedReportsAndDashboardsFeature.cs:287`. TT/DR. |
| Relatório de tempo por projeto | Restringir itens, entradas e agrupamentos por papel/projeto/equipe; filtro de terceiro não amplia. `ReportsFeature.cs:90`. DR. |
| Métricas do quadro | Predicados de itens e TimeEntries antes de GroupBy/Sum. `BoardMetricsQueries.cs:27`. DR/WI. |
| Empresa | Ator e projetos acessíveis antes do SUM SQL; metadados existentes preservados. `CompanyQueries.cs:22`. DR/WI. |
| Equipes | WeekHours só usa entradas autorizadas; cadastro/capacidades não derivados de TimeEntry ficam fora do B07. `TeamsFeature.cs:68`. DR/TT. |
| Totais de tarefa e quadro | Próprio histórico mais terceiros autorizados, depois do portão do recurso. `GetTimeTotalQueryHandler.cs:33`. TT/DR/WI. |
| Total por pessoa | Próprio histórico; comum recebe 403 ao pedir terceiro; gestor/admin também restringem projetos/população. Mesmo handler. TT/DR. |
| Lista de lançamentos da tarefa e TaskTimePanel | Filtrar antes de serializar pessoas, notas e durações; o cliente soma essa lista. `GetTimeEntriesByWorkItemQueryHandler.cs:45`. DR105/122. |
| Cartões de quadro e projeto | TotalTimeSeconds restrito; UserTimeSeconds próprio e snapshot TimeCalculatedAt preservados. `WorkItemCardProjection.cs:74`. TT/DR. |
| Subtarefas | Mesmo recorte do cartão; carrega Board para equipe herdada. `GetSubItemsQueryHandler.cs:35`. TT/DR. |
| Detalhe da tarefa | RealizedHours usa a mesma política, após autorização do recurso. `WorkItemManagementFeature.cs:123`. TT/DR/WI. |
| Minhas tarefas | Total restrito; UserTimeSeconds próprio; projeto acessível antes da projeção. `GetMyTasksQueryHandler.cs:32`. TT/DR. |
| Meu trabalho | Já restrito ao ator em BuildHours/MapTask, entradas encerradas e timer separado. `MyWorkDashboardFeature.cs:193`. TT. |
| Timer ativo/running, diário, semana e lista do ajuste | Já próprios, ator vindo do JWT; rateio Brasília existente. Não expõem entradas de terceiros. TT. |
| Busca do ajuste e justificativas | Tarefas acessíveis e justificativas próprias; sem somatório de terceiros. TT. |
| Criar/editar/excluir/iniciar/parar horas | Escrita própria e validação interna de períodos do mesmo usuário; não expõe agregado de terceiros. TT. |
| Productivity | Filtros salvos, regras e ações em massa não somam TimeEntries. Nenhuma alteração. |
| PDF/impressão | hoursPdf, reportExport e ReportsHub usam os DTOs autorizados; sem outra consulta de lançamentos. DR95–97/122. |
| Ferramentas de IA | AiWorkspaceTools projeta metadados/checklist/comentários/anexos; sem TimeEntries ou horas. RecentActivity usa eventos de WorkItem/StageHistory e auditoria de WorkItem; alterações TimeEntry têm outro EntityType. |
| Auditoria | Sem soma; snapshots exigem AdministerOrganization no servidor. `AuditFeature.cs:58`. Papel explicitamente autorizado, sem ampliar ao gestor/comum. |
| Formulários externos | TotalHours em ExternalFormsFeature é duração do formulário, não tempo de trabalho. Não é TimeEntry. |
| Totais antigos do repositório | GetTotalSecondsByWorkItemId/UserId/BoardId só têm declaração/implementação; nenhum chamador HTTP. Endpoints usam GetVisibleEntriesAsync com escopo obrigatório. |

## Cobertura e verificações

`ReportBuilderVisibilityTests` cobre papéis Viewer, Client, ProductOwner, ScrumMaster, ProjectManager, TeamMember, Manager e Administrator; outro tenant; projeto inacessível; negações; métricas e colegas na mesma tarefa; salvo compartilhado; fontes Projeto/Equipe/Sprint; usuário autorizado sem lançamento; preparados/relatório por projeto; política pessoal e predicado de consulta.

`e2e/report-builder-privacy.spec.ts` cobre fontes Apontamentos/Usuários na UI sem projeto e definição compartilhada/CSV. `e2e/hours-access-privacy.spec.ts` usa quatro papéis, projeto, equipes, usuários e intervalos próprios para conferir os canais do inventário, projeto isolado e negações. As fixtures usam UUID e limpeza no teardown. Um caso que agrupava relatórios e métricas foi dividido após medir 31,68s com limpeza, preservando todas as asserções e o timeout padrão.

| Comando / rodada | Resultado | Tempo |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln --no-restore` | Passou; 0 erros, 3 avisos anteriores | 82,33s |
| `dotnet test tests/Prisma.Workspace.Tests --no-build --no-restore` via runner QA | 383/383, nenhum skip; SQL/banco de unidade próprios | 64,46s runner |
| TypeScript explícito E2E com emissão, `--ignoreConfig --module ESNext --moduleResolution Bundler --types node` | Passou | 15,49s |
| `npm run lint` | 0 erros, 12 avisos anteriores | 1,56s |
| Docker candidato918c626 | Passou; health200/login200 | 281,77s |
| Matriz HTTP inicial desktop/mobile, sem retries | 39/39, nenhum skip; antes da divisão do caso lento | 590,95s nativo;593,77s runner |
| Matriz HTTP após divisão/fixture de arranjo | Pendente na próxima full | Pendente |
| UI do construtor, três repetições | 19/19,0skip/0falha/0retry; maior caso14,46s | 190,45s nativo;193,31s runner |
| `npx tsc -b` | Passou | 97,11s |
| Vitest threads,42arquivos | 155/155 | 286,65s |
| `npm run build` local | Passou | 107,77s |
| E2E completo com banco QA limpo, tentativa1 | Interrompido:24pass,1timeout novo,182não executados | 284,30s até interrupção |

A primeira tentativa HTTP passou três casos funcionais e parou em preparação: o prefixo de uma fixture tinha espaço e gerou e-mail inválido. O prefixo foi corrigido; não se alterou produto para acomodar o teste. Comandos manuais iniciais de tipos usaram configuração de módulo incompatível ou omitiram `--types node`; o comando válido acima passou. Não foi usado `tsc --noEmit`.

Somente prisma-qa app3110/SQL3111; xUnit usa Prisma_BoardColumns_Horas2. A imagem candidata contém exatamente918c626, com health/login200. Prisma-dev e produção intocados. Revisão e QA independente do SHA final ainda necessários. O N+1 de ProjectAccessService permanece reservado ao C6; não foi antecipado. Firefox/WebKit, provedor real de IA e produção não verificados.


## Arranjo da matriz e consultas frias

A primeira full foi interrompida após um timeout de30s no primeiro cenário HTTP de membro, na primeira leitura após criar três pessoas, duas equipes e seis intervalos. Nenhuma asserção de conteúdo falhou naquele caso; o tempo de preparação estava sendo somado às leituras frias. A próxima rodada medirá as etapas separadamente antes de fechar o diagnóstico de tempo.

A fixture `hours`, exclusiva deste novo spec, agora tem seu próprio prazo de30s para o arranjo. O corpo continua com30s e a limpeza continua na fixture request com20s. Anotações `hours-setup-ms` e `hours-body-ms` expõem os custos separados. É o mecanismo documentado de [timeout de fixture do Playwright](https://playwright.dev/docs/test-fixtures#fixture-timeout), sem aumentar o timeout do cenário. A comparação AST confirmou as mesmas62expressões de asserção antes/depois. Tipos e lint passaram após a mudança; browser/full ainda pendentes nessa forma.

O spec de UI do construtor cria seus intervalos em ontem de Brasília, para que continuem no período padrão de30dias quando o CI rodar em outro mês. As asserções continuam exigindo1h própria e2h do colega. Nenhum dado do seed é usado como precondição.


## Diagnóstico corrigido: leitura de cartões por projeto

Separar o arranjo foi útil para medir e organizar os testes, mas não resolveu a falha. A Full2 repetiu o timeout com setup5,489s e corpo29,974s; foi interrompida com37pass/1falha/169não executados. A reprodução do prefixo anterior da suíte mais quatro papéis registrou23pass/2timeouts/1skip antigo: `GET /api/WorkItems/project/{id}` levou30,974s para membro e30,696s para leitor e retornou500 ao cancelar. As leituras por quadro desses casos levaram0,897s e0,102s e retornaram200. Gestor/admin passaram com1,205s e0,255s no projeto.

A amostra da requisição no SQLQA estava executando SELECT de WorkItems por projeto, com17,536s decorridos/17,175s deCPU e nenhum bloqueio; o plano em cache contém105operadores. A consulta de projeto carregava várias coleções irmãs num único SELECT, estratégia que EF alerta como suscetível à [multiplicação por JOINs](https://learn.microsoft.com/ef/core/querying/single-split-queries). Não foi comprovado um defeito de fixture ou de ordenação dos specs como causa do custo SQL.

A alteração ficou somente em `WorkItemRepository.GetByProjectIdAsync`: `AsSplitQuery`, preservando as mesmas coleções, predicados, ordenação e DTOs. É o padrão já usado no AnalyticsRepository e no repositório de detalhes. A consulta por quadro e o N+1 reservado ao C6 não foram alterados. As leituras adicionais são o custo da estratégia; os cartões continuam sendo uma visão atualizável da aplicação.

O novo teste SQL `ProjectCards_PreserveCollectionsAndInheritedBoardScopeOnSqlServer` salva dois cartões com posição igual, subtarefa, dois responsáveis, dois apontamentos e dois itens de checklist; confere o grafo, Board.ProjectId/TeamId herdados e totais próprios versus equipe. A primeira preparação do teste precisou de AddRange explícito para as factories com GUID já preenchido; após corrigir só a fixture, o build passou e xUnit384/384 ficou verde. Imagem candidata, repetição do prefixo e Full3 ainda pendentes. Os resultados da imagem918c626 não validam essa nova estratégia de consulta.
