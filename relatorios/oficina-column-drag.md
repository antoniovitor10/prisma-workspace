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
