# Estabilidade das datas no E2E de horas — horas-7

Rodada de 17/10/2026. Mudança somente em teste: `4b761ee` usa dias encerrados e navega à semana correspondente; `a85f1b2` sincroniza pela semana visível e confere a data exata do GET de lançamentos. Fonte funcional continua `37313ee`, aprovada no runtime `71bd3af`; main `8ec6699` reconferida após a execução.

## Causa e correção

Às 00:01 de Brasília, o caso de 59 s criava um período de hoje às00:10–00:11. A criação recebia400 antes de qualquer edição pela UI: o período ainda era futuro. O helper também deixava outros casos com horários de hoje vulneráveis nos primeiros minutos do dia. Isto é defeito do arranjo do teste, sem regressão na regra do produto que recusa futuro.

Em `time-tracking-fixes.spec.ts:38`, os ajustes usam um dia já encerrado, calculado explicitamente em Brasília. O caso de59s usa o domingo da semana anterior, forçando essa navegação em qualquer dia da execução. O helper`:11` aguarda GET200 e a semana renderizada, usa Anterior quando necessário, exige que a data exista e confere o parâmetro date do GET de lançamentos. Assim também é possível acessar o dia anterior na virada de segunda-feira. Sem relógio falso ou alteração da hora do servidor.

Todas as asserções anteriores permanecem: PUT200, timestamps integrais de sete casas/offset, contiguidade,59s, nota, totais diário/semanal/por tarefa, GET e reload. Foram acrescentadas sincronização visual e checagem da data consultada; nenhuma regra temporal, produto, spec, migration ou estrutura de auditoria mudou. O diff entre71bd3af e a85f1b2 contém apenas esse arquivo E2E.

## Provas e verificações

Comandos .NET na raiz; comandos frontend em src/Prisma.Workspace.Web. Tempos de parede.

- Antes: execução real em03:01UTC (00:01Brasília),1setup aprovado e1 caso reprovado no POST manual201 esperado/400 recebido,19,76s. Não era falha da edição; ela nem havia começado.
- Depois: preview nas duas larguras,3/3 incluindo setup em56,43s, ainda antes de00:11. A fonte finala85 também passou o caso entre03:09:43,196 e03:09:53,743UTC, dentro da mesma janela em que o arranjo anterior era inválido. Não se esperou o horário futuro tornar-se passado para obter aprovação.

| Comando | Resultado | Tempo (s) |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln --no-restore` | Passou; 0 erros, 3 avisos anteriores | 73,01 |
| `dotnet test tests/Prisma.Workspace.Tests --no-build --no-restore --logger 'console;verbosity=normal'` | 391/391; inclui sete SQL de timer | 32,87; 40,85 com preparação |
| Mesmo comando com `--filter FullyQualifiedName~BoardStructureSqlTests.DeleteBoard_FailureRollsBackTaskStageAndHistory` | 1/1, controle após deadlock | 11,36 |
| `npx tsc -b` | Passou | 72,96 |
| Tipos E2E, comando abaixo | Passou | 6,37 |
| `npm run lint` | Passou; 0 erros, 12 avisos anteriores | 0,81 |
| `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` | 165/165, 42 arquivos | 226,41 |
| `npm run build` | Passou | 87,51 |
| `npm run e2e -- time-tracking-fixes.spec.ts --repeat-each=3 --retries=0 --max-failures=1` | 49/49; 0 pulos/retries | 586,34 |
| `npm run e2e -- --retries=0` | 219 aprovados, 6 pulos anteriores, 0 falhas/retries; exit0 | 1.776,28 |

```bash
npx tsc --ignoreConfig --strict --skipLibCheck --target ES2023 --module ESNext --moduleResolution Bundler --types node --esModuleInterop --outDir ../../.runlogs/horas-7/e2e-types-final e2e/time-tracking-fixes.spec.ts
```

Os wrappers E2E acrescentaram trace=off, reporters line/json e diretório privado de artefatos. Chromium Playwright1.62.1 isolado no projeto QA, por WS3115/loopback, um worker. Focal no mesmo banco entre as três repetições:48 limpezas/96 ações/0 falhas, máximo915ms. Full após banco QA recriado:104 limpezas/224 ações/0 falhas, máximo1.751ms; todos96 cenários de horas/B07 passaram. Nenhum novo skip, test.fail, retry, sleep ou aumento de timeout.

Rich:63mobile passou nesta full sem alteração do arquivo; não atribuir sua estabilidade a esta correção. Os seis pulos são os mesmos anteriores. IA e colunas também passaram.

## Tempos individuais do focal

Maior duração reportada22,575s, no caso anterior do timer, abaixo do padrão30s. Casos de precisão ficaram em até14s.

| Caso | Desktop, 3 rodadas (s) | Mobile, 3 rodadas (s) |
| --- | --- | --- |
| `time-tracking-fixes.spec.ts:51` timer em andamento não dobra após recarregar o card | 22,57; 13,30; 11,87 | 14,07; 13,18; 21,28 |
| `time-tracking-fixes.spec.ts:76` editar lançamento no ajuste persiste horários, observação e atualiza dia e semana | 10,30; 15,14; 8,79 | 16,69; 9,52; 12,79 |
| `time-tracking-fixes.spec.ts:99` editar só a observação preserva períodos contíguos com frações de segundo | 11,41; 12,04; 9,67 | 10,23; 10,74; 11,44 |
| `time-tracking-fixes.spec.ts:134` editar só a observação mantém 59 segundos e os instantes de um período isolado | 10,55; 11,12; 14,00 | 11,37; 11,66; 11,40 |
| `time-tracking-fixes.spec.ts:163` excluir lançamento exige confirmação, cancelar preserva e confirmar atualiza os totais | 8,57; 7,43; 7,97 | 7,74; 8,16; 9,78 |
| `time-tracking-fixes.spec.ts:183` buscar tarefa pelo título e número no ajuste funciona com teclado | 13,99; 8,26; 6,86 | 8,76; 9,39; 10,37 |
| `time-tracking-fixes.spec.ts:238` busca do ajuste mantém seis títulos longos legíveis e permite selecionar após rolar | 11,11; 12,83; 8,26 | 14,50; 10,87; 9,93 |
| `time-tracking-fixes.spec.ts:278` busca do ajuste mantém a opção ativa visível no teclado e grava o destino do Enter | 12,12; 12,17; 10,28 | 16,77; 10,66; 11,61 |

## Tentativa SQL negativa e limites

A primeira suíte xUnit ficou em390/391: `BoardStructureSqlTests.DeleteBoard_FailureRollsBackTaskStageAndHistory` recebeu InvalidOperationException envolvendo SqlException de deadlock, em vez do DbUpdateException deliberadamente provocado pelo teste. Foram35,43s de comando,50,04s com preparação. Nenhum arquivo C# mudou nesta rodada. O caso isolado passou1/1 em11,36s; uma confirmação integral passou391/391 em32,87s,40,85s com preparação. Sem mudar fonte, isolamento, timeout, retry ou asserções. O grafo da outra transação não foi capturado; não se afirma ter corrigido o deadlock. Ambas as execuções foram preservadas.

Somente prisma-qa3110/SQL3111/PrismaWorkspace_QA, com backups600 verificados por RESTORE VERIFYONLY e SQL versionado/seed oficial antes de focal e full. Banco xUnit separado preservado; dev e produção intocados. Imagem candidata construída em120,25s, health/login200; quatro DLLs e todos os ativos wwwroot/assets idênticos aos de71bd3af. Não é comparação do filesystem inteiro. A imagem do SHA documental deve preservar o manifesto testado antes da entrega.

Sem CI remoto, Firefox, aparelho Safari/iOS real, leitor de tela ou duas APIs fisicamente implantadas nesta rodada. WebKit4/4 e QA funcional completos pertencem à rodada anterior da mesma implementação de produto; não substituem o novo aceite independente no SHA final. Ressalvas antigas e decisão de checagem de legados em produção continuam com Maestro; timer esquecido e H15/manual simultâneo permanecem nos limites anteriores.

O PRONTO-PROD de71bd3af foi retirado ao confirmar o arranjo futuro. Nova entrega, revisão e QA exatos precedem nova declaração; PUBLICADO precede C1–C6. Artefatos privados em .runlogs/horas-7, fora do Git.
