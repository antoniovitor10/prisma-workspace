# Tarefa 6 — revisão L06

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
