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
