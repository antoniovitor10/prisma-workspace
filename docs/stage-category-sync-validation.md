# Validação da sincronização de recarga de coluna

ENTREGA stability-2, separada da correção visual QA-H06. Branch `fix/time-tracking-20261015`; commit de teste `41d9822`, sobre a fonte funcional `48655dd`. Nenhum código de produto alterado nesta estabilização.

## Evidência e causa

A suíte completa de horas-4 terminou com 201 aprovados, 6 pulos antigos e duas falhas: a conhecida `rich-task-description.spec.ts:63` mobile e `stage-category.spec.ts:51` mobile. A segunda falhou na asserção visual de 5 segundos logo após `page.reload()`, enquanto o snapshot mostrava **Carregando quadro...**. Os 80 casos de horas/B07 passaram.

Uma consulta HTTP posterior, antes de qualquer reset, confirmou que a coluna específica já estava renomeada e com categoria 5. O PUT havia retornado 204. A falha foi de sincronização da expectativa visual com as consultas que alimentam o quadro, sem evidência de perda de persistência. O mesmo spec havia sido identificado como instável na outra frente, mas esta entrega não o tratou como exceção automática.

## Alteração

`src/Prisma.Workspace.Web/e2e/stage-category.spec.ts` usa `navigateWithApi`, já existente, para esperar respostas GET completas e 200 de `/api/Stages/board/{id}` e `/api/WorkItems/board/{id}` iniciadas pelo documento recarregado. Depois mantém a mesma asserção do botão renomeado e a consulta independente de persistência/categoria.

Não mudou timeout, dado, categoria esperada, permissão ou implementação. O spec continua importando a fixture base; não foi aplicado o desvio de tipografia da fixture funcional. Não há sleep, retry, skip ou test.fail novo. A dependência histórica do seed e a criação de coluna nele permanecem; esta alteração não reescreve a factory nem enfraquece a asserção.

## Verificações

| Comando | Resultado | Tempo |
| --- | --- | --- |
| Compilação TypeScript explícita dos specs de coluna e horas, com emissão em diretório ignorado | Aprovado | 6,33 s |
| `npm run lint` | Aprovado, 0 erros e 12 avisos anteriores | 0,84 s |
| `npm run e2e -- stage-category.spec.ts --repeat-each=3 --retries=0 --trace=off --reporter=line,json` | 7/7 aprovados, desktop/mobile 3 vezes, zero pulos/retries | 85,83 s (84,15 s nativos) |
| Imagem no SHA do teste e comparação de DLLs/ativos com a imagem funcional de horas-4 | Aprovada; SHA 41d9822, health/login 200, DLLs e ativos idênticos à fonte funcional 48655dd | 114,37 s |
| `npm run e2e -- --retries=0 --trace=off --reporter=line,json` após backup verificado e reset QA | 203 aprovados, 6 pulos antigos, 0 falhas/retries | 1.571,81 s (1.569,89 s nativos) |

Build .NET, xUnit 384/384, Vitest 155/155, `tsc -b`, build frontend e focal de horas 31/31 estão em `docs/time-adjustment-search-validation.md`. O produto não mudou depois desses gates; a comparação de hashes da imagem demonstrou a equivalência.

Artefatos filtrados em `.runlogs/horas-4`, ignorados pelo Git. Somente prisma-qa 3110/3111; prisma-dev e produção intocados. Nenhuma migration, ModelSnapshot ou estrutura de auditoria alterada. Rich description permanece sem alteração; passou nas duas larguras na suíte final (16,27 s desktop e 11,80 s mobile), sem alegar correção. Revisão e QA independente do SHA final continuam obrigatórios.

Resultado final: os 80 casos de horas/B07 passaram, assim como a recarga de coluna nas duas larguras (8,00 s desktop e 13,71 s mobile). Foram 88 limpezas, 192 ações e zero falhas de limpeza. A descrição rica não foi alterada e passou nesta rodada final; as tentativas anteriores continuam registradas.
