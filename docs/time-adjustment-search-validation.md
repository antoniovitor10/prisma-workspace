# Validação da busca de tarefas no ajuste de horas

Correção QA-H06 da entrega horas-3, em `fix/time-tracking-20261015`. Fonte funcional: `48655dd`, sobre `24431f6`; main incorporada: `8ec6699`. A estabilização test-only `41d9822`, documentada em `docs/stage-category-sync-validation.md`, também integra o SHA entregue, sem alterar o produto. Somente prisma-qa (3110/3111), com dados próprios e limpeza. Prisma-dev e produção preservados.

## Causa e alteração

A lista de resultados usava uma grade limitada a 220 px. Com seis títulos longos, as linhas da grade eram comprimidas para botões de 38 px, mas o texto continuava ocupando várias linhas e pintava sobre a opção seguinte. O novo teste reproduziu o defeito nas duas larguras: texto até 522 px em um botão cujo limite era 458 px.

`src/Prisma.Workspace.Web/src/features/time/TimeAdjustment.tsx:10` passa a usar uma coluna flexível com opções que não encolhem, quebra de texto normal e quebra de referências sem espaços. A lista continua limitada e rolável. Nenhum título é truncado; busca, ordenação, API e gravação permanecem iguais.

`src/Prisma.Workspace.Web/e2e/time-tracking-fixes.spec.ts:125` cria seis tarefas UUID via API, atribuídas ao usuário próprio. Confere texto completo, linhas dentro de cada botão, lista dentro da viewport, rolagem até a última opção e gravação de 60 segundos na tarefa escolhida. Não há sleep, asserção opcional, retry, aumento de timeout ou novo skip/test.fail. Usa as factories e a limpeza existentes.

## Verificações

| Comando | Resultado | Tempo |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln --no-restore` | Aprovado, 0 erros e 3 avisos anteriores | 58,92 s |
| `dotnet test tests/Prisma.Workspace.Tests --no-build --no-restore --logger 'console;verbosity=normal'` | 384/384 aprovados, zero pulos | 32,72 s (45,12 s com preparação) |
| `npx tsc -b` | Aprovado | 94,06 s |
| `npm run lint` | Aprovado, 0 erros e 12 avisos anteriores | 2,16 s |
| `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` | 155/155 aprovados, 42 arquivos | 270,63 s |
| `npm run build` | Aprovado | 77,53 s |
| Compilação TypeScript explícita do spec E2E, com emissão em diretório ignorado | Aprovado | 10,65 s |
| `npm run e2e -- time-tracking-fixes.spec.ts --repeat-each=3 --retries=0 --trace=off --reporter=line,json` | 31/31 aprovados, zero falhas/pulos/retries | 339,79 s (336,12 s nativos) |
| `npm run e2e -- --retries=0 --trace=off --reporter=line,json` | 203 aprovados, 6 pulos antigos, 0 falhas/retries | 1.571,81 s (1.569,89 s nativos) |
| Imagem candidata e smoke de QA | Imagem 48655dd; health 200 e login 200 | 217,22 s de build |

O xUnit usa banco separado no SQL do prisma-qa. A rodada focal partiu de QA recriado pelo SQL versionado e seed oficial, após backup com CHECKSUM/VERIFYONLY. As três repetições usam o mesmo banco, sem reset entre elas. A suíte completa recebe outro backup e reset antes da execução. Operações pesadas são sequenciais e respeitam a reserva da fila.

Tempo de cada caso nas três repetições, incluindo o tempo reportado pelo Playwright:

| Caso | Desktop (s) | Mobile (s) |
| --- | --- | --- |
| Timer após reload | 17,55; 11,15; 11,98 | 15,71; 15,39; 14,47 |
| Editar e atualizar totais | 8,42; 9,18; 10,57 | 9,12; 11,99; 9,64 |
| Excluir/cancelar/confirmar | 6,64; 6,07; 7,85 | 8,09; 8,94; 8,29 |
| Buscar por título/número e teclado | 8,59; 7,92; 9,50 | 10,38; 7,87; 9,88 |
| Seis títulos longos, rolagem e seleção | 14,46; 7,87; 10,54 | 11,77; 8,46; 8,88 |

## Tentativas e limites preservados

- Vitest inicial: 154/155 em 294,50 s; `ProjectKanbanDirect.test.tsx:89` não encontrou Editar coluna antes do quadro ficar pronto. O arquivo e o timeout não foram alterados. Foco 12/12 em 20,61 s e nova execução completa 155/155 passaram. A causa dessa primeira falha não foi isolada; não alegamos uma correção nesse teste.
- Primeiro arranjo do novo E2E retornou 403 na criação: usava o token Developer. Corrigido apenas o arranjo para o administrador da factory, conforme os outros testes; não houve alteração de permissões do produto. A tentativa seguinte reproduziu a falha geométrica nas duas larguras, antes de reconstruir a imagem.
- Rodada focal no QA acumulado foi interrompida: 6 aprovados, 2 timeouts do timer, 1 interrompido e 22 não executados; exit 130. A tela aguardava a lista de quadros durante reload. Logs registraram cancelamento em `GetAllBoards` → `ProjectAccessService.GetAccessibleProjectIdsAsync:77`, caminho do C6 preexistente. Erros de contexto encerrado/403 durante a interrupção também foram retidos. Esse código não foi alterado. O backup anterior ao reset preserva o estado acumulado para a investigação do C6; não afirmamos que o reset corrige o produto.
- A primeira suíte completa terminou em 201 aprovados, 6 pulos antigos e 2 falhas, sem retries, em 1.461,81 s. Rich description mobile:63 é a falha conhecida permitida; stage-category mobile:51 aguardava o quadro após reload. O readback HTTP confirmou nome/categoria persistidos. A sincronização desse teste foi tratada em commit/ENTREGA própria, preservando todas as asserções; uma nova suíte completa foi executada depois. Nenhum resultado negativo foi descartado.
- H15/H16: concorrência de lançamentos e de início de timers continua real e sem correção nesta entrega. H15 coincide com a ressalva baixa já aceita; H16, dois timers abertos, existe na main e está descrito na spec, mas seu enquadramento expresso pelo Prumo/Maestro ainda é necessário. Nenhum gate é considerado aprovado por esta documentação.

Ressalvas herdadas da revisão horas-3: o perfil Identity global sem vínculo não foi exercitado, pois a condição não existe no QA; custo de resolução do escopo por requisição e métodos antigos sem chamador permanecem pendências baixas. A prova SQL real foi novamente executada no xUnit completo. A visibilidade de horas próprias por papel continua conforme o inventário de B07 em `docs/report-builder-privacy-validation.md`.

Artefatos locais filtrados em `.runlogs/horas-4`, ignorados pelo Git. Não foram verificados Firefox/WebKit, produção ou provedores de IA reais. Nenhuma migration, ModelSnapshot ou estrutura de histórico alterada. A entrega requer revisão e QA independente do SHA final; somente o Maestro publica.

Resultado final: os 80 casos de horas/B07 passaram, assim como a recarga de coluna nas duas larguras (8,00 s desktop e 13,71 s mobile). Foram 88 limpezas, 192 ações e zero falhas de limpeza. A descrição rica não foi alterada e passou nesta rodada final; as tentativas anteriores continuam registradas.
