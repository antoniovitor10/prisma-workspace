# Oficina — regressão integrada C1 a C6

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
