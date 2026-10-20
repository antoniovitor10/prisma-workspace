# Oficina — TASK124: arraste de colunas

Base: a2797a71b79543cf71930d04547021564a7e4e88. Branch fix/kanban-column-drag. PR em rascunho; nenhuma integração ou publicação autorizada.

A spec aprovada boards-stages-wip e as histórias catalogadas US006/017 já contratam reordenação persistente. A implementação usa somente o endpoint existente reorderStages e a posição das colunas. O título aceita ponteiro/toque, com limiar de 8px; os botões existentes continuam acessíveis por teclado. Atualização otimista bloqueia pedidos concorrentes, restaura a ordem em falha e ignora restauração de resposta antiga depois de trocar de quadro. Cancelar o gesto não salva. Cartões mantêm seus dados.

Arquivos: Kanban.tsx, Kanban.styles.ts, ProjectKanbanDirect.test.tsx; E2E column-drag.spec.ts e fixture boards.ts. Nenhuma biblioteca nova. Nenhuma alteração de classificação, WIP, permissões, transições ou auditoria. Migration: não.

Verificação real em 20/10/2026: build backend aprovado; xUnit completo 690 aprovados/33 skips existentes; Vitest afetado17 aprovado. Contraprova com Kanban e estilos da base:4 falhas esperadas/1 controle aprovado,12 filtrados pelo comando. Restaurado:5 aprovados/12 filtrados. Vitest completo167 aprovados/41 arquivos; npx tsc -b --force, lint, build e tipos explícitos do E2E aprovados. Uma primeira falha de fixture buscava texto do cartão sem seu prefixo; corrigida para aguardar o heading real e verificar a coluna do cartão, sem retirar asserção.

E2E nativo focal e completo: pendentes na fila serializada do navegador. As capturas1440x900/390x844 ainda não foram produzidas nem inspecionadas. Não declarar validação visual ou E2E completa neste checkpoint.

Autorrevisão: sem achado alto no diff; risco médio em captura de ponteiro móvel e resposta assíncrona, cobertos por testes unitários e pendentes de prova em navegador. Alternativa de teclado preservada. A persistência continua sujeita às permissões já aplicadas pela API. Auto-QA: roteiro automatizado preparado para persistência após reload, igualdade integral do DTO do cartão, teclado, cancelamento e rollback409 nas duas larguras; execução pendente.

REVISAO e QA oficiais: pendentes.
