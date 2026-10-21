# SPEC-KANBAN-VISUAL-ORDER: Ordem visual do Kanban

**Status:** approved

**Aprovação humana:** PO definiu explicitamente a ordem livre das colunas, a ordem manual compartilhada dos cartões e o comportamento pessoal dos filtros em 2026-08-24.

## Objetivo

Fazer do Kanban uma superfície de organização manual previsível: cada quadro define livremente suas colunas, todos os usuários autorizados veem a mesma ordem-base dos cartões e uma nova tarefa nasce no topo da coluna escolhida.

Esta spec especializa a D52 somente quanto à ordem visual. Não cria nomes institucionais de coluna, não transforma coluna em status global de projeto e não altera a relação transversal entre quadro e tarefa.

## Contrato funcional vigente

### Colunas

1. Cada quadro possui sua própria sequência de colunas, persistida por `Stage.Position`.
2. Nome e posição das colunas são livres; não existe sequência obrigatória como `Backlog`, `A fazer`, `Em desenvolvimento`, `Testes`, `Em impedimento`, `Implantação` e `Entregue`.
3. Usuário autorizado a administrar colunas pode reordená-las; a nova ordem é compartilhada por todos que acessam o quadro.
4. Importação não deve substituir a ordem original ou configurada por uma ordem institucional fixa. Quando a origem fornece ordem válida, ela deve ser preservada; quando não fornece, a importação usa uma ordem determinística sem inferir nomes obrigatórios.

### Cartões sem filtro ativo

5. A ordem-base dos cartões dentro de cada coluna é manual, persistida e compartilhada por todos os usuários do quadro.
6. Uma tarefa recém-criada entra no topo da coluna selecionada.
7. Depois da criação, o cartão pode ser reordenado verticalmente por interação manual; a posição persistida passa a ser a nova ordem-base compartilhada.
8. Mover uma tarefa entre colunas é uma alteração real da tarefa e deve persistir a coluna de destino conforme a D52.
9. Datas de criação, prioridade, prazo ou título não podem reorganizar automaticamente a ordem-base. Ordenações alternativas, se oferecidas, são apenas visões temporárias e explícitas.
10. Empates ou posições concorrentes usam desempate estável e determinístico, sem fazer `CreatedAt` prevalecer sobre a intenção manual.

### Cartões com filtro ativo

11. O resultado filtrado é uma visão pessoal e temporária sobre o mesmo quadro; aplicar um filtro não altera cartões, colunas nem a ordem-base compartilhada.
12. Reordenar verticalmente cartões enquanto um filtro está ativo altera somente a ordem pessoal daquele resultado filtrado. Essa ordem não grava `Position`, não afeta outros usuários e não sobrescreve a ordem-base.
13. A ordem pessoal filtrada é descartada ao remover ou alterar o filtro e não integra a definição de um filtro salvo.
14. Mover uma tarefa entre colunas continua permitido com filtro ativo e persiste a coluna real de destino. Somente a ordenação vertical exibida no subconjunto filtrado permanece pessoal e temporária.
15. Limpar os filtros restaura a ordem-base manual compartilhada, e não a ordenação automática por itens mais recentes.

## Fora do escopo

- Definir nomes, quantidade mínima ou template obrigatório de colunas.
- Alterar a classificação interna de coluna aberta/concluída ou as regras de concluir/reabrir da D52.
- Alterar permissões de administração de quadros e colunas.
- Alterar transições, limites WIP, backlog, sprint ou automações.
- Criar migration sem auditoria que comprove necessidade real.
- Persistir ordem pessoal por filtro, dispositivo ou usuário.

## Critérios de aceitação

- **Dado** um quadro com colunas nomeadas e ordenadas pelo usuário **quando** ele é reaberto **então** a mesma sequência aparece para todos os usuários autorizados.
- **Dado** um quadro importado **quando** a importação termina **então** sua ordem não é substituída por uma lista institucional fixa de nomes.
- **Dado** uma coluna com cartões existentes **quando** uma tarefa é criada nela **então** o novo cartão aparece no topo.
- **Dado** um quadro sem filtro **quando** um cartão é reordenado verticalmente **então** a posição persiste e é vista por outro usuário autorizado.
- **Dado** que outro usuário reabre o quadro **quando** nenhuma ordenação temporária está ativa **então** ele recebe a mesma ordem-base compartilhada.
- **Dado** um filtro ativo **quando** o usuário reordena verticalmente o subconjunto visível **então** somente sua visão atual muda e `Position` não é persistida.
- **Dado** uma ordem pessoal filtrada **quando** o filtro é alterado ou removido **então** essa ordem é descartada e a ordem-base compartilhada reaparece.
- **Dado** um filtro salvo **quando** ele é persistido **então** sua definição não contém a ordem vertical temporária dos cartões.
- **Dado** um filtro ativo **quando** uma tarefa é movida para outra coluna **então** a coluna real da tarefa muda e a operação não é tratada como simples ordenação pessoal.
- **Dado** que o usuário limpa filtros e agrupamentos **quando** o quadro volta à visão normal **então** a ordenação é `Ordem do quadro`, nunca `Mais recentes` por padrão.

## Estado atual comprovado no código

O sistema já possui elementos úteis para este contrato:

- `Stage.Position` e endpoint de reordenação de colunas;
- controles para mover colunas à esquerda e à direita com atualização otimista e rollback;
- posição numérica em `WorkItem`/placement e comando de movimento com posição;
- opção visual `Ordem do quadro` no filtro do Kanban;
- ordenações explícitas por prioridade, prazo, título e data de criação;
- movimento de tarefa entre colunas por ponteiro.

Evidências principais:

- `src/Prisma.Workspace.Web/src/pages/Kanban.tsx`
- `src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.tsx`
- `src/Prisma.Workspace.Web/src/features/board/kanbanOrdering.ts`
- `src/Prisma.Workspace.Application/Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs`
- `src/Prisma.Workspace.Infrastructure/Repositories/WorkItemRepository.cs`
- `migracao/build-sql.cjs`

## Gaps entre o código atual e o contrato aprovado

### G-KANBAN-ORDER-001 — padrão ainda é “Mais recentes”

`Kanban.tsx` inicializa `cardSort` como `created`, aplica `compareNewestWorkItems` e volta para essa ordenação ao limpar filtros. Assim, `CreatedAt` encobre a posição manual compartilhada. O padrão esperado é `position`/`Ordem do quadro`.

### G-KANBAN-ORDER-002 — cartão novo é inserido no fim

O fluxo local de criação calcula `Math.max(...position) + 100`; portanto, uma tarefa recém-criada entra no final da coluna, e não no topo.

### G-KANBAN-ORDER-003 — não há reordenação vertical na mesma coluna

O arraste atual descobre apenas a coluna sob o ponteiro. `moveItemToStage` encerra sem operação quando a origem e o destino são a mesma coluna, e não existe cálculo do índice vertical de destino. A ordem-base manual dos cartões não pode ser alterada pela interface atual.

### G-KANBAN-ORDER-004 — movimentos entre colunas sempre anexados ao fim

Movimentos por botão e por ponteiro também calculam `Math.max(...position) + 100`. Além de não considerar o ponto vertical do drop, a implementação não separa a posição canônica compartilhada da ordem pessoal de uma visão filtrada.

### G-KANBAN-ORDER-005 — filtros não possuem ordem pessoal isolada

Filtro, agrupamento e ordenação atuam diretamente sobre a lista derivada em memória, mas não existe estado específico para reordenação vertical temporária por visão filtrada. Também falta garantir que essa ordem nunca seja enviada à API nem incluída em `SavedFilter`.

### G-KANBAN-ORDER-006 — importador ainda carrega intenção institucional antiga

A spec anterior exigia normalizar o Canal Mobile para sete nomes em ordem fixa e `migracao/build-sql.cjs` permanece na área afetada da TASK-018. O importador deve ser auditado para remover qualquer normalização fixa remanescente e preservar a ordem da origem quando disponível.

### G-KANBAN-ORDER-007 — concorrência e normalização precisam de cobertura

O contrato atual não comprova rebalanceamento seguro de posições, desempate estável nem atualização coerente para dois usuários reordenando o mesmo quadro. A implementação futura deve definir concorrência, rollback e normalização sem usar data de criação como autoridade.

Esta atualização é exclusivamente documental e não autoriza corrigir esses gaps no código de produto.

## Impacto em dados e contratos

- A implementação deve reutilizar as posições já existentes sempre que suficiente.
- Não há migration presumida. Qualquer necessidade de nova tabela/coluna para ordem compartilhada exige auditoria e `G-MIGRATION` antes da alteração.
- A ordem pessoal filtrada vive somente no cliente durante a visão atual e não cria contrato persistente.
- A mudança de coluna continua usando o contrato real de movimento da tarefa; filtro não cria cópia ou projeção independente.
- A futura migração do modelo N:N legado para a posição única da D52 deve preservar a ordem-base válida e será tratada pelo trabalho estrutural correspondente.

## Homologação manual pendente

1. Criar colunas com nomes arbitrários, reordená-las e reabrir o quadro.
2. Validar a mesma ordem de colunas com outro usuário autorizado.
3. Criar três tarefas na mesma coluna e confirmar cada nova tarefa no topo.
4. Sem filtro, reordenar cartões verticalmente, recarregar e confirmar persistência.
5. Abrir o mesmo quadro com outro usuário e confirmar a ordem-base compartilhada.
6. Aplicar filtro, reordenar verticalmente e confirmar que outro usuário não vê a alteração.
7. Alterar e remover o filtro; confirmar descarte da ordem pessoal e retorno da ordem-base.
8. Salvar um filtro após reordenar o subconjunto, reaplicá-lo e confirmar que a ordem temporária não foi salva.
9. Com filtro ativo, mover uma tarefa para outra coluna e confirmar que a mudança real persiste.
10. Limpar filtros e confirmar que a visão retorna para `Ordem do quadro`.
11. Importar um quadro de homologação e confirmar preservação da ordem fornecida pela origem, sem nomes fixos impostos.

## Mapeamento de testes

- **.NET:** criação no topo; persistência/rebalanceamento da ordem-base; movimento entre colunas; autorização; concorrência e rollback; desempate estável.
- **React/Vitest:** padrão `Ordem do quadro`; reordenação vertical sem filtro; isolamento temporário com filtro; descarte ao alterar/remover filtro; filtro salvo sem ordem pessoal; nova tarefa no topo.
- **Playwright:** ordem livre de colunas; criação no topo; reordenação compartilhada entre duas sessões; reordenação pessoal filtrada; movimento real entre colunas com filtro; recarga e restauração da ordem-base.

## Human Gates para implementação

- `G-SPEC`: contrato aprovado por decisão explícita do PO.
- `G-WORKFLOW`: não é exigido para simples ordem visual; torna-se obrigatório se a implementação também mudar semântica de coluna aberta/concluída ou transições.
- `G-MIGRATION`: somente se auditoria comprovar mudança real de schema.
- `G-COMPLETION`: conforme a classificação da tarefa de implementação.

## Rastreabilidade

`D52` + `D60` → `SPEC-KANBAN-VISUAL-ORDER` → `TASK-018` → testes .NET, React e Playwright → homologação manual
