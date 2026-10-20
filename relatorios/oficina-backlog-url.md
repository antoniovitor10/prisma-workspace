# Oficina — TASK213: estado do Product Backlog na URL

Base origin/main a2797a71b79543cf71930d04547021564a7e4e88. Branch fix/backlog-url-state. Spec aprovada backlog.md, critérios23/24, histórias catalogadas US-B-001-012/013/014. Checkpoint em PR rascunho; validação nativa e suítes finais ainda pendentes.

Mudanças: codec BacklogUrlState e hook useBacklogUrlState; BacklogPlanner usa URL para filtros, agrupamento, seleção, colapso, sprint escolhida, item aberto e posição vertical. ProjectWorkspace preserva a query na alternância Backlog/Quadro. Namespace bl.* separa o estado do backlog e conserva parâmetros externos. Alterações do mesmo evento são compostas antes de substituir a URL; item/filtros/scroll usam o mesmo caminho. Voltar/avançar restaura o estado a partir da URL. IDs de seleção não retornados pelo projeto não participam de ações em massa. Sprint padrão é derivada dos dados atuais, sem navegação extra.

Não há mudança de API, schema, autorização, ordenação, workflow ou dependência. Pesquisa textual informada pelo usuário aparece na URL compartilhada; não são incluídos descrições, nomes oriundos da API, tokens ou credenciais. Escolha registrada em PERGUNTAS. A validação/autorização de dados continua no backend.

Resultados reais até este checkpoint: tipos forçados, lint e build iniciais aprovados; codec/histórico/visão ampla14PASS. Com integração de filtro/item/seleção/colapso,16PASS. Contraprova com Planner da base:2FAIL esperados. Primeira restauração15PASS/1FAIL em carregamento da sprint; removida navegação automática para sprint padrão e derivados16PASS, contraprova2FAIL e restauração16PASS. Build backend aprovado,3 avisos existentes/zero erros. Nenhuma migration nova.

Uma rodada completa antiga teve170PASS/1FAIL e um erro updateParams ausente: teste e hook foram editados durante a execução, tornando a versão carregada inconsistente. Rodada preservada e invalidada como gate final; não apresentar como verde ou falha definitiva da revisão atual. Uma rodada nova com fontes estáveis está enfileirada para afetados, contraprova de composição no mesmo evento, restauração, tsc -b --force, lint, build e Vitest completo. xUnit/seed e tipos explícitos de E2E ainda pendentes; carga compartilhada acima14 adia novas execuções pesadas.

E2E nativo preparado: persistência de seleção e colapso após reload, posição vertical com20itens reais, alternância contextual e voltar/avançar, filtros e item aberto por URL direta, nas larguras1440x900/390x844. Só será enfileirado após gates locais verificados; capturas ainda não produzidas.

Autorrevisão em andamento: maior risco na sincronização entre URL/histórico/consulta assíncrona e na restauração de rolagem após render. Composição de atualizações possui teste específico; a prova em navegador ainda falta. Auto-QA: roteiro preparado, execução pendente. Migration:não. REVISAO e QA oficiais:pendentes.

## Checkpoint compilável — 20/10/2026

Backend próprio:690PASS/33skips existentes; build aprovado/3avisos existentes. Tipos explícitos E2E aprovados. Na revisão atual e estável:16PASS afetados; contraprova da composição1FAIL/6controles; restaurado16PASS; npx tsc -b --force aprovado. Lint/build completos e Vitest completo desta revisão ainda na fila, respeitando carga<=14. Os resultados iniciais aprovados desses comandos pertencem à revisão anterior e não serão apresentados como fechamento da atual. O E2E focal e completo aguarda todos esses gates no coordenador privado.

## Gates locais finais aprovados — 20/10/2026

Revisão fixa30aa487:Vitest completo171PASS/43arquivos em219,82s, nenhum erro não tratado. Afetados16PASS, contraprova composição1FAIL/6controles, restaurado16PASS. npx tsc -b --force, lint e build finais aprovados. Backend690PASS/33skips existentes e tipos explícitos do E2E aprovados. As falhas/rodadas invalidadas anteriores permanecem acima como histórico, sem serem usadas para certificar esta revisão.

O coordenador confirmou todos os gates e enfileirou backlog-url-focal sob a trava global. E2E completo e inspeção das capturas ainda pendentes; não constitui ENTREGA final da task. REVISAO e QA oficiais:pendentes.

## Diagnóstico do CI automático — 20/10/2026
CI39431928513, head30aa487:backend/frontend/imagem/governança aprovados; E2E121PASS/2FAIL/40skips. Os dois casos de seleção/colapso/rolagem/reload/idaevolta passaram, incluindo mobile. Os dois casos de URLcomitem falharam ao buscar por role o campo de pesquisa no fundo enquanto Dialog.Content estava aberto. O Radix oculta esse fundo da árvore acessível; o mock unitário do Drawer não simula essa semântica. Não é justificativa para usar includeHidden ou force.
O roteiro foi corrigido para verificar pesquisa/lista acessíveis antes da abertura, navegar por URLcomitem e conferir heading/reload, fechar o modal pelo controle acessível e verificar os mesmos valores/params novamente. As assertivas foram preservadas e acrescentada verificação de fechamento. Nenhum componente de produto foi alterado; nenhum prazo/retry/skip novo. Tipos explícitos do teste corrigido e execução nativa ainda pendentes. O CI automático seguinte39434486868, b18cc9d apenasdocs, falhou na mesma causa conhecida; não foi iniciado rerun manual.

## Nova falha local de histórico — 20/10/2026

O CI39437567918 do68b7d92 passou todos os gates, com E2E123PASS/40skips, sem falha/flaky na síntese e deploy skipped. O focal local seguinte, entretanto, teve4PASS/1FAIL:na etapa goBack do mobile a URL voltou a ser /backlog?bl.y=600 em vez de /boards. A exigência da rota permanece exata; não ampliar prazo nem substituir a asserção.

Mecanismo identificado:um frame do listener de scroll ainda pode executar entre o pop do histórico e o cleanup React. O setter relativo daquela renderização repõe a rota/query antiga. Foi extraído o assinante BacklogScroll com verificação da URL dona antes de agendar e dentro do callback; quatro testes cobrem controle normal, frame pendente após trocar rota, evento após navegação antes de desmontar e outra query na mesma rota. A contraprova remove essas verificações mantendo a gravação antiga; aguardando CPU, depois afetados/tipos/lint/build/full da revisão e novo focal/full. Nenhuma asserção E2E foi retirada ou afrouxada. Produto atual com esta correção ainda não publicado nem certificado pelos resultados anteriores.

## Guarda de localização contraprovada — 20/10/2026

Remover as verificações de dono do listener reproduziu3FAIL/1controle:callback devolve a rota antiga após trocar para quadro, agenda scroll depois de sair antes do cleanup e apaga a query de outra entrada na mesma rota. Restauradas as verificações,20afetados PASS e npx tsc -b --force PASS. A restauração da posição também confere a localização antes de mexer no scroll. A asserção E2E goBack→boards continua idêntica.

O roteiro registra apenas tipo push/replace/pop, classe boards/backlog/other, índice numérico e tempo relativo; não copia URL, IDs, títulos, filtros ou credenciais. Tipos explícitos desta instrumentação serão conferidos pelo coordenador1362595 após history-full-vitest0 e antes do novo focal/full. Lint/build/full desta revisão seguem no runner1291881, com fontes estáveis; não reutilizar os171PASS anteriores para fechar a correção nova. Backend690PASS/33skips permanece sem alteração de código. Sem migration, dependência, timeout/retry/skip/cota ou produção. Revisão e QA oficiais pendentes.

## Gates locais da correção de histórico aprovados — 20/10/2026

Revisão6fd3b0b:Vitest completo175PASS/44arquivos em211,16s; afetados20PASS, contraprova3FAIL/1controle. Tipos forçados, lint e build aprovados. O coordenador1362595 conferiu os tipos explícitos do E2E instrumentado(exit0) e enfileirou backlog-url-history-focal; se passar, executará o completo. Sem afrouxar a exigência goBack→boards ou aumentar prazo.

O CI123PASS/40skips de68b7d92 continua sendo da revisão anterior, e o focal local4/1 motivou a correção atual. Não usar esses123 como validação do guard novo até nova execução. Backend690PASS/33skips permanece com fonte idêntica. Capturas e novo resultado nativo ainda pendentes. Revisão e QA oficiais pendentes.


## Novo focal local — 20/10/2026 15:30 UTC

A execuçãobacklog-url-history-focal encerrou3PASS/2FAIL:desktop voltou ao backlog em vez deboards; mobile perdeu a rolagem600 e ficou0. Não houve suíte completa após falhar. A guarda6fd3b0b tem contraprova unitária válida, mas não resolveu esse fluxo nativo.

A telemetria desktop registroureplace backlog idx0;push boards idx1;replace backlog idx1;pop backlog idx0. Nenhum push de retorno ao backlog foi registrado. O teste clicava no mesmo alternador assim quetoHaveURL(boards) passava, antes de exigir qualquer elemento da tela do quadro. No ReactRouter instalado, useLinkClickHandler calcula replace por createPath(location)===createPath(path); antes do commit React, location ainda pode ser backlog. A nova hipótese apoiada nessa sequência é clique na árvore anterior durante transição, não mera demora do banco. O E2E agora exige uma coluna[data-stage-id] visível após ir ao quadro e antes de voltar. Exigências originais de histórico/rolagem/seleção/colapso preservadas, sem prazo maior. Nova tipagem e execução focal/completa enfileiradas; não afirmar correção comprovada antes dos resultados.
