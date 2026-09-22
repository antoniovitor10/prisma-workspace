# SPEC-INDEPENDENT-BOARD-COLUMNS

**Status:** approved

**G-SPEC:** aprovado pelo PO em 2026-09-22: "pode implementar as correções agora como eu pedi", em resposta à apresentação deste contrato. O inventário e o mapeamento físico continuam obrigatórios antes da migration de produção.

## Autoridade e limites

História: `stories/independent-board-columns.md`. D89 registra a escolha humana por colunas independentes. A escolha não aprova automaticamente os detalhes de migração abaixo. Esta proposta substitui as cláusulas de fluxo compartilhado de `SPEC-BOARD-AS-VIEW`; não reativa quadros transversais, WIP ou atribuições N:N.

## Contrato proposto para aprovação

1. Cada quadro pertence a um projeto e possui suas próprias colunas, com nome, ordem e categoria explícitos.
2. Cada tarefa pertence a um único quadro e ocupa uma coluna desse quadro; nenhuma duplicação de tarefa ou posição paralela.
3. Novo quadro pode receber estrutura básica ou cópia das colunas de outro quadro do mesmo projeto. A cópia não inclui tarefas e permanece independente.
4. A aba Kanban abre diretamente o quadro selecionado; sem quadros, oferece criar o primeiro. A possibilidade de operar Kanban sem quadro da D83 deixa de valer neste modelo proposto.
5. Mover tarefa entre quadros exige destino e coluna explícitos, dentro do mesmo projeto e com autorização. Não inferir conclusão pela ordem ou pelo nome da coluna.
6. Excluir quadro com tarefas exige transferência explícita; excluir coluna ocupada exige coluna de destino válida. Operações atômicas e auditadas, sem perda de tarefas.
7. Equipes e permissões permanecem no projeto. Não reintroduzir WIP nem alterar a relação Sprint/Projeto 1:N.

## Migração proposta

- Migration incremental, nunca apagar migrations aplicadas ou tratar a base atual como descartável.
- Inventariar primeiro quadros, tarefas, colunas e referências históricas. `WorkItem.BoardId` existente é candidato a origem, não prova suficiente sem validar a consistência.
- Preservar o quadro atual de cada tarefa quando consistente; copiar a estrutura compartilhada para cada quadro e mapear as tarefas para suas colunas equivalentes sem mudar categoria, conclusão ou ordenação relativa.
- Não duplicar tarefas, reescrever eventos históricos ou apagar colunas referenciadas pelo histórico. Definir o mapeamento físico de IDs e a retenção das colunas legadas após o inventário, antes da implementação.
- Dados ambíguos ou inconsistentes bloqueiam o backfill; apresentar contagens e pedir decisão, sem escolha silenciosa de destino.
- Backup verificado, ensaio em cópia restaurada e comparação de contagens/referências antes e depois. Rollback de aplicação e banco deve ser coordenado; imagem antiga sozinha não desfaz mudança de schema.

### Estratégia física aprovada para implementação

G-MIGRATION e G-WORKFLOW aprovados pelo PO em 2026-09-22: "Aprovo a migration e esse fluxo seguro", apos apresentacao explicita da copia por quadro, preservacao das tarefas e IDs historicos, backup verificado, ensaio e destino obrigatorio ao excluir estrutura ocupada. A aprovacao nao dispensa os testes nem o ensaio.

- `Stages` mantém `ProjectId` e `WorkflowStatusId`. Recebe `BoardId` anulável com FK `Restrict` para `Boards` e `LegacyStageId` anulável com FK `Restrict` auto-referente.
- Cada etapa existente permanece legada, com `BoardId = NULL` e `LegacyStageId = NULL`, para preservar exatamente os IDs usados por `StageHistories`. Ela não é mais uma coluna operacional.
- Para cada par consistente `(BoardId, StageId legado)` do mesmo projeto, a migration cria uma clone com novo ID, `BoardId` do quadro e `LegacyStageId` apontando para a etapa legada. Somente `WorkItems.StageId` é atualizado por essa chave composta.
- `StageHistories` e `TaskEvents` não são alterados. Assim o feed e o lead time podem relacionar a clone à etapa anterior por `LegacyStageId`, sem reescrever auditoria ou JSON histórico.
- A migration falha antes de gravar quando encontra `WorkItem` sem quadro, etapa inexistente, ou etapa de projeto diferente do quadro; e falha se contagens de `WorkItems`, `StageHistories`, `TaskEvents` ou clones divergem do mapeamento calculado.
- O `Down` é limitado ao rollback imediato sem novas colunas ou históricos nas clones. Depois de uso operacional, rollback exige restauração coordenada do backup; não se deve forçar uma reversão que descarte auditoria.

## Validação

- .NET: isolamento de colunas, validação quadro/coluna/projeto/tenant, transferências atômicas, exclusão segura, concorrência e preservação do histórico.
- React: quadro ativo claro, criação básica/cópia, coluna independente e destino explícito para movimentação.
- E2E desktop/mobile: dois quadros com estruturas diferentes, alterações isoladas, tarefa localizável após recarga, transferência e exclusão com preservação dos dados.
- Agent-browser: percorrer os mesmos fluxos e conferir tema claro/escuro; não usar tarefas reais para ensaios destrutivos.

## Gates

- G-SCOPE: direção funcional decidida pelo PO em 2026-09-22.
- G-SPEC: aprovado pelo PO em 2026-09-22.
- G-MIGRATION: aprovado pelo PO em 2026-09-22 para a estrategia incremental descrita, condicionado a backup verificado e ensaio.
- G-WORKFLOW: aprovado pelo PO em 2026-09-22 para o fluxo seguro de transferencia e exclusao com destino explicito descrito nesta spec.
- G-HISTORY: não alterar estrutura histórica; caso necessário, pedir aprovação específica.
