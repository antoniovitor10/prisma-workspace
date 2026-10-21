---
name: testes
description: Como os testes funcionam no Prisma WorkSpace - xUnit (com SQL Server opt-in), Vitest com Testing Library, Playwright E2E desktop e mobile, governança do Context Explorer, testes Python e do MCP; onde ficam, como rodar tudo ou um arquivo, como subir o ambiente E2E no servidor, seletores por papel e rótulo, massa de dados, dublês de API e testes instáveis conhecidos. Use em toda task que escreve, roda ou avalia testes, e antes de dar uma task como pronta.
metadata:
  origem: projeto
  versao: "1"
  revisar_em: "2026-12-31"
---

# Testes no Prisma WorkSpace

O "o que testar" é do método (`metodo/nucleo/referencias/testes.md`). Aqui fica o "como" deste repositório.

## Regras

- **Vermelho é diagnóstico** (princípio 8): nunca afrouxar asserção, timeout, retry, nem pular teste para ficar verde.
- **Typecheck é `npx tsc -b`** em `src/Prisma.Workspace.Web` (princípio 7). O `tsconfig.json` da raiz tem `"files": []` e só referências de projeto: `tsc --noEmit` resolve a configuração vazia e sai 0 sem olhar `src/`. Já deixou passar um `TS2554` (5 argumentos numa função de 4) que fez a coluna criada pelo Kanban nascer com a categoria errada.
- **E2E obrigatório** (princípio 9) em toda task que muda frontend (componente, rota, lógica de UI), endpoint (controller, DTO, validação, regra) ou schema (migration, entidade, relação): os E2E afetados passam em `chromium-desktop` e `chromium-mobile`, e todo CA com interface tem um teste de interação novo ou ajustado. Não exigem E2E: só documentação, CI sem efeito no comportamento, refatoração interna sem mudança de interface pública, correção de lint ou formatação.
- **Seletores por papel e rótulo acessível** (`getByRole`, `getByLabel`), como o resto da suíte (cerca de 290 `getByRole` e 90 `getByLabel` contra 8 `getByTestId`). `data-testid` só quando não há papel ou rótulo estável; nunca seletor CSS frágil. Sem `waitForTimeout`.
- **No servidor, todo Playwright roda dentro de** `flock /home/agentes/projetos/.trava-navegador <comando>`, e execução pesada (E2E inteiro, xUnit inteiro, build de imagem) só depois de `uptime` com carga de 1 minuto até 14 (skill `operacao-agentes`).
- **Nunca contra produção** nem contra os ambientes `prisma-dev` e `prisma-metodo`. Os scripts E2E só aceitam banco `DetranKanban_E2E*` ou `DetranKanban_MigrationTest_*` (`src/Prisma.Workspace.Web/e2e/README.md:29-35`).
- **Linha de base** em `metodo/projeto/config.yaml` (`linha_de_base`): nenhuma task cria falha nova nem piora uma existente.
- **Enquanto o legado existir** (até o PR de remoção), qualquer mudança em `specs/`, nos blocos YAML do `backlog.md`, em `agents/`, `workflows/`, `profiles/`, `context/` ou `tools/agent-loop` quebra o job Context governance, que exige contagens fixas em `tools/context-explorer/lib/canonical.js:9-29` (53 specs, 111 tasks, 16 nós, 21 arestas, 7 gates, 11 estados, 47 testes do loop) e 19 domínios e 5 agentes. Trabalho novo só em `metodo/` e `docs/`, que o job não lê.

## Ferramentas, onde ficam e como rodar

| Tipo | Onde | Tudo | Um arquivo | No CI |
|---|---|---|---|---|
| xUnit 2.9.3 | `tests/Prisma.Workspace.Tests` (62 arquivos) | `dotnet test Prisma.Workspace.sln -c Release` | `dotnet test --filter FullyQualifiedName~UpdateStageCommandHandlerTests` | **não** (só build) |
| Vitest 4 + Testing Library, jsdom | `src/Prisma.Workspace.Web/src/**/*.test.ts(x)` (41 arquivos) | `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` | `npx vitest run src/layout/Topbar.test.tsx` | sim |
| Playwright 1.62 | `src/Prisma.Workspace.Web/e2e/*.spec.ts` (30 arquivos) | `bash scripts/ci-e2e.sh <imagem>` ou `npm run e2e` | `bash scripts/ci-e2e.sh <imagem> e2e/board-stage-sync.spec.ts` ou `npx playwright test e2e/board-stage-sync.spec.ts` | sim, contra a imagem |
| Governança | `tools/context-explorer/test` (42) | `cd tools/context-explorer && npm test` | `node --test test/<arquivo>.js` | sim |
| MCP | `tools/prisma-mcp/test` | `cd tools/prisma-mcp && npm ci && npm test` | | não |
| Python | `scripts/tests`, `infra/ai-bridge/test_bridge.py` | `python3 -m unittest discover -s scripts/tests` | | sim (job Container) |

Configuração do Vitest em `src/Prisma.Workspace.Web/vitest.config.ts:6-8` (jsdom, `src/test/setup.ts` com `jest-dom`). Configuração do Playwright em `src/Prisma.Workspace.Web/playwright.config.ts`: `workers: 1` e `fullyParallel: false` (linhas 23-28, a conta E2E é compartilhada e a concorrência dispara o rate limiter), `retries: 2` só no CI (linha 25), projetos `setup`, `chromium-desktop` (Desktop Chrome, 1280x720) e `chromium-mobile` (Pixel 5, 393x851) nas linhas 41-64.

## Ambiente do E2E

- **Caminho recomendado para agente no servidor:** construir a imagem com nome próprio e rodar o script do CI, que sobe SQL Server, app e navegador numa rede Docker descartável, sem porta no host, e limpa tudo no fim (`scripts/ci-e2e.sh:12-21`):

  ```
  docker build -t prisma-<agente>-e2e:<sha> .
  flock /home/agentes/projetos/.trava-navegador bash scripts/ci-e2e.sh prisma-<agente>-e2e:<sha> [e2e/arquivo.spec.ts]
  docker rmi prisma-<agente>-e2e:<sha>
  ```

- **No PC:** API com banco E2E (`scripts/run-api-e2e.ps1` ou `.sh`), `npm run dev` em `src/Prisma.Workspace.Web` e `npm run e2e`. Banco criado por `scripts/setup-e2e-database.*`; credenciais em `src/Prisma.Workspace.Web/.env.e2e.local` (nunca ler nem versionar; o `playwright.config.ts:5-12` carrega o arquivo).
- **Integração Git do método:** os 17 testes de `e2e/method-git-phase2a.spec.ts` (34 execuções) só rodam com `E2E_METHOD_GIT=1` e o GitHub falso (`e2e/fixtures/github-fake.mjs`); sem isso são pulados (linha 15).

## Massa de dados e usuários

- O E2E usa o seed demonstrativo (`Seed__DemoEnabled=true`) com a conta E2E (`E2E_TEST_USER_EMAIL` e `E2E_TEST_USER_PASSWORD`, gerada pelo script). O login acontece uma vez em `e2e/fixtures/auth.setup.ts` e fica em `e2e/.auth/user.json` (arquivo proibido de ler e versionar).
- `e2e/fixtures/test.ts:36-62` dá `authenticatedGoto` e `resolveSeedProject` (projeto seed `INFRACOES` ou o primeiro com quadro); `authenticatedApiGet` (linha 20) lê a API com a sessão do navegador.
- xUnit com SQL Server real é opt-in: os testes da integração Git exigem `PRISMA_METHOD_GIT_SQL` e se pulam sem ela (`tests/Prisma.Workspace.Tests/GitDeliverySqlTests.cs:18-27`); `BoardStructureSqlTests` usa `ConnectionStrings__DefaultConnection` e recusa banco que não comece com `Prisma_BoardColumns_` (linhas 298-300).

## Dublês

- Backend: repositório em memória dentro do próprio teste (`tests/Prisma.Workspace.Tests/UpdateStageCommandHandlerTests.cs:11`), sem framework de mock.
- Frontend: `vi.spyOn(api, '<método>').mockResolvedValue(...)` no cliente único e `vi.mock` de componentes pesados (`src/Prisma.Workspace.Web/src/layout/Topbar.test.tsx:10-28`).
- E2E: GitHub falso em `e2e/fixtures/github-fake.mjs`; provedor de IA falso alcançado pela rede do ambiente (D98).

## Um exemplo real de cada tipo

- **Unitário (xUnit):** `tests/Prisma.Workspace.Tests/UpdateStageCommandHandlerTests.cs:52-79`, handler com repositório em memória.
- **Integração (xUnit + SQL Server):** `tests/Prisma.Workspace.Tests/BoardStructureSqlTests.cs:14-29`, falha no meio da exclusão do quadro desfaz tarefa, coluna e histórico.
- **Componente (Vitest):** `src/Prisma.Workspace.Web/src/layout/Topbar.test.tsx`, menu da conta e "Nova organização" só para quem pode criar.
- **Interação (Playwright):** `src/Prisma.Workspace.Web/e2e/board-stage-sync.spec.ts:12-40`, concluir pela gaveta move o card e sobrevive ao recarregamento, por papel e rótulo.
- **Governança:** `tools/context-explorer/test/*.js` contra `lib/canonical.js`.

## Testes lentos ou instáveis conhecidos

- No CI da `a2797a7` (run 39098740642): 159 execuções E2E, 119 passaram, 40 puladas (34 do método Git, mais casos só de um dos projetos), 0 falhas. No servidor, sob carga e com dados compartilhados, já falharam por tempo: `rich-task-description` no mobile (reabrir o detalhe), `board-stage-sync` e `ai-assistant` (linhas 149, 221 e 340). Falha nova nesses arquivos é diagnóstico, não instabilidade presumida.
- Vitest completo leva cerca de 5 minutos em série; xUnit completo, alguns minutos com SQL. Última execução completa registrada do xUnit: 715 de 720 em 2026-10-16.
- Larguras do método (`responsivo-desktop-mobile`: 1440x900, 1366x768, 390x844) valem para capturas de QA; os projetos do Playwright continuam Desktop Chrome e Pixel 5.

## Origem das regras

- `AGENTS.md` antigo (Gate de tipos e Verificação final E2E), D39, D40, D78, D98.
- `.github/workflows/ci.yml`, `docs/maintenance/continuous-integration.md`, `src/Prisma.Workspace.Web/e2e/README.md`, arquivos citados acima.
- Instruções do Vitor aos agentes do servidor (flock, uptime, vermelho é diagnóstico), inventário A1 de 2026-10-21.
