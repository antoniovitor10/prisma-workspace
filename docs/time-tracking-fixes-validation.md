# Correção de horas — tarefa 7, 2026-10-15

Base `origin/main` / `2c3816c`; branch `fix/time-tracking-20261015`. Implementação autorizada diretamente pelo PO, incluindo atualização do contrato. Sem migration, alteração de estrutura de auditoria, merge de L08, produção, main ou agentes adicionais.

## Causas e correções

- Kanban somava o total da API (já com timer aberto) ao período inteiro do navegador. `WorkItemCardProjection`, `GetSubItemsQueryHandler` e `GetMyTasksQueryHandler` agora informam `timeCalculatedAt`, usando o mesmo instante nos agregados. `utils/liveTime.ts` acrescenta somente o tempo posterior ao cálculo ou ao início, quando posterior. Mantidos totais da API e horas de terceiros. A tela atual Meu trabalho usa outro endpoint, `/api/me/work`, que já devolve somente intervalos encerrados; preservado e testado. Drawer/painel já calculavam cada intervalo uma vez; adicionada regressão de 18h28 para confirmar.
- Não existiam PUT/DELETE de TimeEntry. Commands novos restringem ao proprietário e revalidam acesso à tarefa, timer encerrado, fim > início, ausência de futuro e de sobreposição (inclusive outros registros próprios em outra organização, sem expor seus dados). Reutilizado `DomainException` retorna 400, acesso negado retorna 403 e recurso ausente no tenant retorna 404. A auditoria existente de `AppDbContext` grava ator e antes/depois na mesma SaveChanges; UPDATE usa entidade rastreada para preservar originais; DELETE preserva snapshot anterior. Origem manual/timer e autoria do registro não mudam.
- Ajustar tarefas lista intervalos próprios do dia, com início/fim/duração/origem/observação. Editor usa horários de Brasília, exclusão exige confirmação no modal. Mutações recarregam dia, semana e cartões. `/me/tasks` oferece apenas atribuídas e não cobre a busca das demais. A consulta única `/api/me/time/tasks?query=` substitui requisições por quadro: título/#, atribuídas primeiro, sem tarefas/projetos arquivados, somente projetos acessíveis, até 50 resultados; teclado, clique e mobile.
- Diário usava UTC e semana UTC-03. Ambos agora usam `TimeTrackingDay` / America/Sao_Paulo. Não há rateio: o intervalo inteiro pertence ao dia do início.

## Cobertura

- `tests/Prisma.Workspace.Tests/TimeTrackingCorrectionTests.cs`: 14 casos para dono/não dono, timer aberto, fim anterior/igual, futuro, sobreposição com encerrado/aberto, adjacência, acesso à tarefa, snapshots de atualização/exclusão, dia de Brasília e snapshot do card.
- Vitest: `utils/liveTime.test.ts`, `MinhasTarefas.test.tsx`, `TaskTimePanel.test.tsx`, `features/time/TimeAdjustment.test.tsx`. Timer pré-existente de 18h28, horas encerradas e recarga, além de seleção/reabertura acessível da busca.
- `e2e/time-tracking-fixes.spec.ts`: linha 30 timer UI + polling de tempo real + reload sem duplicar; 55 edição pela UI + API + total diário/semanal + reload; 78 exclusão/cancelamento/confirmação + totais; 98 busca nome/# e teclado, atribuídas antes das demais e arquivadas ausentes. Desktop/mobile, usuário/projeto/quadro/colunas/tarefas UUID próprios e limpeza finally. Factories copiadas seletivamente de L08, sem merge ou seed de negócio.

## Verificações

| Comando | Resultado | Tempo |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln` | Passou; 0 erros, 3 avisos anteriores | 140,86 s |
| `bash scripts/test-server-dev.sh` | xUnit 325/325, zero skips | 52,69 s |
| `npx tsc -b` | Passou | 113,97 s |
| `npm run lint` | Passou; 0 erros, 12 avisos anteriores | 0,64 s |
| `npm test` | Vitest 151/151 em 41 arquivos | 272,97 s |
| `npm run build` | Passou | 140,25 s |
| Tipos E2E explícitos com tsc `--ignoreConfig --types node ... --outDir` | Passou | 7,10 s |
| `npm run e2e -- time-tracking-fixes.spec.ts` (runner isolado, API real 3100) | 9/9 com setup, desktop/mobile, zero retries | 138,08 s |
| Reset por `scripts/e2e-create-database.sql`, guard de projeto/banco e boot existentes | 28 migrations, um projeto seed | 61,33 s |

Tempos da rodada focal aprovada: timer 18,1/18,7 s; edição 17,8/18,1 s; exclusão 9,6/15,5 s; busca 8,8/15,5 s (desktop/mobile). Suíte completa após banco limpo: **122 aprovados, 5 falhas em testes antigos, 6 skips, zero retries**, 1480,79 s (`npm run e2e`, runner réplica isolada). Os oito casos de horas passaram. A suíte global não está verde.

| Falha preservada | Projeto | Tempo |
| --- | --- | --- |
| `ai-assistant.spec.ts:221` | chromium-desktop | 37.247 s |
| `ai-assistant.spec.ts:340` | chromium-desktop | 32.886 s |
| `ai-assistant.spec.ts:340` | chromium-mobile | 33.193 s |
| `board-stage-sync.spec.ts:12` | chromium-desktop | 19.813 s |
| `rich-task-description.spec.ts:18` | chromium-mobile | 34.235 s |

IA 221/340 e descrição rica excederam 30 s; board-stage-sync desktop esperou Concluído e ainda viu Em andamento após 5 s. A causa dessas falhas antigas não foi isolada nesta tarefa; os arquivos foram preservados. Board-stage-sync mobile e descrição rica desktop passaram. A falha mobile antiga da descrição rica reapareceu. Nenhuma falha foi marcada como esperada para ocultá-la.

| Horas na suíte completa | Desktop | Mobile |
| --- | --- | --- |
| Timer após reload | 23.08 s | 23.85 s |
| Editar e persistir | 21.56 s | 25.11 s |
| Excluir/cancelar | 12.11 s | 13.99 s |
| Buscar nome/número | 15.99 s | 16.39 s |

Os casos na base percorrida pela suíte chegaram a 25,1 s; a causa exata do aumento não foi isolada nesta rodada. A consulta de acesso por projeto e a listagem global de quadros são limitações anteriores de desempenho. Não foram impostas novas regras de produto ou ampliados timeouts. Falhas/JSON/artefatos da rodada completa preservados em `.runlogs/time-fixes/full-failures` e `.runlogs/e2e-l08/t7-full-verified.json`, sem traces ou tokens expostos. Após a padronização do nome interno Correct (sem mudança de comportamento), a imagem final recebe nova rodada focal em 3100, também registrada abaixo.

## Ambiente e limites

Prisma-dev em 0.0.0.0:3100, SQL próprio em 127.0.0.1:3102, DetranKanban_E2E. Chromium/Node do Playwright 1.62.1 em container prisma-dev com rede none e transporte TCP/Unix privado para o API real em 3100: evita o ERR_NETWORK_CHANGED previamente comprovado no host. Fonte externa usa fallback somente na fixture nova; API/JS/ativos locais/persistência reais. Sem sleeps, retries, timeout aumentado ou mocks de API.

Para suíte completa, réplica da mesma imagem imutável da API em 3115 interna compartilha loopback do runner para o provedor falso de IA 3103 dos testes existentes; usa o mesmo SQL próprio via transporte transparente. Drivers/env locais ignorados em `.runlogs/e2e-l08`, `.runlogs/time-fixes` e `.env.server-e2e-isolated` (600). Recursos temporários são encerrados ao terminar; QA não é resetada.

Rodadas intermediárias não são aprovação: tentativa de login durante reset antes de health; setup da suíte completa com transporte SQL encerrado pela sessão, corrigido com sessão independente e restart somente da réplica (health não prova conexão SQL; login 200 comprovado antes de reiniciar); preenchimento datetime-local com segundos zero gerava Malformed value porque Chromium normaliza o valor; busca selecionada fechada exigiu reabertura por seta/clique; seletor exato de label da textarea substituído por papel acessível; verbo de arquivamento corrigido de POST para DELETE no teste. Nenhum teste existente ou produto alheio ao escopo foi alterado. Formato normalizado e motivo de Malformed value conferidos no código/documentação oficial do [Playwright](https://github.com/microsoft/playwright/blob/main/packages/injected/src/injectedScript.ts).

Não verificados: QA humano, Firefox/WebKit, clientes com relógio/fuso históricos de horário de verão, concorrência simultânea de alterações. Sobreposição/futuro na criação manual permanecem comportamento anterior, fora da implementação dos novos endpoints; não há constraint transacional para impedir corridas concorrentes.

Pendências do PO: permissão administrativa específica para horas de terceiros (não encontrada, somente dono nesta entrega); rateio de intervalos à meia-noite; limite/alerta para timer esquecido. O período 14/10 12:44–15/10 07:12 permanece 18h28 inteiras em 14/10 no diário e na semana. Não foi imposto limite diário ou criado apontamento negativo. Excluir remove o lançamento do total e preserva a auditoria existente; restaurar dados excluídos exige ação autorizada a partir dela.


## QA visual

Imagem `prisma-qa-app:time-tracking-20261015`, fonte funcional `d7758c1`. Build final 168,86 s; atualização somente do serviço app prisma-qa, 23,86 s. SQL próprio 127.0.0.1:3111 / PrismaWorkspace_QA, StartedAt idêntico antes/depois; banco e volumes preservados. GET /health, SPA e login 200 em 127.0.0.1:3110; novas consultas `/api/me/time/tasks` e `/api/me/time/entries` também 200 autenticadas. Credenciais em `.env.qa.login`, senha nunca exibida, arquivos privados QA modo 600.

Operação deste checkout (sem alterar os scripts da main para outros lotes): `PRISMA_SERVER_IMAGE=prisma-qa-app:time-tracking-20261015 docker compose --project-directory /home/agentes/projetos/prisma -p prisma-qa --env-file .env.server-qa -f .runlogs/time-fixes/compose-qa.yaml up --no-build --pull never --no-deps -d --wait app`. O Compose local ignorado reutiliza a parametrização já existente na branch L07; não mexe no SQL nem na configuração de prisma-dev. Para parar somente a UI/API QA, o mesmo comando de Compose com `stop app`.

## Conferência da imagem final em 3100

Imagem imutável idêntica à QA, fonte funcional d7758c1. Após reset próprio pelo SQL versionado (52,36 s, 28 migrations/um seed), `npm run e2e -- time-tracking-fixes.spec.ts` passou novamente: **9/9**, zero retries, 163.88 s. A réplica temporária da suíte foi encerrada antes desta rodada; casos executados contra API real em 3100.

| Caso final | Desktop | Mobile |
| --- | --- | --- |
| Timer após reload | 28.42 s | 20.20 s |
| Editar e persistir | 17.32 s | 18.21 s |
| Excluir/cancelar | 13.37 s | 15.32 s |
| Buscar nome/número | 18.92 s | 13.11 s |

## Arquivos alterados

- `NOTAS.md`
- `PROGRESS.md`
- `docs/time-tracking-fixes-validation.md`
- `specs/time-tracking.md`
- `src/Prisma.Workspace.Api/Controllers/MeTimeController.cs`
- `src/Prisma.Workspace.Api/Controllers/TimeEntriesController.cs`
- `src/Prisma.Workspace.Application/Features/Me/Dtos/MeTaskDto.cs`
- `src/Prisma.Workspace.Application/Features/Me/Queries/GetMyTasksQueryHandler.cs`
- `src/Prisma.Workspace.Application/Features/MeTime/Queries/GetMyDailyByTaskQuery.cs`
- `src/Prisma.Workspace.Application/Features/MeTime/Queries/GetMyDailyByTaskQueryHandler.cs`
- `src/Prisma.Workspace.Application/Features/MeTime/Queries/GetMyDailyEntriesQuery.cs`
- `src/Prisma.Workspace.Application/Features/TimeEntries/Commands/DeleteTimeEntryCommand.cs`
- `src/Prisma.Workspace.Application/Features/TimeEntries/Commands/UpdateTimeEntryCommand.cs`
- `src/Prisma.Workspace.Application/Features/TimeEntries/Dtos/TimeEntryDto.cs`
- `src/Prisma.Workspace.Application/Features/TimeEntries/Queries/GetMyWeeklyTimeQueryHandler.cs`
- `src/Prisma.Workspace.Application/Features/TimeEntries/TimeEntryMapper.cs`
- `src/Prisma.Workspace.Application/Features/TimeEntries/TimeTrackingDay.cs`
- `src/Prisma.Workspace.Application/Features/WorkItems/Dtos/WorkItemDto.cs`
- `src/Prisma.Workspace.Application/Features/WorkItems/Queries/GetSubItemsQueryHandler.cs`
- `src/Prisma.Workspace.Application/Features/WorkItems/Queries/WorkItemCardProjection.cs`
- `src/Prisma.Workspace.Application/Interfaces/ITimeEntryRepository.cs`
- `src/Prisma.Workspace.Domain/Entities/TimeEntry.cs`
- `src/Prisma.Workspace.Infrastructure/Repositories/TimeEntryRepository.cs`
- `src/Prisma.Workspace.Web/e2e/README.md`
- `src/Prisma.Workspace.Web/e2e/fixtures/boards.ts`
- `src/Prisma.Workspace.Web/e2e/fixtures/functional.ts`
- `src/Prisma.Workspace.Web/e2e/fixtures/time.ts`
- `src/Prisma.Workspace.Web/e2e/time-tracking-fixes.spec.ts`
- `src/Prisma.Workspace.Web/src/features/task/TaskTimePanel.test.tsx`
- `src/Prisma.Workspace.Web/src/features/time/TimeAdjustment.test.tsx`
- `src/Prisma.Workspace.Web/src/features/time/TimeAdjustment.tsx`
- `src/Prisma.Workspace.Web/src/pages/Kanban.tsx`
- `src/Prisma.Workspace.Web/src/pages/MinhasTarefas.test.tsx`
- `src/Prisma.Workspace.Web/src/services/api.ts`
- `src/Prisma.Workspace.Web/src/utils/liveTime.test.ts`
- `src/Prisma.Workspace.Web/src/utils/liveTime.ts`
- `tests/Prisma.Workspace.Tests/TimeTrackingCorrectionTests.cs`
