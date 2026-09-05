# Como contribuir

Obrigado por ajudar a construir o Prisma WorkSpace. O projeto ainda está em preparação privada; estas regras já
definem o fluxo que será usado pela Community Edition.

## Antes de começar

1. Procure uma issue existente e confirme que a mudança pertence ao roadmap.
2. Para comportamento novo, crie ou vincule uma história em `stories/` e uma spec aprovada em `specs/`.
3. Mudanças de arquitetura, schema, workflow ou histórico precisam respeitar `AGENTS.md` e `DECISIONS.md`.
4. Nunca inclua credenciais, dados reais, storage state do navegador ou configuração de produção.

## Ambiente

O caminho mais curto é o [quickstart Docker](docs/installation/docker.md). Para desenvolvimento sem containers da
aplicação, instale .NET 8 SDK, Node.js 20 e uma instância SQL Server compatível.

## Pull requests

- mantenha o escopo pequeno e explique motivação, comportamento e riscos;
- inclua testes para mudanças de comportamento;
- atualize documentação, spec e `PROGRESS.md` quando aplicável;
- execute os mesmos gates do CI antes de enviar;
- não misture formatação ou refatorações não relacionadas.

Comandos principais:

```bash
dotnet restore Prisma.Workspace.sln
dotnet build Prisma.Workspace.sln --configuration Release --no-restore
dotnet test Prisma.Workspace.sln --configuration Release --no-build
cd src/Prisma.Workspace.Web
npm ci
npm run lint
npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false
npm run build
```

Mudanças em frontend, endpoints ou schema também devem cumprir o gate E2E descrito em `AGENTS.md`.

## Segurança e conduta

Não abra uma issue pública para vulnerabilidades. Siga [SECURITY.md](SECURITY.md). Ao participar, mantenha comunicação
respeitosa, técnica e acolhedora; um código de conduta formal será escolhido antes da abertura pública.
