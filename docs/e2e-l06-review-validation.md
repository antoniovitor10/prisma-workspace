# Tarefa 6 — revisão L06

Resultado final da parte A: 17 casos L06, cada um repetido três vezes em desktop e mobile, 102/102 aprovações e máximo individual de 16,28 s. A rodada conjunta com L08 terminou com 145 normais e 18 falhas esperadas, zero inesperadas/retries, em 1336,53 s. Sem defeito nas guardas pedidas: API retorna 400; UI inválida envia zero PUTs e exige alerta novo. Produto/specs/schema preservados.

Os timeouts de transporte no host tiveram evidência concreta net::ERR_NETWORK_CHANGED em GET real. A origem da alteração de rede não foi identificada; não é prova de defeito do produto. A rodada final usou container próprio prisma-dev-e2e-runner, imagem oficial Playwright 1.62.1, Node 24.18.1, network none. Um proxy TCP por socket Unix privado modo 600 encaminha o loopback 3100 ao mesmo app/banco prisma-dev; não altera bytes HTTP, credenciais, respostas, endpoint, seed, API ou SQL. Gateway Docker não respondeu; não se alterou firewall. Tipografia externa continua fora da prova funcional/QA visual. Checks frontend executados no Node 22 do host.

Na confirmação da exclusão, o teste espera o GET das tarefas do destino, exige IDs exatos e depois confere seleção, ausência do quadro, GET 404 e estado persistido de cada tarefa. A asserção anterior disputava cinco segundos com a lista ainda carregando. Criação/cancelamento de quadro e colunas conservam suas recargas.

Comando final conjunto, via driver local ignorado:

`PRISMA_E2E_RUN_NAME=repeat-isolated bash .runlogs/e2e-l08/run-isolated.sh e2e/boards-columns-l06.spec.ts e2e/time-tracking-l08.spec.ts --repeat-each=3`

Maior tempo das três repetições, em segundos (inclui preparação/limpeza do resultado Playwright):

| Linha / caso | Desktop máx. | Mobile máx. |
| --- | --- | --- |
| 60 — L06 cria quadro pela UI, copia somente colunas e persiste após recarga | 12.70 s | 14.76 s |
| 92 — L06 cancelar exclusão de quadro preserva quadro e tarefas após recarga | 12.71 s | 15.21 s |
| 108 — L06 excluir quadro ocupado sem destino é recusado pela UI e preserva tarefas | 9.21 s | 14.33 s |
| 131 — L06 confirmar exclusão de quadro mostra destino na UI e persiste transferência | 11.27 s | 16.27 s |
| 163 — L06 criar coluna pela UI persiste nome e categoria após recarga | 10.53 s | 14.45 s |
| 180 — L06 renomear coluna pela UI conserva categoria e persiste após recarga | 9.47 s | 15.78 s |
| 197 — L06 excluir coluna vazia: cancelar preserva; confirmar remove sem afetar outras | 9.43 s | 14.01 s |
| 215 — L06 excluir coluna ocupada: API exige destino; UI informa impacto e cancelar preserva tarefas | 10.85 s | 9.06 s |
| 239 — L06 confirmar exclusão ocupada transfere os mesmos IDs e persiste após recarga | 11.00 s | 10.82 s |
| 260 — L06 D62: API recusa reclassificação sem consentimento da categoria ou descendentes | 4.10 s | 4.11 s |
| 278 — L06 UI exige ambos os consentimentos com alerta novo e zero PUTs; cancelar preserva | 9.96 s | 9.53 s |
| 305 — L06 concluir pela UI consente descendência externa e persiste estado e histórico | 12.18 s | 14.10 s |
| 332 — L06 conclusão via API alcança neto externo, preserva colunas e histórico individual | 8.25 s | 6.06 s |
| 352 — L06 reabrir: API e UI exigem confirmação; cancelar preserva tarefas concluídas | 13.21 s | 8.62 s |
| 374 — L06 reabrir coluna ocupada pela UI persiste estado e histórico das tarefas nela | 10.69 s | 13.33 s |
| 394 — L06 reabertura via API preserva descendentes concluídos fora da coluna | 4.63 s | 4.33 s |
| 411 — L06 leitor não cria nem exclui coluna pela UI ou API | 12.11 s | 13.92 s |

A suíte completa com SQL oficial recriado será registrada no relatório L08, branch descendente. Os diagnósticos abaixo são versões anteriores e não aprovação da versão final.

Referências: [Chromium fecha conexões ao detectar alterações de rede](https://chromium.googlesource.com/chromium/src.git/+/refs/tags/126.0.6478.162), [Docker network none](https://docs.docker.com/engine/network/drivers/none/), [imagem Playwright e correspondência de versão](https://playwright.dev/docs/docker).

Branch: test/e2e-l07, base 064375e. Produto, specs e migrations preservados.

## Alterações

- Exclusão ocupada dividida em cancelamento/recusa e transferência confirmada.
- DELETE sem destino: 400, mensagem específica e comparação do estado persistido.
- Conclusão: PUTs sem confirmação principal e sem confirmDescendants recusados com 400; dois descendentes abertos reais, em outra coluna, incluindo neto.
- UI: contador de PUT permanece zero; cada tentativa começa com editor novo/alerta ausente e exige a mensagem exata nova.
- Conclusão e reabertura têm jornadas próprias. A preservação as-built de descendentes externos na reabertura fica em caso de API separado; a dúvida anterior de D62 permanece.
- Reload observa requisições novas e aguarda editor fechado após mutation, para não capturar refresh anterior em andamento.

## Verificações executadas nesta parte

Comandos frontend em src/Prisma.Workspace.Web, contra prisma-dev em 3100, um worker, sem retries.

| Comando | Resultado | Tempo |
| --- | --- | --- |
| npx tsc -b | passou | 92,26 s |
| npm run lint | passou, 12 avisos anteriores | 1,46 s |
| npx tsc --ignoreConfig --target ES2022 --module ESNext --moduleResolution bundler --strict --skipLibCheck --types node --outDir ../../.runlogs/e2e-l08/a-typecheck e2e/boards-columns-l06.spec.ts | passou | sem medição própria |
| npm run e2e -- e2e/boards-columns-l06.spec.ts --repeat-each=3 --reporter=html,list,json (versão diagnóstica) | 60 passaram, um timeout de reload; um mobile de 20,30 s motivou divisão | 744,63 s |
| npm run e2e -- e2e/boards-columns-l06.spec.ts --grep 'concluir coluna|reabrir coluna|reabertura via API|confirmar exclusão ocupada' --reporter=html,list,json (versão final) | 9/9, incluindo setup, desktop/mobile | 82,43 s |

No focal final: exclusão 9,54/9,73 s; conclusão 15,57/10,17 s; reabertura UI 10,95/10,16 s; descendentes externos API 4,56/4,94 s (desktop/mobile). Sem defeito de produto nas guardas pedidas.

Repetição da versão final e suíte completa com banco limpo serão registradas no relatório L08, na branch descendente. Não executar reset na QA; não corrigir testes antigos. Logs/JSON ignorados em .runlogs/e2e-l08.

## Complemento da revisão

A primeira rodada conjunta no banco histórico foi interrompida: 32 aprovações normais, 3 falhas esperadas L08, 5 falhas de espera por carregamento e um caso interrompido, 401,15 s. Um caso de conclusão passou em 22,74 s. O banco tinha 215 projetos arquivados e 433 quadros deles ainda devolvidos na listagem. GET /api/Boards retornou 446 quadros em 4,01 s como administrador; GET /api/projects ativos retornou seis em 0,059 s. A implementação consulta o acesso projeto a projeto; não há SLA aprovado nem autorização para mudar esse produto nesta tarefa.

A conclusão agora tem UI com pai/filho externo (inclui payload confirmDescendants=true) e API com neto/segunda tarefa, preservando toda a prova de histórico e localização. As navegações/recargas dos casos anteriores também aguardam requisições novas e hidratação. As asserções originais foram mantidas. O spec aplica orçamento de 20 segundos.

Reset inicial guardado de prisma-dev com SQL versionado: 40,67 s, 28 migrations existentes e um projeto seed. Após o reset: focal da conclusão dividida 5/5 em 36,29 s; todo o L06 desktop/mobile 25/25 em 164,98 s, máximo 10,51 s. As três repetições finais seguem no mesmo banco, sem resets intermediários; outra recriação antecede a suíte completa. Nenhuma produção/QA alterada.

## Barreira final de navegação

A repetição intermediária após reset terminou com 112 normais, 18 falhas esperadas L08 e três falhas de navegação (1002,41 s). O listener anterior podia aceitar um refetch do documento anterior e receber resposta nula ao recarregar. A versão final espera requestfinished, rejeita requisições iniciadas antes dos headers do documento novo e mantém 200 obrigatório para cada caminho. Não ignora respostas HTTP de erro.

Somente os specs L06/L08 usam fixtures/functional.ts: a folha de Google Fonts falha no servidor e é substituída por CSS vazio no headless para remover a dependência externa de tipografia. API, ativos locais, scripts, banco e ações são reais; QA visual/instância QA não recebem esse tratamento. Diagnóstico de 31 reproduções não registrou erro de JavaScript; não foi comprovado crash de produto.

Focal da barreira final: 19/19, três repetições desktop/mobile; tempo 203,35 s e máximo 17,80 s. As verificações finais completas seguem no relatório L08.

Referência técnica: [eventos e timings de Request](https://playwright.dev/docs/api/class-request#request-timing) e [ciclo de navegação](https://playwright.dev/docs/navigations#navigation-events). Consultados também via Context7 /microsoft/playwright, sem credenciais.


## Divisões finais e sincronização por documento

A repetição intermediária de 133 execuções teve 112 aprovações normais, 18 falhas esperadas L08 e três timeouts L06 (1037,96 s). A criação/renomeação/exclusão de coluna e o cancelamento/confirmação de exclusão de quadro foram separados: o L06 tem agora 15 casos por dispositivo, mantendo as provas de persistência e transferência. A verificação imediata do seletor após “Ir para o quadro” disputava a lista ainda em “Carregando quadros...”; o teste aguarda a URL nova e então comprova seleção e dados após recarga.

A barreira observa o evento de início da navegação principal e guarda as referências das requisições iniciadas pelo documento novo. Somente requestfinished dessas referências pode satisfazer a espera, com GET 200 obrigatório; um refetch antigo não entra. Isso substitui a comparação entre timings de requisições distintas, sem aumentar timeout nem ignorar erro HTTP. As fontes externas têm fallback somente nos dois lotes funcionais. Rodadas diagnósticas anteriores com banco acumulado não foram consideradas prova final de estabilidade; resultados completos serão anotados abaixo e no relatório L08.


## Ajuste final de orçamento

A repetição conjunta de 151 execuções terminou com 131 aprovações normais, 18 falhas esperadas L08 e dois timeouts L06, em 1429,96 s. Exclusão de quadro chegou a passar em 20,04 s; foram separados o fluxo de recusa sem destino e a confirmação. As recusas D62 de API e o guard da UI também foram separados, resultando em 17 casos L06.

Focal das quatro divisões: 23/25 em 299,26 s; os dois timeouts ainda eram da confirmação com múltiplas navegações. Focal seguinte sem a recarga redundante: 7/7 em cerca de 102 s, máximo 16,62 s. A repetição posterior foi interrompida somente no runner próprio após dois novos timeouts dessa navegação (638,00 s); não foi considerada aprovação. Nenhum segredo foi encontrado nos logs locais da tarefa.

O caso final de confirmação segue a seleção do destino que a própria UI faz após DELETE. Mantém a ausência da opção do quadro excluído, GET 404 do quadro, IDs/BoardId/StageId/CompletedAt das tarefas consultados novamente no servidor e os títulos visíveis no destino. Não força novas navegações redundantes; a prova de persistência é a leitura real da API, não somente o fechamento do modal. Criação e cancelamento de quadro e CRUD de colunas conservam suas recargas.

A barreira de navegação registra referências de requests após a navegação principal e aceita requestfinished/200; falhas de transporte são propagadas com pathname/motivo, sem headers. Navegação e respostas são aguardadas conjuntamente para que a rejeição de uma delas seja sempre observada. Sem retries nem aumento do orçamento de 20 s. Os timeouts anteriores sem etapa de erro não permitiram estabelecer uma única causa de transporte ou crash; a latência no banco histórico foi medida e permanece sugestão de produto, sem correção de escopo nesta tarefa.
