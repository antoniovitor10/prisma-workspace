# Estabilização dos E2E de quadros e catálogo de IA

Entrega própria de testes, conforme o GOALS da Frente 1. Os commits de estabilização alteram somente specs Playwright; as correções funcionais de horas têm [relatório separado](time-tracking-followup-validation.md). Não houve alteração de produto, spec funcional, migration ou timeout global nesses commits.

## Causas observadas

| Spec | Falha observada | Correção do teste |
| --- | --- | --- |
| `board-stage-sync.spec.ts` | Escolhia o primeiro quadro de um projeto compartilhado. Após outros specs, o quadro D89 selecionado não tinha tarefa aberta; também fechava a gaveta antes de confirmar o PUT | Projeto, quadro, colunas e tarefa próprios por factory; aguarda PUT 204 antes de fechar, verifica detalhe e projeção da API, estado da lista e documento recarregado |
| `independent-board-columns.spec.ts` | Procurava o card enquanto o quadro ainda carregava; o cenário acumulava preparação, três fluxos, histórico e limpeza no prazo de 30 s, excedido na terceira repetição | Três contratos com projetos próprios, navegação sincronizada por HTTP e limpeza no teardown da fixture; preservadas as recusas, transferências, destino e pelo menos três eventos |
| `ai-assistant.spec.ts:115` | Buscava a opção antes de aguardar o catálogo carregado com debounce. A opção não apareceu em cinco segundos no desktop/mobile; o caso passou isoladamente com trace | Aguarda o POST da configuração final, verifica HTTP 200, catálogo pronto e modelo esperado, antes de manter as asserções originais de UI |

O trace privado do caso de IA registra uma consulta cancelada durante a edição e duas respostas 200 posteriores. A causa da variação de duração entre execuções não foi isolada. A correção sincroniza com a fronteira HTTP real; não simula a API, não clica em atualizar para contornar o carregamento automático e não amplia o prazo das asserções de opções.

## Contratos preservados

- **Sincronização de coluna:** a tarefa começa em coluna aberta; a UI escolhe uma coluna de categoria `Done = 5`; o servidor responde 204, grava `StageId` e `CompletedAt`, e a projeção do quadro e a lista exibem o destino antes e depois de uma navegação nova.
- **Independência/cópia:** três colunas copiadas com IDs diferentes; renomear e ordenar A não altera B; ordem com IDs de B é recusada; movimento direto entre colunas de quadros distintos é recusado; excluir quadro/coluna ocupados sem destino é recusado; excluir coluna com destino mantém a tarefa na UI.
- **Transferência pela gaveta:** tarefa própria no backlog, seleção de quadro e coluna, POST 204, detalhe persistido e card encontrado no quadro de destino em documento novo.
- **Exclusão com histórico:** a mesma tarefa passa por exclusão de coluna, transferência entre quadros e exclusão de quadro ocupado com destino explícito; mantém destino correto, pelo menos três eventos e presença na UI após navegação.
- **Catálogo/credencial:** espera a resposta da configuração final de provedor, método, URL e chave fictícia; verifica modelo no JSON e na opção; mantém salvar, testar, ativar e a verificação de que a credencial não retorna ao navegador.

As correções de preparação do próprio teste também foram verificadas no código canônico: PUT de tarefa retorna **204**, e categoria **4 é Review**, enquanto **5 é Done**. A asserção de `CompletedAt` foi mantida. O nome acessível do card inclui `#número · título`, agora conferido por expressão ancorada com o título UUID, sem correspondência opcional.

## Verificações

| Comando | Resultado | Tempo |
| --- | --- | --- |
| TypeScript explícito dos quatro specs e fixtures, emitindo JS em diretório ignorado | Passou; repetido após ajuste da fixture | 7,37 s (final) |
| `npm run e2e -- board-stage-sync.spec.ts --max-failures=1 --retries=0 --reporter=line,json` | 3/3, desktop/mobile e setup | 54,70 s |
| `npm run e2e -- independent-board-columns.spec.ts --max-failures=1 --retries=0 --reporter=line,json` | 7/7, desktop/mobile e setup | 79,09 s |
| Specs alterados, desktop/mobile com `--repeat-each=3`, mesmo banco, sem retries | 85/85 com setup; zero skips, retries ou falhas; 78 limpezas sem falhas | 990,56 s |
| Suíte inteira, sem retries | 149 aprovados, 6 skips condicionais anteriores; zero falhas/flaky/retries | 1.113,40 s |

A opção de falha rápida é usada apenas na rodada focal para interromper ao primeiro defeito, sem executar uma suíte completa com fonte já reprovada. Não transforma falhas em esperadas. Na repetição final, todos os 85 casos previstos foram executados e passaram. Na suíte inteira, todos os casos habilitados passaram; os seis skips condicionais já existentes são descritos no relatório de horas. As 34 limpezas dessa rodada não tiveram falhas.

Tentativas incompletas e falhas de preparação não são aprovação: os logs/JSON separados permanecem ignorados em `.runlogs/horas-2`. A rodada completa anterior terminou com 134 aprovados, sete falhas e seis skips, em 1.290,11 s; seus artefatos foram preservados antes dos ajustes. Dois projetos de horas dos timeouts e um projeto de colunas que excedeu a limpeza foram identificados por UUID, horário e proprietário e arquivados; nenhum dado compartilhado do seed foi limpo.

## Ambiente e limites

Execuções exclusivamente no `prisma-qa`, app 3110 e SQL 3111. Prisma-dev, banco da demonstração e produção preservados. Uma operação pesada por vez, com reserva na fila e prioridade reduzida; janela de silêncio da demonstração respeitada. Não foram usados sleeps, novos skips, `test.fail`, retries ou timeout global maior para fazer passar.

Uma repetição posterior terminou durante a limpeza inline após as asserções. As factories agora registram ações na fixture `request`: seu `finally` executa a pilha inversa com o contexto HTTP disponível e prazo próprio de 20 s, mantendo o corpo do cenário em 30 s. Erros de limpeza continuam reprovando; anotações registram quantidade, duração e falhas. Smoke dessa estrutura: 5/5 em 74,53 s, com uma ação para quadro/projeto e duas para horas, todas sem falhas.

Também foi medido o efeito dos dados acumulados: 205 projetos, 203 arquivados, 406 quadros e GET de quadros em 6,1 s, no caminho N+1 já registrado para C6. Não foi antecipada a correção de produto desse grupo. Foi preservado backup SQL verificado, com acesso privado, e recriado somente `PrismaWorkspace_QA` pelo SQL versionado adaptado ao nome QA e pelo seed oficial. O banco xUnit permaneceu intacto. A repetição usa o mesmo banco entre casos; a suíte inteira começa em outro reset desse banco QA para comparação com CI.

As factories usam nomes UUID e projetos próprios; a conexão de IA usa exclusivamente o provedor falso local, sem credencial real. Os erros HTTP não propagam cabeçalhos; a validação local filtra credenciais antes de gravar saídas e mantém somente metadados da evidência de rede, sem reter o trace autenticado. Revisão e QA independente do SHA da entrega permanecem necessários antes de PRONTO-PROD.
