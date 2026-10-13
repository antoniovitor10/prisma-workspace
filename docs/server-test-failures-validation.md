# Correções de testes no servidor — tarefa 2

Execução em 2026-10-13, por Tear, na branch `fix/server-test-failures`, baseada em `origin/feat/ai-assistant-foundation` / `1494b7a`. Ambiente existente `prisma-dev`, app/API em 3100. Nenhum código de produto, spec, migration ou container alheio alterado.

## Causas e correções

1. Context Explorer: as três expectativas continuavam em quatro contratos, mas D96 acrescentou `agents/qa.yaml`. Loader e model já descobrem YAML dinamicamente. As expectativas agora exigem exatamente os cinco IDs, incluindo `qa`; o texto do painel deixou de declarar quatro contratos.
2. OrganizationSettings: o render síncrono iniciava queries e a busca por role no DOM completo concorria com promises/notificações pendentes. Instrumentação temporária mostrou membros ainda pending ao retornar do render (410 ms), sucesso da query em 657 ms e atualizações posteriores em 873/1077 ms. Queries auxiliares de fluxo e status IA não tinham mock e terminaram em erro de rede; faltava desmontar e limpar o cliente. Não foi encontrado vazamento de fake timers entre arquivos; o teste isolado também passou antes da correção, confirmando a intermitência. Agora o teste usa `await act`, mocks auxiliares e cleanup/clear/restoreAllMocks. As asserções de campo, e-mail e payload permanecem; nenhum timeout foi aumentado. [React act](https://react.dev/reference/react/act) documenta a espera das atualizações assíncronas; [Testing Library](https://testing-library.com/docs/dom-testing-library/api-async/) documenta o prazo padrão de 1000 ms do findBy.
3. Atribuição E2E: `resolveSeedProject` escolhia um projeto com quadro, sem garantir tarefa. O teste tomava `tasks.body[0]` sem verificar HTTP. Reprodução controlada criou um projeto vazio ordenado antes dos demais: o resolver o selecionou, GET retornou 200/[] e o teste mobile original falhou em `task?.id`, linha 49. A fixture de diagnóstico foi arquivada. O spec corrigido cria projeto/item exclusivos, verifica os status e a presença do ID criado, mantém as verificações de acesso/atribuição/revogação e arquiva seu projeto em finally. Não há evidência de defeito de produto nesse cenário. A resposta HTTP da falha histórica da tarefa 1 não foi registrada, portanto não é possível afirmar seu payload exato retrospectivamente.

## Verificações

Tempos de parede medidos com `/usr/bin/time`. Context Explorer executado em `tools/context-explorer`; comandos frontend em `src/Prisma.Workspace.Web`. Logs completos locais ignorados em `.runlogs/server-test-failures`.

| Comando | Resultado | Tempo |
| --- | --- | --- |
| `npm test` (Context Explorer) | passou, 42/42 | 1,32 s |
| `npm run build` (Context Explorer web, adicional) | passou; aviso de chunk grande | 24,07 s |
| `npx tsc -b` (verificação final) | passou, exit 0 | 145,59 s |
| `npm run lint` (verificação final) | passou, 0 erros / 12 avisos anteriores | 0,99 s |
| `npm test` (frontend) | passou, 145/145 em 39 arquivos | 331,17 s |
| `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` | passou, 145/145 em 39 arquivos | 217,72 s |
| `npm run e2e -- e2e/project-assignment-access.spec.ts --project=chromium-desktop --project=chromium-mobile --repeat-each=3` | passou, 7/7 (setup + 3 por dispositivo), sem retries | 75,03 s |
| `npm run e2e` | falhou, 116 aprovados / 3 falhas / 6 skips, sem retries | 843,32 s |

A suíte completa encontrou uma falha adicional fora do escopo em `e2e/board-stage-sync.spec.ts:24`, nos dois dispositivos: `O quadro D89 1791915394618 A precisa de uma tarefa fora da etapa de conclusão`, recebido undefined. O spec escolhe o primeiro quadro do projeto compartilhado e exige uma tarefa não concluída sem prepará-la. A execução para antes de interagir com o status do produto. Esse teste permaneceu intacto; nenhum dado compartilhado foi alterado para esconder a falha. Artefatos em `src/Prisma.Workspace.Web/test-results/board-stage-sync-*`.

Outra falha fora do escopo: `e2e/rich-task-description.spec.ts:63` em mobile, timeout de 30000 ms ao clicar novamente em `Abrir detalhes de`. Desktop passou. Causa não determinada nesta tarefa; não houve alteração desse teste ou do editor de produto. Artefatos em `src/Prisma.Workspace.Web/test-results/rich-task-description-*`. O teste de atribuição corrigido passou também dentro da suíte completa em ambos os dispositivos.

A verificação inicial de tipos também passou em 78,47 s e lint em 1,24 s; a tabela usa a repetição após a última alteração. Tempos refletem carga do servidor e execução concorrente de Vitest/E2E.

## Limites e continuidade

Não repetidos .NET restore/build/xUnit, build da SPA, testes Python ou smoke HTTP: não houve alteração nessas áreas. Sem QA visual manual, proxy público autenticado, modelos IA reais, produção, deploy ou merge. O ambiente de desenvolvimento continua ativo. Próximo passo: Maestro revisar os três commits e encaminhar as falhas adicionais da suíte completa em tarefa própria.
