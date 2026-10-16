# Horas-3: privacidade B07 em todas as leituras de horas

Resultado do executor: xUnit384/384; Vitest155/155; tipos, lint e builds aprovados; E2E completo200aprovados/6skips antigos/1falha antiga permitida, sem retries. Todos os78cenários de horas/B07 passaram. Revisão do Prumo e QA independente do SHA final continuam necessários antes de PRONTO-PROD.

Um leitor podia consultar horas de colegas pelo construtor, relatórios preparados e relatório por projeto. A prova anterior registrou1h própria e2h do colega, mas recebeu3h e duas pessoas. O Maestro encaminhou nominalmente esses handlers e o inventário de todos os somatórios em2026-10-16.

## Contrato e correção

`HoursReadAccessService` aplica [dashboards-reports.md:101–124](../specs/dashboards-reports.md): comum só próprias horas; gestor somente equipes lideradas; administrador organização ativa. A equipe é a do item ou do quadro. Membro ativo, projeto acessível e negações explícitas precedem agregação e serialização. Compartilhar uma definição ou ter acesso ao quadro não concede as horas de terceiros.

Consultas pessoais preservam o histórico do próprio dono; horas adicionais exigem papel/equipe autorizados. Relatórios gerenciais usam estritamente sua população. [time-tracking.md](../specs/time-tracking.md) explicita essa distinção. O ator é obrigatório no executor; fontes, métricas, definições compartilhadas e CSV usam o ator da execução. Usuários autorizados sem lançamento preservam zero. Os portões do recurso continuam obrigatórios.

A consulta de cartões por projeto carregava várias coleções irmãs em um SELECT. A sequência completa revelou leituras de30,974s/500 para membro e30,696s/500 para leitor; quadro retornava200 em0,897s/0,102s. O SQL estava emCPU, sem bloqueio, e o plano continha105operadores. A correção63fb38b usa `AsSplitQuery` somente em `GetByProjectIdAsync`, preservando grafo/predicados/ordem/DTO. A mesma sequência, em banco recriado, passou com894ms/200 e208ms/200, e gestor/admin221ms/223ms. Não houve alteração no N+1 reservado ao C6 nem na consulta por quadro. A estratégia já é usada em Analytics e detalhes; [documentação EF](https://learn.microsoft.com/ef/core/querying/single-split-queries).

Não há migration, mudança de ModelSnapshot ou estrutura de auditoria; rotas e formatos de DTO permanecem compatíveis. O frontend de produto não foi alterado nesta correção do B07.

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

## Cobertura e resultados reais

`ReportBuilderVisibilityTests` tem43casos de papel, tenant, projeto, negações, métricas, relatório compartilhado, fontes Projeto/Equipe/Sprint, usuário sem lançamentos, preparados/tempo por projeto e política pessoal. O testeSQL novo de cartões preserva duas raízes com posição igual, filho, dois responsáveis, dois logs, checklist e equipe herdada do quadro, incluindo os totais por papel.

O novo `hours-access-privacy.spec.ts` tem46casos desktop/mobile e todas as62expressões de asserção foram preservadas ao separar o arranjo. `report-builder-privacy.spec.ts` tem6casos de UI/CSV; suas datas usam ontem de Brasília, sem envelhecer para fora da janela padrão de30dias. Fixtures têm UUID e limpeza; sem sleeps, retries, novos skips ou test.fail.

| Comando / rodada | Resultado | Tempo |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln --no-restore` | Passou; 0erros/3avisos anteriores no build integral; final incremental0erros/1aviso anterior | 119,12s;39,11s final |
| `dotnet test tests/Prisma.Workspace.Tests --no-build --no-restore` via runner privado SQLQA | 384/384,0skips | 61,12s runner;57,72s testes |
| `npx tsc -b` | Passou | 97,11s |
| `npm run lint` | 0erros/12avisos anteriores | 1,17s última rodada |
| `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` | 155/155 em42arquivos | 286,65s |
| `npm run build` local | Passou | 107,77s |
| Tipos E2E explícitos, com emissão fora doGit e `--ignoreConfig --module ESNext --moduleResolution Bundler --types node` | Passou | 11,54s |
| Matriz HTTP inicial desktop/mobile | 39/39 antes de dividir o cenário amplo | 590,95s nativo;593,77s runner |
| Prova a frio após reiniciar somenteQAapp | 9/9; setup máx10,078s/corpo6,953s; não reproduziu a sequência problemática | 101,66s nativo;103,74s runner |
| UIbuilder com data dinâmica, `--repeat-each=3 --retries=0` | 19/19 com setup, pico10,07s | 140,11s nativo;142,12s runner |
| Prefixo exato que reproduzia o SQL lento, após correção e novo banco | 25aprovados/1skip antigo/0falhas | 199,92s nativo;203,67s runner |
| Docker da candidata27df845 | Passou; health200/login200 | 215,19s |
| `npm run e2e -- --retries=0 --trace=off --reporter=line,json`, bancoQA recriado | 200aprovados/6skips antigos/1falha antiga,0retries | 1.540,09s nativo;1.542,37s runner |

A full aprovou46casos da matrizHTTP,6do construtor/CSV,8de correções de horas e18do follow-up:78afetados. Houve86limpezas/188ações/0falhas de limpeza; maiorcleanup2,195s. Na matriz, setup máximo8,711s e corpo15,026s. A fixture tem prazo próprio30s de arranjo; o corpo continua30s e o request tem20s de teardown. Anotações de path/status/ms não registram autenticação; [documentação Playwright](https://playwright.dev/docs/test-fixtures#fixture-timeout).

A única falha final foi a conhecida `rich-task-description.spec.ts:63` no mobile, timeout ao reabrir o detalhe; esse arquivo é idêntico à main. O GOALS permite expressamente essa falha antiga e ela não foi corrigida nesta entrega. Os seis skips são condicionais já existentes: instalação SQL(2), onboarding somente desktop(2), teste de overflow somente mobile e teste deNovaColuna somente mobile. Não houve novo skip nem retry para esconder falha.

## Tentativas anteriores e correção do diagnóstico

- A primeira matriz parou em e-mail inválido produzido por espaço no prefixo da fixture; o prefixo foi corrigido, mantendo as asserções.
- Full1 interrompida:24pass/1timeout/182não executados. Separar o arranjo melhorou a medição, mas não resolveu a causa.
- Full2 interrompida:37pass/1timeout/169não executados; setup5,489s/corpo29,974s provou que o orçamento de preparação não explicava o problema SQL.
- Prefixo de diagnóstico:23pass/2timeouts/1skip antigo, com path e tempos que identificaram o endpoint por projeto. A amostra SQL confirmou CPU, sem bloqueio. ApósAsSplitQuery, o mesmo prefixo e a full foram concluídos; a full teve somente a falha antiga de descrição rica.
- O novo teste SQL teve uma preparação inicial comGUIDs interpretados como entidades existentes. `AddRange` explícito corrigiu só a fixture; a execução posterior384/384 passou. Não se mudou produto para acomodar esse erro.

Artefatos privados em `.runlogs/horas-3`: relatórios JSON nativos, tempos, logs redigidos, backups600 verificados, planoSQL e manifestos. Não foi usado `tsc --noEmit` nem foram impressos segredos.

## Ambiente e limites

Somente prisma-qa app3110/SQL3111, bancoPrismaWorkspace_QA; xUnit usa Prisma_BoardColumns_Horas2. Cada rodada completa teve backup comRESTOREVERIFYONLY e reset exclusivamenteQA porSQL versionado/seedDevelopment oficial. O backup da full final tinha14.798.848bytes. Prisma-dev e produção não foram tocados.

O N+1 de ProjectAccessService continua paraC6. Relógio do navegador, início do timer fora da validação de criação/edição, concorrência de lançamentos e editor noturno sem data explícita são ressalvas já registradas. Limite para cronômetro esquecido continua pendente doPO. Firefox/WebKit, provedor real deIA e produção não foram verificados. Revisão e QA independente do SHA final permanecem obrigatórios; só depois de PUBLICADO começaC1–C6.
