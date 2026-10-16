# Testes E2E (Playwright)

Testes end-to-end do frontend com Playwright.

## Pre-requisitos

### Opção A: Usar banco de dados dedicado (recomendado)

1. Crie o banco E2E:
   ```powershell
   .\scripts\setup-e2e-database.ps1
   ```
   Este script irá:
   - Criar o banco `DetranKanban_E2E` no SQL Server
   - Aplicar todas as migrations do EF Core
   - Configurar arquivos de conexão (`.env.e2e.connection` e `.env.e2e.seedpassword`)

2. Inicie a API com banco E2E:
   ```powershell
   .\scripts\run-api-e2e.ps1
   ```

3. Inicie o frontend:
   ```bash
   cd src/Prisma.Workspace.Web
   npm run dev -- --mode e2e --host 127.0.0.1 --port 5450
   ```

## Protecao contra banco incorreto

Os scripts E2E aceitam exclusivamente bancos cujo nome comece com `DetranKanban_E2E` ou `DetranKanban_MigrationTest_`. Bancos de desenvolvimento e produção, incluindo `DetranKanban_Dev` e `DetranKanban`, são recusados antes de operações mutáveis ou da inicialização da API.

A opção de executar a suíte automatizada contra `DetranKanban_Dev` é proibida. Nunca aponte estes scripts para produção.

O setup padrão usa `DetranKanban_E2E`. Embora o guard reconheça o prefixo de migration test, `setup-e2e-database.ps1` aborta para `DetranKanban_MigrationTest_*`, pois o SQL de criação atual é exclusivo do banco E2E padrão.

## Configuracao

1. Copie o arquivo de variaveis de ambiente:

   cp .env.e2e .env.e2e.local

2. Edite .env.e2e.local e preencha as credenciais de teste:

   E2E_TEST_USER_EMAIL=admin@prisma.example.invalid
   E2E_TEST_USER_PASSWORD=sua-senha-aqui

   NUNCA commite .env.e2e.local.

## Reset do banco entre execuções

O reset completo validado recria o banco E2E padrão:

```powershell
.\scripts\setup-e2e-database.ps1
```

Não invoque `e2e-reset-database.sql` diretamente. Execuções SQL manuais contornam o guard obrigatório dos scripts PowerShell.

## Execucao

Com a API e o frontend rodando:

  npm run e2e           # Executa todos os testes (Chromium, headless)
  npm run e2e:ui        # Abre a interface interativa do Playwright
  npm run e2e:debug     # Modo debug com inspector

  npm run e2e:report    # Abre o relatorio HTML da ultima execucao

## Variaveis de ambiente

| Variavel               | Descricao                         | Padrao                  |
|------------------------|-----------------------------------|-------------------------|
| E2E_BASE_URL           | URL do frontend                   | http://localhost:5450   |
| E2E_API_URL            | URL da API                        | http://localhost:5400   |
| E2E_TEST_USER_EMAIL    | Email do usuario de teste         | (obrigatorio)           |
| E2E_TEST_USER_PASSWORD | Senha do usuario de teste         | (obrigatorio)           |

## Estrutura

  e2e/
    .auth/              Storage state (gitignored)
    fixtures/           Fixtures e setup de autenticacao
    smoke.spec.ts       Smoke tests (login, dashboard, logout)
    README.md           Este arquivo

## Convenções

- Locators por role, label ou data-testid estavel. Nunca por CSS fragil.
- Sem sleeps fixos (page.waitForTimeout). Use waitForLoadState ou expect com retry.
- Nunca execute contra producao.
- Chromium em cada PR. Firefox/WebKit em execucao noturna ou pre-release.

## Correção de horas (tarefa 7, 2026-10-15)

`time-tracking-fixes.spec.ts` cobre timer após reload, edição com persistência e atualização diária/semanal, exclusão com confirmação/cancelamento e busca por título/número com teclado, ordem de atribuição e exclusão de arquivadas. Usa projeto, usuário, quadro, coluna e tarefas UUID próprios via `fixtures/time.ts` e `boards.ts`, copiados seletivamente de L08 sem integrar a branch. A limpeza no finally da fixture encerra o timer, desativa o membro e arquiva o projeto. Nenhuma entidade de negócio do seed é usada.

Executar `npm run e2e -- time-tracking-fixes.spec.ts` contra o ambiente dedicado configurado em `.env.e2e.local`, respeitando as reservas da tarefa. Não usar a instância reservada para demonstração. A fixture funcional usa CSS de fallback somente para a fonte externa indisponível no servidor; API, scripts, ativos e persistência são reais. Datas do editor são de Brasília. O teste aguarda tempo real por polling do timestamp, sem sleeps.

## Limpeza das factories

Importe `test` de `fixtures/test.ts` ou `fixtures/functional.ts` e passe a fixture `request` para as factories. `cleanupFor` registra ações na pilha do teste; não passe `page.request`. A execução em ordem inversa encerra cronômetros e justificativas antes de remover membros e arquivar projetos.

A limpeza roda no `finally` da fixture de requisição, com prazo próprio de 20 s e o contexto HTTP preservado; o corpo do cenário continua com o limite padrão de 30 s. Falhas de limpeza reprovam o teste e aparecem separadas do erro funcional. É o mecanismo de [teardown e timeout de fixture do Playwright](https://playwright.dev/docs/test-fixtures#fixture-timeout).

Os erros de transporte omitem o call log que pode incluir cabeçalhos de autenticação. Não imprima tokens, cookies ou senhas de teste, nem versione estado autenticado ou traces com credenciais.
