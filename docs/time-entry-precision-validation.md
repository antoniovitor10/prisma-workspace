# Validação da precisão na edição de horas — horas-6

Correção QA-H09, rodada de 16/10/2026 (Brasília). Fonte funcional `37313ee`; ajuste somente de teste `75f3544`; main `8ec6699` reconferida depois da suíte. Somente prisma-qa, app 3110/SQL 3111, banco PrismaWorkspace_QA. Unidade SQL em banco separado. Dev e produção preservados; nenhuma migration ou diferença em Migrations/ModelSnapshot contra a main.

## Causa e correção

O editor exibiu horários em segundos inteiros e reconstruiu ambos os instantes ao salvar qualquer campo. Editar apenas Observação apagava frações: podia criar sobreposição em períodos contíguos (400) ou transformar um intervalo de 59 s em 60 s (200). O QA independente confirmou ambos no desktop e mobile na entrega anterior.

`TimeAdjustment.tsx:59/75` compara cada valor editado ao seu valor visível original, normalizados em Brasília. Se o horário não mudou, envia a string integral original da API, incluindo sete casas fracionárias e offset. Se mudou, mantém a conversão existente. Equivalência entre HH:mm e HH:mm:00 não altera o instante. Validações temporais, autorização e auditoria do servidor permanecem obrigatórias e inalteradas; não se arredonda a regra de sobreposição.

`TimeAdjustment.test.tsx:10` acrescenta cinco regressões: nota apenas com UTC/offset -03, edição separada de início/fim preservando o outro extremo e normalização de segundos zero. No código anterior, as cinco falharam; após a correção, sete testes do arquivo passaram.

`e2e/time-tracking-fixes.spec.ts:78/113` acrescenta dois casos com usuário/projeto/tarefa próprios: períodos contíguos com sete casas e intervalo isolado de 59 s. A edição é pela UI; exige PUT200 e payload original integral, persistência por GET e reload, nota nova, duração e totais diário/semanal/por tarefa preservados. O caso anterior de edição intencional de fim também passou. Sem sleeps, novo skip, retry, test.fail ou aumento de timeout.

## Verificações

Tempos de parede do comando; preparação xUnit indicada separadamente. Frontend executado em src/Prisma.Workspace.Web.

| Comando | Resultado | Tempo (s) |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln --no-restore` | Passou, 0 erros/3 avisos anteriores | 64,82 |
| `dotnet test tests/Prisma.Workspace.Tests --no-build --no-restore --logger 'console;verbosity=normal'` | 391/391, incluindo os sete SQL de timer | 35,87; 51,89 com preparação |
| `npx tsc -b` | Passou | 87,35 |
| `npm run lint` | Passou, 0 erros/12 avisos anteriores; final após ajuste do E2E | 1,80 |
| `npm test -- src/features/time/TimeAdjustment.test.tsx --pool=threads --maxWorkers=1 --fileParallelism=false` | 7/7 | 7,29 |
| `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` | 165/165, 42 arquivos | 205,75 |
| `npm run build` | Passou | 62,97 |
| Tipos E2E, comando abaixo | Passou, após ajuste do E2E | 4,65 |
| `npm run e2e -- time-tracking-fixes.spec.ts --repeat-each=3 --retries=0 --max-failures=1` | 49/49; desktop/mobile, 0 pulos/retries | 507,18 |
| `npm run e2e -- --retries=0` | 218 aprovados, 6 pulos anteriores, 1 falha antiga, exit1; 0 retries | 1.738,54 |

```bash
npx tsc --ignoreConfig --strict --skipLibCheck --target ES2023 --module ESNext --moduleResolution Bundler --types node --esModuleInterop --outDir ../../.runlogs/horas-6/e2e-types-2 e2e/time-tracking-fixes.spec.ts
```

Os wrappers E2E acrescentaram trace=off, reporters line/json e diretório privado de artefatos. Chromium oficial Playwright1.62.1, isolado no projeto QA, conexão por WS3115/encaminhamento loopback conforme a rodada anterior; nenhum erro de transporte ignorado. Antes do focal e da full, backups600 verificados com RESTORE VERIFYONLY, SQL versionado para recriar somente PrismaWorkspace_QA e seed oficial. Sem reset entre as três repetições. Focal:48 limpezas/96 ações/0 falhas, máximo614ms. Full:104 limpezas/224 ações/0 falhas, máximo2.363ms.

A full não está inteiramente verde. Única falha: `rich-task-description.spec.ts:63` mobile, timeout30s ao reabrir o detalhe, igual à falha anterior permitida pelo GOALS; arquivo idêntico à main. Nenhuma nova falha; todos96 de horas/B07 passaram:46 privacidade HTTP,6 construtor/CSV,10 concorrência,16 correções e18 complementares. Colunas e IA passaram nesta rodada, sem atribuir uma correção nova.

## Tempos individuais do focal

Todos aprovados; máximo21,34s no caso anterior de exclusão mobile, abaixo do padrão30s. Os dois novos casos ficaram abaixo14s.

| Caso | Desktop, 3 rodadas (s) | Mobile, 3 rodadas (s) |
| --- | --- | --- |
| `time-tracking-fixes.spec.ts:30` timer em andamento não dobra após recarregar o card | 20,18; 13,97; 11,21 | 10,87; 11,65; 11,78 |
| `time-tracking-fixes.spec.ts:55` editar lançamento no ajuste persiste horários, observação e atualiza dia e semana | 12,81; 8,22; 8,99 | 8,36; 13,08; 8,96 |
| `time-tracking-fixes.spec.ts:78` editar só a observação preserva períodos contíguos com frações de segundo | 11,97; 7,62; 8,94 | 8,63; 13,93; 9,43 |
| `time-tracking-fixes.spec.ts:113` editar só a observação mantém 59 segundos e os instantes de um período isolado | 9,07; 8,72; 9,46 | 13,68; 10,83; 11,44 |
| `time-tracking-fixes.spec.ts:142` excluir lançamento exige confirmação, cancelar preserva e confirmar atualiza os totais | 6,73; 6,58; 7,00 | 8,38; 7,73; 21,34 |
| `time-tracking-fixes.spec.ts:162` buscar tarefa pelo título e número no ajuste funciona com teclado | 7,43; 5,80; 6,63 | 7,21; 7,93; 10,05 |
| `time-tracking-fixes.spec.ts:217` busca do ajuste mantém seis títulos longos legíveis e permite selecionar após rolar | 8,13; 7,56; 7,98 | 8,01; 10,14; 10,33 |
| `time-tracking-fixes.spec.ts:257` busca do ajuste mantém a opção ativa visível no teclado e grava o destino do Enter | 8,74; 12,88; 8,55 | 9,90; 9,94; 9,91 |

## Tentativas negativas e limites

- Vitest antes da correção:5 falhas novas/2 testes anteriores aprovados,12,43s. Depois:7/7 em7,29s. A falha demonstra perda das frações, inclusive do extremo não editado.
- Primeiro focal:3 aprovados/1 falha/45 não executados por max-failures,65,35s (63,32s nativo). O novo teste comparava o DTO de criação inteiro: displayName nulo ali é enriquecido no GET. PUT200, timestamps integrais e total já estavam corretos. `75f3544` compara explicitamente IDs, dono, tarefa, extremos, duração e nota e exige exatamente dois registros. Nenhuma asserção temporal retirada. Full não foi iniciada nessa tentativa.
- Imagem funcional373 construída em166,43s; candidata75 em132,37s, health/login200, quatro DLLs e todos os ativos em wwwroot/assets idênticos. Não é comparação do filesystem inteiro. Imagem documental final deve preservar esse manifesto antes da entrega.
- Sem CI remoto, Firefox/WebKit, leitor de tela, duplo toque físico ou duas APIs fisicamente implantadas nesta rodada. Produção não consultada. Legados duplicados encerrados em agora conservam intervalos históricos até ajuste do dono; cabe ao Maestro avaliar a checagem em produção antes de PUBLICADO. H15/manual simultâneo e custos/N+1 do C6 permanecem nos limites anteriores.
- Revisão e QA independentes devem aprovar o mesmo SHA final; esta validação do executor não os substitui. C1–C6 somente após PUBLICADO das horas. Artefatos privados em .runlogs/horas-6, fora do Git.
