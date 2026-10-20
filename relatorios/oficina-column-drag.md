# Oficina — TASK124: arraste de colunas

Base: a2797a71b79543cf71930d04547021564a7e4e88. Branch fix/kanban-column-drag. PR em rascunho; nenhuma integração ou publicação autorizada.

A spec aprovada boards-stages-wip e as histórias catalogadas US006/017 já contratam reordenação persistente. A implementação usa somente o endpoint existente reorderStages e a posição das colunas. O título aceita ponteiro/toque, com limiar de 8px; os botões existentes continuam acessíveis por teclado. Atualização otimista bloqueia pedidos concorrentes, restaura a ordem em falha e ignora restauração de resposta antiga depois de trocar de quadro. Cancelar o gesto não salva. Cartões mantêm seus dados.

Arquivos: Kanban.tsx, Kanban.styles.ts, ProjectKanbanDirect.test.tsx; E2E column-drag.spec.ts e fixture boards.ts. Nenhuma biblioteca nova. Nenhuma alteração de classificação, WIP, permissões, transições ou auditoria. Migration: não.

Verificação real em 20/10/2026: build backend aprovado; xUnit completo 690 aprovados/33 skips existentes; Vitest afetado17 aprovado. Contraprova com Kanban e estilos da base:4 falhas esperadas/1 controle aprovado,12 filtrados pelo comando. Restaurado:5 aprovados/12 filtrados. Vitest completo167 aprovados/41 arquivos; npx tsc -b --force, lint, build e tipos explícitos do E2E aprovados. Uma primeira falha de fixture buscava texto do cartão sem seu prefixo; corrigida para aguardar o heading real e verificar a coluna do cartão, sem retirar asserção.

E2E nativo focal e completo: pendentes na fila serializada do navegador. As capturas1440x900/390x844 ainda não foram produzidas nem inspecionadas. Não declarar validação visual ou E2E completa neste checkpoint.

Autorrevisão: sem achado alto no diff; risco médio em captura de ponteiro móvel e resposta assíncrona, cobertos por testes unitários e pendentes de prova em navegador. Alternativa de teclado preservada. A persistência continua sujeita às permissões já aplicadas pela API. Auto-QA: roteiro automatizado preparado para persistência após reload, igualdade integral do DTO do cartão, teclado, cancelamento e rollback409 nas duas larguras; execução pendente.

REVISAO e QA oficiais: pendentes.

## Oficina — continuação do CI em 20/10/2026
CI39426972004 do fe019f1:backend/frontend/imagem/governança aprovados; E2E117PASS/6FAIL/40skips. Duas falhas são arraste mobile; quatro posteriores são document-alignment. Não declarar regressão verde.
A revisão local confirmou bloqueio Nova Coluna:counter1FAIL/16filtrados e restauração17PASS; tipos/lint passaram. O runner anterior foi encerrado foraRED antes de concluir os gates completos para tratar novo achado.
Novo contrato de soltura:handler antigo dependia de último MOVE;2 testes(stale e up-only) vão demonstrar RED. Runner973474 aplica correção para coordenada final UP e distância mínima, depois19afetados/tipos/lint/build/full/E2Etypes. Alterações ainda não commitadas; não mexer durante execução.
E2E mobile inclui evidência de pointer/touch e viewport sem conteúdo de tarefa ou IDs. Espera infinita de alerta substituída por assert.poll com prazo padrão e remoção do listener para permitir cleanup quando o gesto não salvar. Causa das quatro falhas subsequentes ainda exige prova, não assumir resolvidas. Próximo:ler column-release-gates-run.log, publicar só após restauração/gates e executar focal/full nativo sobtrava. Watcher quarto agora981872.

## Correção contraprovada de soltura e concorrência — 20/10/2026

O bloqueio Nova Coluna durante reordenação foi demonstrado:retirar disabled gerou1FAIL/16filtrados; restaurado,17PASS. Há também guarda no submit, sem permitir criação concorrente durante o pedido.

Dois cenários reproduziram outra falha de contrato:o último MOVE ainda aponta à origem, ou nenhum MOVE chega antes do UP. Contra o handler anterior ambos falharam(2FAIL/17filtrados). O handler agora lê elementFromPoint nas coordenadas finais e reconhece a distância mínima entre DOWN/UP; o último target de MOVE serve só ao destaque visual. Com isso,19afetados PASS, tipos forçados/lint/build PASS. Backend permanece690PASS/33skips, sem alteração desde o gate anterior.

A hipótese de eventos touch coalescidos é compatível com a falha mobile do CI, mas ainda não chamar a causa específica daquele CI resolvida sem novo E2E. O teste registra geometria/escala/pointer/hitbooleans, sem título/IDs/segredos. O rollback passa a aguardar o mesmo alerta por expect.poll padrão, em vez de Promise infinita, preservando mensagem, quantidade de requests e igualdade da ordem. Isso permite cleanup quando não ocorre o alerta. Os quatro erros posteriores de document-alignment do CI continuam registrados e precisam de reconfirmação; não tratá-los como corrigidos por suposição.

Vitest completo desta revisão, tipos explícitos do E2E e nativo focal/completo seguem pendentes no runner973474 e quarto coordenador981872. O completo167 anterior é da revisão anterior; não usar como gate desta. Nenhum timeout/retry/skip/cota novo, migration ou alteração de workflow. Revisão e QA oficiais pendentes.

## Causa geométrica do E2E mobile confirmada — CI39438607088

Head d480de2:CI backend/frontend/imagem/governança aprovados; E2E121PASS/2FAIL/40skips. As quatro falhas anteriores de document-alignment não se repetiram; a causa exata dessas quatro não foi capturada, não atribuir correção global por hipótese. Restaram os dois casos mobile de colunas.

A evidência de pointer mostrou todos os oito MOVE, UP e lostcapture; início x94,398/y632,625 sobre o título, escala1/DPR2,75. O caminho passou pelo alvo emx346,05(overTarget=true), mas terminou emx382 com overTarget=false no MOVE e no UP. Portanto coalescência não explica estes registros do CI. A correção do handler continua válida para os dois cenários unitários contraprovados, mas não foi a causa específica desse ensaio.

O roteiro calculava o fim pelo centro do retângulo completo, limitado só ao viewport. A coluna é parcialmente recortada pelo contêiner horizontal; o limite do viewport podia ficar fora dela. Agora o fim é o centro da interseção visível com viewport e ancestrais que recortam overflowX. O teste exige largura visível positiva e elementFromPoint pertencente ao alvo antes do gesto, além de todas as mesmas assertivas de persistência/reload/DTO/teclado/cancelamento/rollback. Sem force, clique sintético ou prazo/retry novo. Lint do arquivo aprovado; tipos explícitos corrigidos no processo1409692 e novo E2E aguardam a fila.

O produto de d480de2 tem Vitest completo169PASS/41arquivos em203,56s,19afetadosPASS, tipos/lint/build e tipos do E2E anterior PASS. Este commit altera somente o roteiro e documentação; não certifica o novo E2E sem executá-lo. Revisão e QA oficiais pendentes.

## CI da geometria corrigida aprovado — 20/10/2026

CI39441941041, head25f12cd:backend,frontend,imagem,governança e E2E success; deploy skipped. Síntese nativa remota123PASS/40skips em4,5min, sem failed/flaky na síntese. Telemetria de toque:endx357/y632,625; pointerup com overTarget=true nos casos de persistência e rollback; cancelamento teve pointercancel, sem UP. Mesmas assertivas de ordem/DTO/teclado/rollback preservadas.

A falha antiga de fimx382fora do alvo e os dois CIs anteriores permanecem como evidência de diagnóstico. O CI aprovado não substitui o focal local sob a trava nem a inspeção das capturas. Tipos explícitos frescos do helper e nativo local continuam enfileirados; produto possui local169PASS/41arquivos,19afetados e tipos/lint/build aprovados. Revisão e QA oficiais pendentes.
