# Baseline do primeiro snapshot privado

**Data:** 2026-09-05
**Objetivo:** comprovar que a extração por allowlist preservou uma base compilável antes do rename e da instalação.

## Resultado

| Verificação | Resultado |
|---|---|
| Gitleaks no snapshot | 0 achados |
| Denylist de caminhos/arquivos | aprovada |
| .NET restore | aprovado |
| .NET build | aprovado, 0 erros e 2 avisos legados de nulabilidade |
| Testes .NET | 105/105 |
| Frontend `npm ci` | aprovado |
| Frontend build | aprovado |
| Frontend Vitest | 46/46 em 21 arquivos |
| Frontend lint | 0 erros e 8 avisos legados |
| Context Explorer | 42/42 |

## Observações

- O Vitest foi executado com `--pool=threads --maxWorkers=1 --fileParallelism=false`; o pool padrão ficou sem
  progresso por vários minutos neste Windows, enquanto a execução em threads terminou sem falhas.
- O build exibe dois avisos de anotação originados de SignalR/Rolldown, sem impedir a geração dos bundles.
- `npm audit` encontrou 33 vulnerabilidades conhecidas: 28 moderadas, 5 altas e 0 críticas. Os pacotes diretos
  envolvidos incluem `react-router-dom`, `@tiptap/react` e `@tiptap/extension-image`. Atualizações serão analisadas
  em lote próprio, com testes; `npm audit fix` não foi aplicado automaticamente.
- E2E não foi executado nesta etapa porque nenhum comportamento de produto foi alterado: o trabalho foi extração,
  documentação e governança. A primeira mudança funcional/técnica do snapshot volta a exigir o gate E2E normal.

## Comandos reproduzíveis

```powershell
.\scripts\audit-public-snapshot.ps1
dotnet restore Prisma.Workspace.sln
dotnet build Prisma.Workspace.sln --no-restore
dotnet test tests/Prisma.Workspace.Tests/ --no-build

Set-Location src/Prisma.Workspace.Web
npm ci
npm run build
npx vitest run --pool=threads --maxWorkers=1 --fileParallelism=false
npm run lint

Set-Location ../../../tools/context-explorer
npm test
```
