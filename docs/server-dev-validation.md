# Validação do ambiente Linux — 2026-10-13

Base: `feat/ai-assistant-foundation`, commit `faed2ef7dba8620c14ba75f94bec8bcc0155dc98`.
Entrega: `chore/server-dev-env`. Somente infraestrutura de desenvolvimento e documentação.
SDK instalado: 8.0.131 (Ubuntu); Node: 22.23.3. Docker Compose e SQL Server 2022 Developer.

Tempos de parede medidos com `/usr/bin/time`, incluindo arranque de cada comando. Algumas
verificações ocorreram simultaneamente; não são benchmarks exclusivos da máquina.
Logs completos locais em `.runlogs/server-dev/` (ignorados pelo Git).

| Comando | Resultado real | Tempo |
| --- | --- | --- |
| `dotnet restore Prisma.Workspace.sln` | Passou, 5 projetos | 15,40 s |
| `dotnet build Prisma.Workspace.sln --no-restore` | Passou, 0 erros, 3 avisos | 89,93 s |
| `dotnet test tests/Prisma.Workspace.Tests --no-build --no-restore` via `scripts/test-server-dev.sh` | Passou, 311/311, 0 skips | 46,72 s |
| `npx tsc -b` | Passou, exit 0 | 141,20 s |
| `npm run lint` | Passou, 0 erros; 12 avisos preexistentes | 1,56 s |
| `npm test` no frontend | Passou, 145/145, 39 arquivos | 335,50 s |
| `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` no frontend | Falhou, 144 aprovados e 1 falha, 39 arquivos | 350,26 s |
| `npm run build` no frontend | Passou, exit 0; aviso de anotação PURE de dependência | 145,76 s |
| `npm test --prefix tools/context-explorer` | Falhou, 39 aprovados e 3 falhas de 42 | 2,19 s |
| `python3 -m unittest discover -s scripts/tests` | Passou, 8/8 | 0,50 s |
| `python3 infra/ai-bridge/test_bridge.py` | Passou, 17/17 | 0,49 s |
| HTTP: `GET /health`, `GET /`, `POST /api/auth/login` por urllib | Passou, 3 respostas 200; token omitido | 2,15 s |
| `docker compose -p prisma-dev --env-file .env.server-dev -f compose.server-dev.yaml build app` | Passou, imagem `prisma-dev-app:local` | 354,96 s |
| `bash -n scripts/{setup-e2e-database,run-api-e2e,run-local,test-server-dev}.sh` | Passou, 4 scripts | 0,02 s |
| `scripts/run-local.sh down` | Passou, removeu somente containers/rede prisma-dev, preservou volumes | 17,21 s |
| `scripts/run-local.sh up` | Passou, app e SQL saudáveis após reconstrução | 210,43 s |
| Smoke de `scripts/run-api-e2e.sh` | Passou, GET /health 200 em 3101; encerrou somente processo temporário | 45,89 s |
| `npm run e2e` | Falhou: 118 aprovados, 1 falha mobile, 6 skips; desktop/mobile headless, 0 retries | 1.026,69 s |

Após o ciclo down/up, HTTP foi repetido: saúde, SPA e login devolveram 200 em 1,51 s.
As portas 3101/3103/3104 ficaram livres; permanecem apenas app 3100 e SQL privado 3102.

## Falhas preservadas

Context Explorer:

- `artifacts.test.js:54`, “agents: 4 agents present”: esperado 4, recebido 5.
- `model.test.js:28`, “artifactsSummary reflete contagens canônicas”: esperado 4, recebido 5.
- `model.test.js:110`, “model expõe contratos reais de agentes sem fallback”: o conjunto real inclui `qa`.

Vitest no pool de threads: `OrganizationSettings.test.tsx:58`, “permite ao gestor de membros
editar o nome funcional sem perder o e-mail”: `findByRole('textbox', { name: 'Nome de exibição de Maria Antiga' })`
não encontrou o campo no prazo. A suíte padrão passou integralmente; não houve correção,
remoção ou alteração do teste e não se declara a execução de threads aprovada.

A primeira execução xUnit, em schema sem seed, teve 310 aprovações e uma falha (39,09 s):
`StageToProjectBackfillTests.Stages_HaveProjectId_OrBackfillCopiesFromBoard` exige ao menos
uma Stage. Corrigida somente a preparação para usar um banco novo com o seed oficial.
A execução final passou com 311/311. O banco inicial `Prisma_BoardColumns_ServerDev` foi preservado.

A primeira rodada E2E foi interrompida para corrigir conectividade da API em bridge para
fixture no host: 12 aprovados, 2 falhas, 1 interrompido, 1 skip e 109 não executados
(176,42 s, exit 130). Falhas: timeout de beforeAll em IA e `board-stage-sync.spec.ts:45`
esperando “Concluído” e recebendo “Em andamento”. A interrupção atingiu
`independent-board-columns.spec.ts`. Seus logs/artefatos permanecem em
`.runlogs/server-dev/e2e-initial-*`. Apenas a rede do app local foi ajustada para host,
com bind explícito na porta 3100; o fixture tornou-se acessível em localhost:3103.
O banco exclusivo DetranKanban_E2E foi recriado usando o SQL já versionado antes da rodada final.

E2E final: `project-assignment-access.spec.ts:49:20`, somente Chromium mobile.
`Test timeout of 30000ms exceeded` e `expect(task?.id).toBeTruthy()` recebeu `undefined`
após consultar `/api/WorkItems/project/${project.id}` e selecionar `tasks.body[0]`.
O mesmo cenário passou no desktop. Nenhuma alteração em produto, spec ou testes para
fazer passar; a falha não foi repetida para ocultar o resultado. A falha inicial de
sincronização de status passou nos dois projetos na rodada final. O timeout de IA foi
resolvido apenas pela rede local; todos os 16 cenários IA passaram.

Resultado final desktop: 59 aprovados, 3 skips; mobile: 58 aprovados, 1 falha, 3 skips;
setup: 1 aprovado. Total 125, sem retries. Artefatos finais em
`src/Prisma.Workspace.Web/test-results` e `playwright-report`; detalhes em
`.runlogs/server-dev/e2e.log` e `e2e.time`. Os seis skips são condições existentes:
mobile-only A11y/Kanban ignorados no desktop, setup real desabilitado nos dois projetos
e dois cenários de concorrência/cadastro ignorados no mobile.

## Limitações e operação

O fluxo PowerShell original depende de comandos Windows (`npm.cmd`, `Start-Process`,
`Test-NetConnection`); os equivalentes Linux novos atendem o ambiente do servidor.
`tools/context-explorer` tem script `dev` com `start /B`; o servidor pode ser iniciado por
`node server.js` e o frontend separadamente. Dependências das duas pastas foram instaladas.
Não foi aberto o painel nem efetuada aprovação humana.

Não foi encontrada suíte HTTP separada. `Prisma.Workspace.Api.http` é um exemplo de requisição
`/weatherforecast`, não uma suíte automatizada; os endpoints reais foram validados por smoke
HTTP e pelo Playwright. Não foram usados navegador/logins do PC, inferência paga, produção,
credenciais de produção, ai-memory ou outros agentes. Firefox e WebKit foram instalados,
mas a configuração E2E versionada executa Chromium desktop e mobile.

Operação, credenciais e isolamento: [server-dev.md](server-dev.md).
