# Oficina — regressão integrada C1 a C6

Estado da fonte atual0a6da1d:CI233PASS/40skips e focal estrito local3PASS. Último completo local anterior232PASS/1FAILbootstrap/40skips; causa não comprovada, sem alegar correção. C4posterior27PASS. Quatro falhas esperadas de horas permanecem lacunas reais.

Base a2797a7; PR 27 em rascunho. Fonte de produto c758a98, ajuste semanal transportado em 8c2589e; d21cf2e acrescentou a evidência semanal. Os relatórios qa-1 a qa-6 detalham os recortes e suas contraprovas. Sem produção ou migration nova. REVISAO e QA oficiais: pendentes.

## Resultado atual: ainda há falhas

A execução qa-full-regression-final encerrou em 20/10/2026 às 15:58 UTC: 230 aprovados, três falhas inesperadas, 40 skips existentes, em 28,9 minutos. As quatro falhas esperadas herdadas de B07 e sobreposição permanecem contadas pelo Playwright entre os aprovados; são lacunas conhecidas, não correções deste goal.

As três falhas inesperadas foram document-alignment: hierarquia e dependência no desktop, participantes no mobile. Os artefatos mostram que o resolvedor procurou INFRACOES e caiu em projects[0], embora o seed atual seja DEMO. No desktop, escolheu um projeto L08 com uma única tarefa, sem filha ou segundo alvo; no mobile a atribuição recebeu400 por falta de acesso ao projeto escolhido. A regra de autorização está correta e será preservada.

A correção da seleção foi validada na branch de estabilidade e transportada como8e47329. O seletor explícito prefere DEMO, aceita INFRACOES como legado e falha se nenhum seed existir. Contraprova unitária real: três falhas e dois controles aprovados com a seleção antiga; cinco aprovados após restauração. Após o transporte, tipos forçados, lint, build e tipos E2E passaram; novo frontend completo178 aprovados em46 arquivos,196,47 segundos. O focal qa-canonical-seed-focal aprovou17 casos, incluindo setup, em2,1 minutos, cobrindo todos os casos document-alignment nas duas larguras. O novo full está enfileirado; não declarar verde integral.

## Evidências anteriores preservadas

- Primeiro full combinado: 223 aprovados, oito falhas, 40 skips. Diagnósticos e correções de resposta L07, erro manual L08, popup D13 e alcance semanal estão nos relatórios dos grupos.
- Focal qa-eight-failures-fixed: nove aprovados, incluindo setup, em 1,5 minuto.
- D13: contraprova com duas falhas mobile e três controles; versão corrigida com cinco aprovados. Capturas antes/depois abertas.
- Semanal: contraprova com uma falha mobile e dois controles; versão corrigida com três aprovados em 27,6 segundos. Sete dias exercitados com cliques comuns, diário e justificativas; capturas abertas.
- Backend completo: 724 aprovados e 33 skips existentes. Frontend completo: 173 aprovados em 45 arquivos. Tipos forçados, tipos E2E, lint e builds aprovados antes desta execução.
- A contagem antiga de controles foi retificada para 25 aprovados e duas falhas D13; o retorno0 do coordenador não era prova de sucesso. A propagação de falhas foi corrigida e os logs nativos prevalecem.

## Autorrevisão e auto-QA

Autorrevisão identificou o erro de seleção de fixture e bloqueia o fechamento integral. Auto-QA dos recortes C1 a C6 possui provas nas duas larguras, conforme relatórios próprios. Não substitui revisão e QA oficiais. Riscos conhecidos de horas, cancelamentos de requisição e limitações visuais adjacentes continuam registrados nos grupos; nenhum prazo, retry, cota ou asserção foi reduzido.


## Atualização de auto-QA — 20/10/2026 16:22 UTC

O focal novo17PASS ocorreu no mesmo banco QA da execução que falhou, sem recriar banco ou enfraquecer hierarquia, relacionamento ou autorização. O seletor explícito resolveu os três casos observados e os demais controles desse arquivo. O último full continua230/3/40 até que a nova execução integral termine. Nenhum skip ou falha esperada novo foi introduzido.


## Nova rodada local e isolamento de sincronização — 20/10/2026 18:23 UTC

qa-canonical-seed-full encerrou230PASS/3FAIL/40skips em26,6min. Os três casos de document-alignment anteriormente corrigidos passaram. As novas falhas foram board-stage-sync desktop/mobile (o primeiro quadro compartilhado já não tinha tarefa fora da conclusão) e C6mobile com quadro ainda em carregamento aos5s. Não se apresenta esta execução como verde.

A correção do teste de sincronização vem do PR30:b52addd, transportada comoe9023e7. Cria fixture exclusiva e confere categoriaDone5, stageId e completedAt pela API, além dos asserts originais no drawer/lista/reload. Contraprova própria do PR30:2FAIL/1setup, esperado Concluído/recebido A fazer; restaurado3PASS. Não houve alteração adicional de produto. Lint e tipos explícitos E2E desta branch aprovados. Novo focal/full qa-isolated-sync-* aguarda trava global. Frontend178PASS/46arquivos e backend724PASS/33skips permanecem da mesma versão de produto.

C6:diagnóstico passivo sem alterar prazo/asserção teve3PASS/22,8s. Reativação retornou estágios em125–140ms e itens em169–170ms, todos200. Isso não explica retroativamente o timeout anterior. Log/snapshot anterior e métricas numéricas posteriores preservados; nenhuma correção especulativa em cache/API.

CI39457139195 de105188b:233PASS/40skips, cinco jobs aprovados/deploy ignorado. Nova CI de e9023e7 em andamento. Quatro falhas esperadas herdadas B07/overlap continuam lacunas conhecidas, não funcionalidades corrigidas. Autorrevisão e auto-QA focais registradas; suítes finais ainda pendentes. Migration:não. REVISAO e QA oficiais:pendentes.


## Fechamento da revisão estrita — 2026-10-20T21:00:20.158921+00:00

## Completo local antes da ordem estrita de confirmação — 20/10/2026

qa-isolated-sync-full:232 aprovados/40skips existentes/1 falha inesperada,30,9min. Saída nativa1, início19:14:26UTC/fim19:45:23UTC. Fonte públicaa0a4522/códigoe9023e7, ainda com consulta da entidade antes da recarga no teste de sincronização. Esta execução não valida a revisão estrita que move essa consulta para depois da leitura recarregada.

Única falha inesperada:D06 campos de projeto, desktop, em authenticatedGoto, antes dos campos. main não existiu nos5s. Captura19:24:02UTC inteiramente branca, aberta; error-context sem snapshot de elementos. Não é prova de defeito dos rótulos. Janela±30s:seis amostras load1m17,91–21,16. Log Vite19:23–19:24 contém cinco cancelamentos SignalR, sem erro de transformação nessa seleção. Nenhum desses dados determina a causa do bootstrap. Faltam eventos de rede/pageerror do navegador original.

Hipóteses por ordem:1)entrada/módulos ou primeiro commit React ainda pendentes;2)exceção antes de montar a aplicação;3)carregamento de dados/organização antes de main, menos consistente com a imagem porque o provider normalmente mostra texto. Não afirmar causa nem corrigir por suposição. Diagnóstico privado repete somente D06projeto/equipe nas duas larguras, mesmas asserções/5s, acrescentando metadados de DOM/rede/console sem payloads. Tipos aprovados; nativo ainda enfileirado.

Os dois casos de sincronização e os dois de C6 passaram neste completo. Isso não explica o timeout C6 anterior nem certifica a ordem estrita posterior. Quatro falhas esperadas herdadas de horas (B07/relatórios e sobreposição, duas larguras) continuam lacunas reais e estão incluídas na contagem nativa de232 aprovados; não são novas correções ou novas falhas inesperadas.

## Controle posterior de D06 — escopo real C4

O filtro D06 também casou com o caminho do arquivo privado, por isso o lote executou C4 inteiro:27PASS/0FAIL em2,0min,20:01:50–20:03:52UTC, fonte de produto inalterada. D06projeto e D06equipe passaram nas duas larguras e suas quatro capturas foram abertas. Não foram cinco testes. Nenhuma asserção/prazo foi alterado.

A instrumentação usava nomes project/team; os casos seguintes sobrescreveram project. Esses arquivos foram renomeados final-c4-case e não são usados para atribuir rede/DOM ao D06projeto. Os arquivos team correspondem ao D06equipe, com main presente e sem pageerror, mas não explicam a página branca anterior no projeto. Falha original segue sem causa comprovada; não alterar produto por suposição nem chamar o completo local de verde. Artefato de escopo:Root.runlogs/oficina/d06-bootstrap/telemetry-scope.json.

## Controle estrito local atual

qa-sync-strict-focal, fonte0a6da1d:3PASS/21,5s, saída0,20:55:48–20:56:12UTC. Tipos E2E/lint aprovados. Capturas atuais1440x900/390x844 abertas:Concluído após recarga sem consulta à entidade antes dessa leitura, e APIconfirma stageId/completedAt depois. CI39489138429 da mesma fonte233PASS/40skips/8,1min, cinco jobs aprovados/deploy ignorado. Produto permanece sem mudança desde frontend178PASS/backend724PASS.

Ressalva vista na captura mobile:coluna Responsável mostra GUID quando o resolvedor não encontra a pessoa na lista assignableUsers. FonteKanban.tsx:1029 retorna userId nesse fallback; não foi medido se a ocorrência era transitória. Registrado como V13P2 na varredura somente leitura, sem implementar correção nessa etapa. Não certificar cobertura global de nomes do recorte C5.


C6: captura da falha anterior em17:15:42UTC. Janela de30s, seis amostras do monitor próprio:load1m mínimo12,31/máximo13,69; amostra mais próxima17:15:45 com13,51. As amostras estão abaixo do limite de início14, mas não medem latência de requisição nem provam causalidade. Artefato privado c6-failure-host-load.json.

Hipóteses por ordem de investigação:1)GET de estágios/itens pendente ou resposta lenta, inclusive espera SQL/contenda do host; falta telemetria da requisição original.2)resposta já recebida mas reconciliação de loading/seleção ainda pendente; falta sequência de estados original.3)seleção/cache ou permissão incorreta, menos apoiada porque o snapshot mostra o quadro criado e carregamento, sem mensagem de acesso; não foi excluída por telemetria do caso original. Diagnóstico posterior3PASS com200/125–170ms confirma somente aquela execução. Não criar correção especulativa nem ampliar5s.


Gates atuais da versão de produto:frontend178PASS/46arquivos; npx tsc -b --force, lint e build aprovados; backend724PASS/33skips existentes e build aprovado. A revisão0a6da1d modifica somente a ordem de uma verificação E2E. Seus tipos explícitos e lint passaram; focal local estrito3PASS/21,5s e CI completo39489138429 com233PASS/40skips/8,1min, cinco jobs aprovados e deploy ignorado. As quatro falhas esperadas herdadas de horas estão incluídas na contagem nativa e não equivalem a quatro funcionalidades corrigidas.

O último completo LOCAL de toda a branch foi232PASS/1FAIL/40skips, fonte anterior à ordem estrita; não será apresentado como verde nem como validação de todo o código E2E atual. O completo atual é o CI, acompanhado dos focais locais. Nenhum completo local adicional foi repetido sem causa nova para buscar verde no bootstrap. A causa da ocorrência branca continua desconhecida; a escolha de manter diagnóstico sem patch especulativo está registrada em PERGUNTAS conforme o item3 do goal.

Autorrevisão:relida a mudança final de teste contra o fluxo original; a entidade só é consultada depois da UI recarregada. Contraprova compartilhada no PR30 falha com projeção antiga após APIrealDone5/completedAt; controle real passa nas duas branches. Diferenças de fonte e escopo de cada execução estão explícitas. Nenhum novo achado alto/médio confirmado no diff estrito; as limitações herdadas e V13foram registradas, sem certificar cobertura global de nomes.

Auto-QA:grupos e capturas antes/depois documentados nos relatórios qa-1 a qa-6; imagens atuais de sincronização e quatro imagens D06do controle C4abertas. A varredura final de interface é somente leitura e vive no PR29, ainda em execução/enfileirada ao registrar este fechamento. Não há integração das propostas nem teste de um produto combinado com todas elas.

Migration:não. Entrega em PR rascunho. REVISAO e QA oficiais:pendentes.
