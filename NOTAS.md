# Continuidade — ambiente do servidor

Atualizado em 2026-10-13, por Tear, tarefa 1 do Maestro.

- Branch: `chore/server-dev-env`; base `feat/ai-assistant-foundation` / `faed2ef7dba8620c14ba75f94bec8bcc0155dc98`.
- Desenvolvimento ativo: containers `prisma-dev-app-1` e `prisma-dev-database-1`, saudáveis. App/API em `0.0.0.0:3100`; SQL exclusivo em `127.0.0.1:3102`. App usa rede host para alcançar fixture de testes em localhost. Nenhum container de produção ou de outro projeto alterado.
- Subir/derrubar: `scripts/run-local.sh up` / `scripts/run-local.sh down`. Volumes preservados. Configuração explícita: `docker compose -p prisma-dev --env-file .env.server-dev -f compose.server-dev.yaml ...`.
- Usuário de teste: `admin@prisma.example.invalid`. Senha em `.env.e2e.seedpassword`; configuração local em `.env.server-dev` e `src/Prisma.Workspace.Web/.env.e2e.local`, ignoradas e modo 600. Nunca copiar valores para documentação ou handoff.
- SDK .NET 8 e dependências frontend/Context Explorer instalados. Todos os navegadores Playwright instalados; suíte executa Chromium desktop/mobile.
- Passaram: restore/build, xUnit 311, tsc -b, lint, Vitest padrão 145, build frontend, Python scripts 8, ponte 17, smoke HTTP e ciclo Linux down/up/API. Falharam: Context Explorer 3/42 (contagem/contratos de agentes), Vitest threads 1/145 (OrganizationSettings), E2E mobile 1/125 (project-assignment-access: task?.id undefined e timeout). E2E: 118 aprovações e 6 skips; nenhum retry.
- Preparações iniciais foram corrigidas somente na infraestrutura: seed xUnit e conectividade do fixture IA. Resultados anteriores preservados. Produto, specs, testes e scripts PowerShell intactos.
- Relatório completo: `docs/server-dev-validation.md`; operação: `docs/server-dev.md`. Logs locais: `.runlogs/server-dev`; artefatos E2E finais: `src/Prisma.Workspace.Web/test-results` e `playwright-report`. Os iniciais ficaram em `.runlogs/server-dev/e2e-initial-*`.
- Próximo passo do Maestro: revisar branch e decidir análise das falhas preservadas. Tear não inicia correções fora da tarefa. Nenhuma pendência de implementação desta infraestrutura; sem publicação em produção, PR ou merge.
