# Horas-5: cronômetro único e navegação da busca por teclado

Branch `fix/time-tracking-20261015`. Correções pedidas na revisão/QA de `04740ab`: H16 médio bloqueante e QA-H08/P2. Fonte funcional `62c3c57`; jornadas E2E separadas em `66af660`/`76b9e38` e navegações/recargas independentes paralelizadas em `44b9fae`. A fila de coordenação e o handoff registram o SHA final da entrega; este documento identifica a fonte funcional/testada e a equivalência dos artefatos. Base main: `8ec6699`. Evidências exclusivamente no prisma-qa (3110/SQL3111), com banco xUnit separado. A rodada final usa Chromium em container próprio do QA, imagem oficial Playwright 1.62.1, bridge isolada, WS apenas 127.0.0.1:3115, 2 CPUs, 3 GB de RAM e 1 GB de /dev/shm, com prioridade nice 10. Os mesmos specs/configuração usam `PW_TEST_CONNECT_WS_ENDPOINT` e `PW_TEST_CONNECT_EXPOSE_NETWORK='<loopback>'`, mantendo URLs 127.0.0.1:3110; nenhum erro de rede é ignorado. [Conexão remota oficial](https://playwright.dev/docs/docker#remote-connection).

## Resultado e implementação

H16 era uma corrida entre consultar o timer aberto e inserir outro. Duplo clique/abas podiam deixar um lançamento oculto acumulando horas. `TimeEntryRepository.cs:44` centraliza início e parada em transação SQL com `sp_getapplock` exclusivo, chave derivada do usuário e parâmetros. A estratégia resiliente envolve a transação; retorno negativo do lock vira erro de domínio em pt-BR. Encerramentos, inclusão e auditoria existente são confirmados juntos, com rollback em falha. [SQL Server](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql), [EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

Na mesma tarefa, mantém o aberto mais recente dela e encerra os extras próprios; troca encerra todos os abertos próprios antes de criar o novo; parada encerra também os duplicados. Mantidos os filtros do tenant ativo/D58, dono e validação do card solicitado. Terceiros e outros tenants não são alterados. Usuário comum pertence a uma organização; este saneamento não muda o comportamento administrativo entre tenants. Horários iniciais/observações legados são preservados e os encerrados ficam corrigíveis pelo dono. Nenhuma limpeza em massa nem fechamento automático de timer esquecido.

Kanban (`Kanban.tsx:1006`) e Meu trabalho (`MinhasTarefas.tsx:97`) usam guarda imediata por ref, estado pendente e controles desabilitados. Cliques em botões desabilitados não abrem o drawer por propagação. O TaskTimePanel já tinha bloqueio de pendência. A garantia SQL continua necessária para abas/chamadas simultâneas.

QA-H08 tinha três componentes: atualizar `aria-activedescendant` não rola a lista; uma opção mobile de 223 px excedia o teto fixo de 220 px; e o próprio modal podia recortar uma opção já visível na lista. `TimeAdjustment.tsx:10/29` preserva títulos completos, usa até metade da altura dinâmica do viewport, rola a opção ativa na lista e a revela nos ancestrais com `scrollIntoView(nearest, instant)`, sem tirar o foco do campo. Enter/Escape e ponteiro permanecem operacionais. [W3C APG](https://www.w3.org/WAI/ARIA/apg/patterns/combobox/examples/combobox-autocomplete-list/#accessibility-features), [MDN](https://developer.mozilla.org/en-US/docs/Web/API/Element/scrollIntoView).

## Cobertura acrescentada

- `TimeTimerSqlTests.cs:17`: sete casos SQL reais, contextos independentes e três pares simultâneos por cenário (mesma tarefa/outras tarefas), duplicados legados, idempotência, troca/parada, isolamento, auditoria antes/depois e rollback após FK inválida injetada no repositório. Não é teste de endpoint inválido retornando 500. Conexões descartadas; dados exclusivos permanecem isolados por organização no banco dedicado, conforme fixtures SQL existentes.
- `ProjectKanbanDirect.test.tsx` e `MinhasTarefas.test.tsx`: início/parada pendentes, cliques repetidos, ausência de drawer e liberação após erro para tentar novamente.
- `TimeAdjustment.test.tsx`: ida/volta, foco, seleção, geometria local 160/0 px e invocação nativa em mocks locais dos botões; o layout real é provado pelo E2E.
- `time-tracking-concurrency.spec.ts:23/62/79`: duplo clique real nas duas telas com POST retido para observar pendência, pares HTTP reais e duas abas liberando POST juntos. Confirma um único aberto e parada sem ocultos. Dblclick nas duas viewports é gesto de mouse; não é ensaio de duplo toque físico.
- `time-tracking-fixes.spec.ts:153`: seis títulos próprios completos, limites de cada linha, rolagem/ponteiro e 60 s na tarefa selecionada.
- `time-tracking-fixes.spec.ts:193`: seis títulos próprios, última/primeira opção inteira na lista e viewport, foco preservado, Enter seleciona/grava 60 s, Escape e reabertura. Fluxos separados mantêm todas as asserções e o timeout padrão; telemetria distingue base da fixture, seis POSTs e arranjo/UI. Sem sleep, asserção opcional ou dependência de dados de outro spec.

## Verificações

Tempos de comando, salvo indicação de duração nativa. Produto inalterado desde `62c3c57`: checks do backend/frontend continuam válidos após commits exclusivamente de E2E/documentação. Imagem final exige SHA exato e comparação de DLLs/ativos com a imagem testada.

| Comando | Resultado | Tempo |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln --no-restore` | Aprovado; 0 erros/3 avisos anteriores | 86,74 s |
| `dotnet test tests/Prisma.Workspace.Tests --no-build --no-restore --logger 'console;verbosity=normal'` via preparação SQL própria | 391/391; 0 pulos | 32,23 s; 44,59 s com preparação |
| xUnit `FullyQualifiedName~TimeTimerSqlTests` | 7/7 | 17,02 s; 31,65 s com preparação |
| `npx tsc -b` | Aprovado | 89,96 s |
| `npm run lint` final | 0 erros/12 avisos anteriores | 0,56 s |
| `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` | 160/160; 42 arquivos | 326,78 s |
| Vitest focado Kanban/Meu trabalho | 18/18 | 28,63 s |
| Vitest focado TimeAdjustment final | 2/2 | 5,38 s |
| `npm run build` | Aprovado | 202,43 s |
| TypeScript explícito dos dois specs E2E, emitido em diretório ignorado | Aprovado | 5,28 s |
| `npm run e2e -- time-tracking-fixes.spec.ts --grep 'busca do ajuste mantém' --retries=0 --max-failures=1 --trace=off` | 5/5; 0 falhas/pulos/retries | 72,44 s; 69,26 s nativos |
| Foco duas abas, desktop/mobile 3x, sem retry | 7/7; maior 21,05 s incluindo fechamento/limpeza, parada confirmada até 19,39 s | 103,78 s; 101,87 s nativos |
| Docker build e health/login | Aprovado, imagem `44b9fae`; health/login 200 e manifesto idêntico ao de `62c3c57` | 110,42 s |
| `npm run e2e -- time-tracking-concurrency.spec.ts time-tracking-fixes.spec.ts --repeat-each=3 --retries=0 --max-failures=1 --trace=off` | 67/67; 0 falhas/pulos/retries, 66 limpezas sem falhas | 680,20 s; 678,39 s nativos |
| `npm run e2e -- --retries=0 --trace=off` após QA recriado | 214 aprovados / 6 pulos anteriores / 1 falha antiga Rich:63 mobile, permitida pelo plano humano; saída 1, sem falhas novas/retries | 1.834,20 s; 1.832,36 s nativos |

| Caso | Desktop, 3 rodadas (s) | Mobile, 3 rodadas (s) |
| --- | --- | --- |
| `time-tracking-concurrency.spec.ts:23` H16: duplo clique no Kanban envia um único início enquanto pendente | 11,68; 6,20; 6,01 | 8,32; 8,09; 8,01 |
| `time-tracking-concurrency.spec.ts:23` H16: duplo clique no Meu trabalho envia um único início enquanto pendente | 7,25; 4,61; 5,84 | 6,30; 9,04; 6,66 |
| `time-tracking-concurrency.spec.ts:62` H16: inícios HTTP paralelos em uma tarefa deixam só um aberto | 3,44; 3,21; 4,11 | 4,97; 4,81; 5,88 |
| `time-tracking-concurrency.spec.ts:62` H16: inícios HTTP paralelos em tarefas diferentes deixam só um aberto | 3,80; 3,23; 2,85 | 4,50; 3,94; 4,49 |
| `time-tracking-concurrency.spec.ts:79` H16: duas abas iniciando tarefas diferentes não deixam cronômetro oculto após recarga | 14,23; 10,57; 10,33 | 24,15; 13,78; 13,06 |
| `time-tracking-fixes.spec.ts:30` timer em andamento não dobra após recarregar o card | 13,44; 13,69; 11,14 | 15,91; 13,53; 13,38 |
| `time-tracking-fixes.spec.ts:55` editar lançamento no ajuste persiste horários, observação e atualiza dia e semana | 9,73; 11,12; 12,48 | 12,57; 12,40; 10,74 |
| `time-tracking-fixes.spec.ts:78` excluir lançamento exige confirmação, cancelar preserva e confirmar atualiza os totais | 6,62; 6,21; 7,64 | 12,90; 9,75; 8,03 |
| `time-tracking-fixes.spec.ts:98` buscar tarefa pelo título e número no ajuste funciona com teclado | 8,88; 8,61; 11,12 | 16,76; 10,18; 16,29 |
| `time-tracking-fixes.spec.ts:153` busca do ajuste mantém seis títulos longos legíveis e permite selecionar após rolar | 10,62; 8,24; 13,67 | 12,24; 9,76; 14,26 |
| `time-tracking-fixes.spec.ts:193` busca do ajuste mantém a opção ativa visível no teclado e grava o destino do Enter | 18,25; 10,15; 12,46 | 12,09; 11,67; 11,61 |

Na full, os 92 cenários de horas/B07 passaram: 46 de privacidade HTTP, 6 do construtor/CSV, 10 de concorrência, 12 de correções e 18 de follow-up. Foram 100 limpezas, 216 ações e 0 falhas (maior 4,407 s). A falha de descrição rica foi o timeout conhecido ao reabrir o backlog na linha 63; o arquivo é idêntico à main e não foi alterado, conforme determinação humana. As seis condições de skip são anteriores; não houve novo skip, test.fail ou retry.

## Tentativas anteriores preservadas

1. Primeiro Vitest de telas: 17/18. O segundo clique em botão desabilitado abria o drawer pelo card. Corrigida a propagação; novo foco 18/18 e integrais 160/160 passaram sem aumentar timeout.
2. Imagem anterior `04740ab`: setup aprovado e duas falhas esperadas H08, nas duas larguras, após filtro 200/ARIA correta; 41,54 s. Na candidata `0a745e9`: 40 aprovados/1 falha mobile/20 não executados (maxFailures=1), 381,72 s. Geometria 223 px/220 px comprovou a capacidade insuficiente; a full dependente não iniciou.
3. Imagem `9fc25f5`: setup aprovado/duas falhas de recorte externo, 48,20 s nativos. Opção cabia na lista, mas ultrapassava a viewport (desktop 595..780/720 px; mobile 561..784/727 px). Corrigida a revelação nos ancestrais, mantendo as mesmas condições de visibilidade.
4. Carga do servidor 37–42/8 vCPUs e swap cheia: cancelei somente meu Docker build (47,12 s/130), antes de trocar app/resetar banco. Retomada após recuperar memória; tentativa de preaquecer targets não reduz paralelismo neste Dockerfile, que já depende do front antes do publish .NET. Justificativa corrigida expressamente na fila, sem alteração de Dockerfile/CI/servidor. Os targets locais geraram custos de exportação extras, não um limitador de CPU.
5. Imagem `62c3c57`, fluxo combinado: setup/mobile aprovados, desktop timeout global de 30 s; 79,83 s nativos/83,26 s de comando. Mobile 32,66 s inclui cleanup; captura desktop mostra a última opção inteira e foco no campo, mas não substitui aprovação. Separadas legibilidade/ponteiro e teclado/Enter; nenhum aumento de timeout. Novo foco 5/5 passou (máximo 19,33 s).
6. Preview `66af660` interrompido pelo executor ao detectar ordem errada no teste: Adicionar fecha o modal. Setup/legibilidade desktop passaram; teclado foi interrompido no arranjo e dois mobile não executados. Houve POST 500 com ArgumentException de participante ausente durante a interrupção; registrado sem tratá-lo como aprovação ou atribuir causa não demonstrada. Ordem corrigida em `76b9e38` para Escape/reabertura precederem a gravação, sem mudar produto. Preview corrigido 5/5 passou.

7. Primeiro focal 67 na imagem `76b9e38`: 7 aprovados/1 falha de transporte no GET de JS (`net::ERR_NETWORK_CHANGED`)/59 não executados, 87,06 s nativos; limpeza sem falhas. App continuava saudável, com mesmo início e SHA. Sem alteração de código/asserts/helper/retries; rodada integral repetida com dados próprios no mesmo banco. A full dependente desta tentativa não iniciou. A repetição integral também encontrou o mesmo erro de transporte: 16 aprovados/1 falha/50 não executados, 226,41 s nativos; app continuava saudável. O navegador foi então isolado por rede com suporte nativo do Playwright, sem mudar testes/asserts/retries. Primeira preparação do container falhou ao resolver dependência montada do host, antes de qualquer teste; instalação exata pela forma oficial npx corrigiu o setup, e sondas health/JS retornaram 200.

8. Primeiro focal isolado: 38 aprovados/1 timeout mobile de duas abas/28 não executados, 605,25 s nativos, sem erro de rede e sem falha de limpeza. A parada pela UI não concluiu no orçamento de 30 s; houve erro secundário de vídeo/contexto após timeout. Medição da cgroup registrou 3,7% dos períodos limitados por CPU, sem provar causa única; limite de 2 CPUs foi mantido. `44b9fae` paraleliza somente as duas navegações e as duas recargas independentes, preservando todos os asserts/barreiras/parada e 30 s. Foco de 6 repetições mais setup passou 7/7, com métricas por fase; a full dependente anterior não iniciou.

Logs/JSON de todas as tentativas desta entrega estão preservados em `.runlogs/horas-5`; capturas recentes usam diretório próprio por execução. Parte das capturas antigas do diretório padrão foi sobrescrita.

## Limites e próximo passo

Backups QA com CHECKSUM/VERIFYONLY e arquivos com modo 600 antecedem cada reset pelo SQL versionado/seed Development oficial. Sem reset entre as três repetições do foco. Banco de unidade separado; prisma-dev e produção intocados. Login QA no arquivo ignorado `.env.qa.login` da raiz canônica, modo 600; senha não incluída.

Nenhum arquivo de Migrations nem ModelSnapshot difere de origin/main (`8ec6699`). Não foi criada migration nem alterada a estrutura de auditoria. Conferência por `git diff origin/main..HEAD --name-only -- ":(glob)**/Migrations/**" ":(glob)**/*ModelSnapshot*"`, sem saída.

H15 (manuais simultâneos), custo de escopo/N+1 do C6 e perfil Identity global sem vínculo permanecem nas ressalvas anteriores. Cronômetro esquecido segue para decisão do PO. Não verificados: Firefox/WebKit, leitor de tela ou duplo toque físico, CI/produção e duas APIs fisicamente implantadas. A exclusão foi exercitada em SQL real com conexões/contextos independentes.

Revisão do Prumo e QA da Lupa devem aprovar o mesmo SHA final antes de PRONTO-PROD. PUBLICADO é exclusivo do Maestro e precede C1–C6.
