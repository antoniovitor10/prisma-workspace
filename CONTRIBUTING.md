# Como contribuir

Obrigado por ajudar a construir o Prisma WorkSpace. O repositório está público e em preparação para a primeira
release da Community Edition; a licença de distribuição permanece pendente.

## Antes de começar

1. Procure uma issue existente e confirme que a mudança cabe no contexto do projeto (`metodo/projeto/contexto.md`).
2. Comportamento novo nasce de uma história (HU) em `docs/trabalho/`, quebrada em tasks; siga o fluxo de `AGENTS.md`.
3. Mudanças de arquitetura, schema, workflow ou histórico respeitam `metodo/projeto/principios.md` e as decisões em `docs/decisoes/`; mudar uma decisão exige um ADR novo.
4. Nunca inclua credenciais, dados reais, storage state do navegador ou configuração de produção.

## Ambiente

O caminho mais curto é o [quickstart Docker](docs/installation/docker.md). Para desenvolvimento sem containers da
aplicação, instale .NET 8 SDK, Node.js 20 e uma instância SQL Server compatível.

## Pull requests

- mantenha o escopo pequeno e explique motivação, comportamento e riscos;
- inclua testes para mudanças de comportamento;
- atualize a documentação e o relatório da task quando aplicável;
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

Mudanças em frontend, endpoints ou schema também passam nos testes E2E afetados (skill `metodo/projeto/skills/testes`).

## Segurança e conduta

Não abra uma issue pública para vulnerabilidades. Siga [SECURITY.md](SECURITY.md). Ao participar, mantenha comunicação
respeitosa, técnica e acolhedora; um código de conduta formal será escolhido antes da abertura pública.
