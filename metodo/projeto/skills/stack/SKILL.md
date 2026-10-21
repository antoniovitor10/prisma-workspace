---
name: stack
description: Stack, versões, estrutura de pastas e comandos de restaurar, rodar, testar e fazer build do Prisma WorkSpace (.NET 8, EF Core, SQL Server, React 19, Vite, Styled Components). Use em qualquer task que mexa em código, dependência, Docker, script ou configuração, e antes de rodar o projeto no PC ou no servidor.
metadata:
  origem: projeto
  versao: "1"
  revisar_em: "2027-01-31"
---

# Stack do Prisma WorkSpace

## Regras

- A stack não muda sem ADR aprovado pelo Vitor (princípio 5): biblioteca nova, troca de framework ou de banco é decisão de arquitetura.
- Backend em Clean Architecture com dependência só para dentro: `Domain` não depende de nada; `Application` depende de `Domain`; `Infrastructure` e `Api` dependem de `Application`.
- Frontend tem um único pacote em `src/Prisma.Workspace.Web`; não existe monorepo de pacotes nem ESLint (o lint é o oxlint).
- `tools/prisma-mcp` exige Node 22; o CI usa Node 20 para o frontend e para a governança. No servidor há Node 22.
- Dependência nova de NuGet ou npm: pergunte antes (ADR se for estrutural). As do Dependabot passam pelo CI e pelo Maestro.

## Versões (lidas dos manifestos em 2026-10-21)

| Camada | Versão | Onde |
|---|---|---|
| .NET | `net8.0` | `src/Prisma.Workspace.Api/Prisma.Workspace.Api.csproj:18` |
| EF Core, SqlServer, Identity EF | 8.0.29 | `src/Prisma.Workspace.Infrastructure/Prisma.Workspace.Infrastructure.csproj:9-15` |
| Serilog.AspNetCore | 8.0.3 | idem, linha 16 |
| HtmlSanitizer | 9.0.967 | idem, linha 8 |
| MediatR / FluentValidation.DI | 12.5.0 / 11.12.0 | `src/Prisma.Workspace.Application/Prisma.Workspace.Application.csproj:8-9` |
| SQL Server | 2022 Developer (`mcr.microsoft.com/mssql/server:2022-latest`) | `compose.yaml`, `scripts/ci-e2e.sh` |
| React / TypeScript / Vite | ^19.2.7 / ~6.0.2 / ^8.1.1 | `src/Prisma.Workspace.Web/package.json:41,60,61` |
| Styled Components, TanStack Query, React Router, React Hook Form, Zod | ^6.4.3, ^5.101.2, ^7.18.1, ^7.81.0, ^4.4.3 | `src/Prisma.Workspace.Web/package.json:46,25,44,43,47` |
| Vitest / oxlint / Playwright | ^4.1.10 / ^1.71.0 / ^1.62.1 | `src/Prisma.Workspace.Web/package.json:62,59,50` |
| MCP | `@modelcontextprotocol/sdk` ^1.21, Node >= 22 | `tools/prisma-mcp/package.json:6` |
| Imagem | `node:20-alpine` (build web), `dotnet/sdk:8.0`, `dotnet/aspnet:8.0` | `Dockerfile:2,10,34` |

## Estrutura

```
src/Prisma.Workspace.Domain          entidades, enums, exceções de domínio (sem dependências)
src/Prisma.Workspace.Application     Features/<Area>/{Commands,Queries} (MediatR), DTOs, validators, interfaces
src/Prisma.Workspace.Infrastructure  Persistence (AppDbContext, Configurations, Migrations), Repositories, Identity, Services (Ai, MethodGit)
src/Prisma.Workspace.Api             Controllers, Middleware, Authentication, Realtime (SignalR), Program.cs
src/Prisma.Workspace.Web             SPA: src/{pages,features,components,layout,services,styles}, e2e/
tests/Prisma.Workspace.Tests         xUnit
tools/prisma-mcp                     servidor MCP do rastreador (TypeScript, Node 22)
infra/ai-bridge                      ponte das CLIs de IA (Python + Node), contêiner próprio
apps/landing                         landing estática (fora do build da imagem)
scripts/                             dev, setup Docker, E2E, deploy, auditoria do snapshot, testes Python
```

O `Dockerfile` copia só `src/`: `metodo/`, `docs/`, `tools/` e `apps/` não entram na imagem.

## Comandos

```
dotnet restore Prisma.Workspace.sln
dotnet build Prisma.Workspace.sln -c Release --no-restore
dotnet test Prisma.Workspace.sln -c Release                       # xUnit; fora do CI (skill testes)
cd src/Prisma.Workspace.Web && npm ci
npm run lint && npx tsc -b                                         # nunca tsc --noEmit (princípio 7)
npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false  # Vitest, como no CI
npm run build                                                      # tsc -b && vite build
cd tools/prisma-mcp && npm ci && npm test                          # fora do CI
python3 -m unittest discover -s scripts/tests && python3 infra/ai-bridge/test_bridge.py
```

Como rodar a aplicação:

| Onde | Como | Portas |
|---|---|---|
| PC | `npm run dev` na raiz (`scripts/dev.mjs`, `compose.dev.yaml`; gera `.env.dev`) | web 5450, API 5400 |
| instalação empacotada | `scripts/setup-docker.sh --accept-sql-server-eula` (`compose.yaml`) | 8080 |
| servidor, por agente | ambiente próprio na faixa de portas do agente (skill `operacao-agentes`) | 3140 a 3169 |
| servidor, contra a imagem | `bash scripts/ci-e2e.sh <imagem> [e2e/arquivo.spec.ts]`: SQL, app e navegador em rede Docker própria, sem porta no host | nenhuma |

## Exemplos do projeto

- `src/Prisma.Workspace.Application/ApplicationServiceExtensions.cs:13-26`: registro de handlers, validators e do `ValidationBehavior` por assembly.
- `compose.dev.yaml:28,42`: API em 5400 só no loopback.
- `Dockerfile:2-34`: build em três estágios, frontend embutido na API.

## Armadilhas conhecidas

- Os scripts do servidor (`scripts/run-local.sh`, `setup-e2e-database.sh`, `run-api-e2e.sh`, `test-server-dev.sh`) e o `compose.server-dev.yaml` estão fixos no projeto Docker `prisma-dev`, nas portas 3100/3102 e no `.env.server-dev` (`scripts/setup-e2e-database.sh:12-24`). `prisma-dev` é demonstração e os agentes não o usam. Para ambiente próprio, use `scripts/ci-e2e.sh` com imagem de nome próprio ou parametrize os scripts numa task.
- `tools/context-explorer/web` usa `start /B` no script `dev` (Windows); não roda assim no servidor.
- Os scripts `.ps1` são do PC; no servidor use os `.sh` equivalentes.
- `package.json` usa `^` e `~`: a versão instalada é a do `package-lock.json`.

## Origem das regras

- `AGENTS.md` antigo (seções Stack e Estrutura da solution), D7, D76, D77, D97, D98.
- Manifestos citados acima, `CONTRIBUTING.md`, `.github/workflows/ci.yml`, inventário A1 de 2026-10-21.
