# L06 — quadros e colunas

Tarefa 4 do Maestro, em 2026-10-14. Branch `test/e2e-l06`, base `origin/main` / `2c3816c`. Somente testes novos, helper e documentação; produto, specs, migrations e specs E2E existentes intactos.

## Casos

Todos em `src/Prisma.Workspace.Web/e2e/boards-columns-l06.spec.ts`, executados em Chromium desktop/mobile.

| Linha | O que prova |
| --- | --- |
| 29 | Criar quadro pela UI com cópia das colunas próprias: reload conserva ID/nome/projeto; IDs das colunas novos, nomes copiados e nenhuma tarefa copiada. |
| 61 | Cancelar exclusão de quadro conserva tarefas e versões; excluir ocupado sem destino recebe 400; confirmar com quadro/coluna do mesmo projeto elimina a origem e persiste os mesmos itens no destino após reload. |
| 102 | Criar coluna pela UI, conferir categoria operacional e persistência; renomear e recarregar; cancelar confirmação de exclusão vazia preserva a coluna; confirmar elimina a coluna operacional após reload. |
| 137 | A10/M17: coluna com duas tarefas exige destino, informa contagem e nome do destino; cancelar preserva IDs/estado/versões; confirmar transfere os mesmos itens sem perda, após reload. |
| 171 | A11/D62: duas tarefas na coluna, filho/neto fora dela; preview informa contagens, exige consentimento separado para descendência, cancelar preserva estado/versões. Confirmar conclui todos sem mover descendentes, gera evento por item; reabrir também exige consentimento/cancelamento e persiste os itens da coluna como abertos. |
| 239 | Leitor com vínculo explícito de leitura: criação/exclusão diretas e tentativas pela UI recebem 404 do guard de papel; reload e GET administrativo confirmam que nenhuma coluna foi criada/removida. |

Helper `e2e/fixtures/boards.ts`: factories de projeto/quadro/coluna/item/leitor com randomUUID; sessão administrativa capturada para a limpeza não depender da sessão de leitor. `withBoardFactory` garante finally; remove vínculo de leitor, desativa membership e arquiva apenas o projeto criado. Verifica que o projeto deixou a lista ativa. Não busca projetos/quadros/tarefas do seed ou de outros specs; apenas reutiliza o login administrativo padrão da suíte. Arquivamento preserva dados/auditoria, e a conta Identity do leitor permanece com membership inativa porque não há endpoint de exclusão de conta neste fluxo.

Sem waitForTimeout, sleeps ou asserções condicionais. Respostas HTTP capturadas antes da ação; confirm/alert nativos resolvidos durante o click, conforme [documentação Playwright](https://github.com/microsoft/playwright/blob/main/docs/src/dialogs.md). Precondições criadas explicitamente por API; GETs verificam status antes do JSON. Cancelamentos comparam snapshots de estado/versão.

## Contratos, dúvidas e sugestões

D89 / SPEC-INDEPENDENT-BOARD-COLUMNS aprovada define transferência explícita ao excluir quadro/coluna ocupados e substitui as regras anteriores de quadro como visão. D62 define confirmação nos dois sentidos, consentimento recursivo e evento por tarefa. D90 respeitada pela criação de itens com responsável que já administra o projeto.

Dúvida: D62 obriga reabrir tarefas na coluna, mas não explicita se reabrir deve alcançar descendentes que foram concluídos em outra coluna. O teste registra o comportamento atual: permanecem concluídos e na coluna original. Não foi inventada nova regra nem alterada spec.

Sugestões observadas, sem implementação: ocultar/desabilitar ações de quadro/coluna para leitor (a API bloqueia, mas controles continuam disponíveis); associar label ao seletor de quadro de destino e dar semântica de diálogo aos modais; mostrar contagem/impacto no modal de exclusão de quadro e preservar mensagem específica da API na falha de criação de coluna, que hoje vira alert genérico.

Nenhum defeito funcional de produto encontrado nos seis casos nas repetições e na suíte completa; nenhum test.fail ou skip adicionado. A presença dos controles para leitor e os pontos de acessibilidade são registrados acima como sugestões de UX; o teste de negação verifica a impossibilidade de persistir alterações pela UI/API, sem inventar regra de ocultação no contrato vigente D89.

## Verificações

Tempos de parede com /usr/bin/time. Comandos frontend em src/Prisma.Workspace.Web. Logs/artefatos locais ignorados em `.runlogs/e2e-l06` e diretórios Playwright.

| Comando | Resultado | Tempo |
| --- | --- | --- |
| `npx tsc -b` | passou, exit 0 | 177,07 s |
| `npm run lint` | passou, 0 erros / 12 avisos anteriores | 1,72 s |
| `npx tsc --ignoreConfig --target ES2022 --module ESNext --moduleResolution bundler --strict --skipLibCheck --types node --outDir ../../.runlogs/e2e-l06/typecheck e2e/boards-columns-l06.spec.ts e2e/fixtures/boards.ts` (adicional) | passou, tipos dos arquivos E2E | 11,08 s |
| `npm run e2e -- e2e/boards-columns-l06.spec.ts --project=chromium-desktop --project=chromium-mobile --repeat-each=3` | 37/37 (setup + 36 execuções), zero retries; mesmo banco | 647,65 s |
| `bash .runlogs/e2e-l06/reset-clean.sh` | banco recriado com SQL versionado; 28 migrations existentes e um projeto seed; app saudável | 41,42 s |
| `npm run e2e` | 131 aprovados / 6 skips existentes, zero retries; banco limpo | 853,25 s |

O reset local verifica o nome DetranKanban_E2E e o rótulo prisma-dev do container antes de parar apenas o app, executar e2e-create-database.sql no SQL próprio e reiniciar. Não usa e2e-reset-database.sql. Produto no app existente é idêntico ao src da main/base desta tarefa; não houve reconstrução de imagem sem mudança de produto.

A primeira rodada de construção dos testes teve 6 aprovações (incluindo setup) e uma falha de expectativa no teste de negação: esperava 403, recebeu 404. Leitura de ProjectAccessService confirmou mascaramento de recurso por falta de papel; corrigida expectativa para 404, mantendo prova de leitura pela UI e de ausência de gravação. O focal de negação subsequente passou. Não é defeito de produto. Verificação adicional de tipos dos arquivos E2E inicialmente pediu --ignoreConfig no TypeScript 6; comando corrigido conforme diagnóstico da CLI, sem mudar configuração versionada.

## Limites e continuidade

Sem backend/xUnit/Vitest/build da SPA repetidos, porque não houve alteração nessas áreas; sem Firefox/WebKit ou QA visual humano. Sem produção, PR, merge, deploy, segredos no Git ou criação de agentes. Próximo passo: Maestro revisar L06 e encaminhar L07; aceitar/decidir as observações de contrato/UX separadamente.
