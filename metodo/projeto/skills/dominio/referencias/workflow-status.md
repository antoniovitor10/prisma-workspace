# SPEC-WORKFLOW-STATUS: Coluna como Status Canônico da Tarefa

**Status:** approved

**Decisão humana:** PO confirmou em 2026-08-24 que não existe status funcional separado do projeto: o status visível da tarefa é o nome da coluna atual do quadro.

## Objetivo

Eliminar a duplicidade entre coluna e status. Toda alteração feita no Kanban, no detalhe, em listas, automações ou integrações deve ler e escrever a mesma posição canônica da tarefa no quadro.

## Contrato funcional vigente

### Fonte única

1. Cada tarefa possui uma única coluna atual (`StageId`) no quadro atual conforme D52.
2. O status textual da tarefa é exatamente `Stage.Name`.
3. A classificação interna da coluna determina somente o estado binário da tarefa:
   - coluna aberta -> tarefa aberta;
   - coluna concluída -> tarefa concluída.
4. `Aberta` e `Concluída` não são nomes obrigatórios nem etiquetas textuais principais; são semântica interna da coluna.
5. Uma tarefa não pode exibir nome de `WorkflowStatus`, categoria genérica ou `CompletedAt` como status textual diferente do nome da coluna.
6. Cor, ícone ou estilo do status visível, quando usados, derivam da coluna e não de status paralelo do projeto.

### Alterações em qualquer superfície

7. O seletor identificado como `Status` no detalhe lista as colunas autorizadas do quadro atual.
8. Escolher um status nesse seletor executa o mesmo movimento de cartão para a coluna selecionada.
9. Arrastar o cartão no Kanban atualiza a coluna canônica; detalhe, listas, backlog, sprint, busca, dashboards e relatórios passam a mostrar o novo nome da coluna.
10. Editar pela API, ação em massa, automação ou integração também usa o mesmo comando/regra de movimento; nenhuma superfície grava status independente.
11. Atualização otimista pode antecipar a nova coluna, mas falha do servidor restaura coluna e status textual juntos.
12. Eventos em tempo real e invalidação de cache atualizam todas as projeções sem exigir recarga manual.

### Conclusão, reabertura e reclassificação

13. Mover individualmente para coluna concluída conclui a tarefa; mover para coluna aberta a reabre.
14. Reclassificar coluna aberta como concluída segue a D62: confirmação de impacto, conclusão atômica das tarefas e descendência recursiva autorizada, com histórico individual.
15. Reclassificar coluna concluída como aberta também exige confirmação e reabre atomicamente todas as tarefas nela, com histórico individual.
16. Após movimento ou reclassificação confirmados, não existe estado válido em que uma tarefa aberta permaneça em coluna concluída ou uma tarefa concluída permaneça em coluna aberta.
17. O status textual continua sendo o nome da coluna tanto antes quanto depois da reclassificação; não surge etiqueta genérica adicional.

### Histórico e leitura

18. A Linha do tempo registra criação e movimentos usando snapshots dos nomes das colunas conforme D61.
19. Renomear uma coluna altera o status textual corrente das tarefas nela, sem reescrever os nomes históricos já observados.
20. Histórico técnico pode registrar conclusão/reabertura e reclassificação, mas não cria um segundo status funcional.
21. Filtro, agrupamento e relatório por `Status` significam filtro, agrupamento e relatório pelo nome/identificador da coluna atual.
22. Interfaces não devem oferecer simultaneamente filtros separados de `Status` e `Coluna` com a mesma finalidade.

## Fora do escopo

- Status independentes por projeto.
- Templates de status ou transições Organização -> Projeto como fonte do Kanban.
- Dropdown separado de workflow e coluna.
- Nome obrigatório para coluna aberta ou concluída.
- Reescrever histórico antigo quando uma coluna for renomeada.
- Excluir schema legado sem auditoria e `G-MIGRATION`.

## Estado atual comprovado no código

O sistema implantado ainda possui duas camadas:

- `Stage` representa coluna e possui nome, posição, categoria e vínculo opcional com `WorkflowStatus`.
- `WorkItem` possui `StageId`, `WorkflowStatusId` e `CompletedAt`.
- `WorkflowStatus` pertence ao projeto e possui nome, cor, categoria, inicial/final e transições.
- templates organizacionais, modos `Inherited`/`Custom` e projeções locais foram implementados pela TASK-009.
- movimentos atualizam `StageId`, sincronizam `WorkflowStatusId` e ajustam `CompletedAt` pela categoria da coluna.
- `WorkItemDto.StatusName` é preenchido por `WorkflowStatus.Name`.
- o detalhe atualmente prioriza `Concluído` quando há `CompletedAt`, depois `WorkflowStatusName`, depois `StageName`.
- notificações de movimento já usam o nome da coluna na mensagem, um comportamento alinhado ao novo alvo.

Essas estruturas permanecem como fotografia as-built e legado técnico. Não possuem autoridade sobre o contrato funcional aprovado.

## Gaps entre código e contrato

### G-STATUS-001 — status paralelo ainda é persistido

`WorkItem.WorkflowStatusId`, `Stage.WorkflowStatusId`, `WorkflowStatus`, `WorkflowTransition` e templates de organização mantêm um workflow separado do quadro. Isso permite divergência e contraria a fonte única da D65.

### G-STATUS-002 — DTO expõe nome de workflow

`GetWorkItemsByBoardIdQueryHandler` preenche `StatusName` e `StatusColor` a partir de `WorkflowStatus`, não da coluna efetiva.

### G-STATUS-003 — detalhe mostra rótulo genérico

`TaskDetailDrawer.tsx` usa `completedAt ? 'Concluído' : workflowStatusName || stageName || 'Backlog'`. Assim, pode ocultar o nome real da coluna e mostrar status independente/genérico.

### G-STATUS-004 — contratos duplicam coluna e status

DTOs e tipos carregam `StageId/StageName` junto com `WorkflowStatusId/WorkflowStatusName`. Consumidores podem escolher fontes diferentes, e não há depreciação explícita do par legado.

### G-STATUS-005 — configuração de workflow ainda está ativa

APIs e telas permitem administrar templates, modos herdado/customizado, status e transições por projeto. Essas superfícies não fazem parte do status canônico aprovado e devem ser ocultadas/desativadas antes da remoção estrutural.

### G-STATUS-006 — modelo N:N ainda admite colunas simultâneas

Placements múltiplos podem representar a tarefa em mais de um quadro/coluna. A migração para a posição singular da D52 é pré-condição para uma fonte única física.

### G-STATUS-007 — reclassificação bidirecional em lote não existe

O código conclui/reabre principalmente ao mover uma tarefa individual. Não há prévia, confirmação, transação coletiva nem histórico por tarefa nos dois sentidos definidos na D62.

### G-STATUS-008 — projeções e filtros precisam convergir

Backlog, sprint, detalhe, busca, filtros, dashboards e relatórios ainda precisam ser auditados para remover rótulos/filtagens duplicados e usar o nome da coluna atual.

Esta revisão é documental e não autoriza corrigir código de produto.

## Persistência e migração futura

- `StageId`/posição singular será a autoridade física do status da tarefa.
- A classificação aberta/concluída pertence à coluna e deve ser persistida sem depender de status do projeto.
- `CompletedAt` pode continuar como dado temporal derivado das transições de conclusão/reabertura, mas não é nome de status.
- Antes de remover `WorkflowStatusId`, tabelas de template/transição ou FKs, deve-se auditar integrações, automações, histórico, filtros, relatórios e dados importados.
- A migração precisa preservar coluna canônica e histórico, relatar ambiguidades e possuir backup/rollback.
- Estruturas legadas podem permanecer temporariamente sincronizadas por compatibilidade, mas não podem ser exibidas nem aceitar edição que gere divergência.
- Toda alteração física exige `G-MIGRATION`; mudança na semântica operacional exige `G-WORKFLOW`.

## Contratos esperados

### Leitura

- DTO funcional expõe `boardId`, `stageId`, `stageName`, classificação interna e `completedAt` quando necessário.
- Campo chamado `statusName`, enquanto existir por compatibilidade, deve ser alias exato de `stageName`, nunca de `WorkflowStatus.Name`.
- Recursos sem coluna só podem existir como legado a migrar e devem retornar estado técnico explícito, não inventar `Backlog`.

### Escrita

- Alterar status recebe a coluna de destino e executa o comando de movimento autorizado.
- A API valida quadro, coluna, tenant, permissão, WIP, hierarquia, conclusão/reabertura e concorrência.
- Não existe comando funcional para alterar somente `WorkflowStatusId` de uma tarefa.
- Reclassificação de coluna segue prévia + confirmação versionada e transação da D62.

## Critérios de aceite

- **Dado** uma tarefa na coluna `Em validação` **quando** qualquer tela mostra seu status **então** o texto exibido é `Em validação`.
- **Dado** uma coluna aberta ou concluída com nome livre **quando** suas tarefas são exibidas **então** o nome da coluna é o status principal, sem etiqueta genérica `Aberta`/`Concluída` substituí-lo.
- **Dado** o dropdown de status no detalhe **quando** o usuário escolhe outra opção **então** o cartão é movido para essa coluna e todas as telas refletem o novo nome.
- **Dado** um cartão arrastado no Kanban **quando** o servidor confirma **então** detalhe, listas, filtros e relatórios passam a usar a coluna de destino sem reload manual.
- **Dado** falha no movimento **quando** a UI havia atualizado otimisticamente **então** coluna e status textual voltam juntos ao valor confirmado.
- **Dado** coluna aberta reclassificada como concluída **quando** o impacto é confirmado **então** todas as tarefas afetadas ficam concluídas e continuam com o status textual igual ao nome dessa coluna.
- **Dado** coluna concluída reclassificada como aberta **quando** o impacto é confirmado **então** todas as tarefas nela são reabertas e o texto continua sendo o nome da coluna.
- **Dado** uma coluna renomeada **quando** a tarefa é consultada **então** o status corrente usa o novo nome e a Linha do tempo preserva snapshots históricos.
- **Dado** filtro/relatório por status **quando** é usado **então** agrupa ou filtra pela coluna atual e não por workflow paralelo.
- **Dado** configuração antiga de workflow **quando** a interface normal é usada **então** ela não oferece um segundo status capaz de divergir da coluna.

## Testes futuros

- **.NET:** alias `statusName == stageName`; movimento único; conclusão/reabertura; ausência de escrita independente; autorização, WIP e concorrência.
- **.NET/SQL Server:** seleção da coluna canônica, preservação de histórico, remoção segura de FKs/status legados e rollback.
- **React:** detalhe/dropdown usa colunas; ausência de rótulo genérico; cache sincronizado; rollback conjunto; filtros sem duplicidade.
- **Playwright:** mover pelo detalhe e verificar Kanban; arrastar no Kanban e verificar detalhe/listas; reclassificar nos dois sentidos; renomear coluna; testar desktop/mobile.

## Impacto nas tasks históricas

- TASK-009 e TASK-015 registram a implementação e os testes do modelo de templates por organização, mas esse modelo foi sucedido funcionalmente pela D65.
- O código produzido por essas tasks é legado a retirar com segurança; não deve ser ampliado como novo requisito.
- O trabalho futuro deve ser de convergência para coluna canônica, não de evolução da herança de workflow.

## Human Gates

- `G-SPEC`: contrato aprovado explicitamente pelo PO.
- `G-WORKFLOW`: obrigatório para substituir a autoridade de workflow por coluna canônica e para a D62.
- `G-MIGRATION`: obrigatório antes de alterar/remover FKs, tabelas, índices ou dados legados.
- `G-HISTORY`: somente se a estrutura imutável do histórico mudar; snapshots de nomes devem ser preservados.
- `G-COMPLETION`: conforme classificação da tarefa de implementação.

## Rastreabilidade

`D52` + `D53` + `D61` + `D62` + `D65` → `SPEC-WORKFLOW-STATUS` → convergência futura de TASK-009/TASK-015 → testes .NET, React e Playwright → homologação manual
