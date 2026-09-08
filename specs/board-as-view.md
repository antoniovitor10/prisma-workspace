# SPEC-BOARD-AS-VIEW: Quadro como visão opcional

**Status:** draft

**Sucede:** `SPEC-BOARDS-STAGES-WIP` (`specs/boards-stages-wip.md`), que permanece vigente até a aprovação desta.

**Origem:** decisão do PO em 2026-09-08, textualmente *"eu queria de uma forma que quadros fossem descartáveis
ou opcionais porque não to vendo sentido pra eles"*, confirmada com *"pode seguir minha ideia"*. O PO também
determinou a remoção do limite de WIP (*"tira o wip por favor"*).

**Autoridade:** decisão humana explícita. Não é autoaprovação de agente.

**Natureza:** contrato proposto. Exige `G-SPEC`, `G-MIGRATION` e `G-WORKFLOW` antes de qualquer implementação.

---

## Problema

Hoje `Board` é um contêiner obrigatório, e não uma forma de olhar o trabalho:

- `Stage` pertence a `Board`, e `Stage` é a fonte funcional do status da tarefa. Sem quadro não existe coluna,
  e sem coluna não existe status. Por isso todo projeto precisa de um quadro, criado automaticamente.
- `WorkItem` carrega `BoardId` e `StageId`, e ainda existe `WorkItemBoardPlacement` como projeção N:N com
  `StageId` e `Position` próprios.
- Essa duplicidade já produziu defeito real em produção: a consulta do quadro lia a etapa do placement enquanto
  a alteração pela tela de detalhe gravava apenas `WorkItem.StageId`, deixando o card preso na coluna antiga de
  forma permanente (corrigido no commit `94980fc`, mas a causa estrutural permanece).
- O usuário precisa entrar num projeto para então escolher um quadro para ver as tarefas, o que o PO questionou
  diretamente.

## Contrato proposto

### Fluxo pertence ao projeto

1. O conjunto ordenado de colunas passa a pertencer ao **Project**, não ao Board.
2. Cada coluna mantém nome, ordem e a classificação funcional (`StageCategory`) escolhida na interface,
   conforme D82. A classificação continua sendo o que define se a tarefa conta como concluída.
3. Um projeto tem um e apenas um fluxo. Não existe fluxo por quadro.
4. Um projeto recém-criado nasce com fluxo padrão utilizável, sem exigir criação de quadro.

### Quadro vira visão

5. `Board` deixa de ser contêiner e passa a ser uma **visão salva**: nome, filtro e ordenação sobre tarefas.
6. A visão não possui colunas próprias. Ela renderiza as colunas do projeto das tarefas que exibe.
7. Quadro é **opcional**: um projeto é plenamente utilizável sem nenhuma visão salva. A aba Kanban do projeto
   mostra o fluxo do projeto diretamente.
8. Criar, renomear, duplicar, arquivar e excluir uma visão nunca altera tarefa, coluna, posição ou histórico.
9. Excluir a última visão de um projeto é permitido e não deixa o projeto inoperante.

### Posição da tarefa

10. `WorkItem` mantém `ProjectId`, `StageId` e uma única `Position` dentro da coluna.
11. `WorkItemBoardPlacement` é **removido funcionalmente**. Não existe mais posição por quadro.
12. Mover a tarefa altera exatamente um par (`StageId`, `Position`), qualquer que seja a visão usada.
13. A movimentação continua registrando `StageHistory` e recalculando `CompletedAt` pela categoria de destino.

### Limite de WIP

14. O limite de WIP é **removido** do produto: modelo, API, interface e configuração de projeto.
15. Nenhuma regra de bloqueio ou aviso por quantidade de cartões permanece.

### Migração

16. Cada `Board` existente vira uma visão do projeto ao qual pertencia.
17. As colunas dos quadros de um mesmo projeto são consolidadas no fluxo do projeto, preservando nome, ordem e
    categoria; colunas equivalentes são fundidas pelo `WorkflowStatusId`.
18. Cada tarefa passa a ter a etapa e a posição que ocupava no seu quadro de origem (`BoardId` atual).
19. `StageHistory`, `TaskEvent` e demais registros históricos são preservados sem reescrita semântica.
20. O PO declarou em 2026-09-08 que os dados atuais são descartáveis, o que permite uma migração direta, sem
    backfill heurístico e sem período de convivência entre os dois modelos.

## Critérios de aceite

- **Dado** um projeto sem nenhuma visão salva, **quando** o usuário abre a aba Kanban, **então** vê as colunas
  do projeto e suas tarefas, sem precisar criar quadro.
- **Dado** um projeto com visões salvas, **quando** o usuário exclui todas, **então** o projeto continua
  operando e nenhuma tarefa muda de coluna ou posição.
- **Dado** um card em qualquer visão, **quando** ele é movido, **então** a nova coluna e posição valem para
  todas as visões e para o detalhe da tarefa, sem divergência.
- **Dado** que a etapa foi alterada pela tela de detalhe, **então** o quadro reflete a mudança imediatamente e
  depois de recarregar.
- **Dado** que a coluna de destino é de categoria concluída, **então** `CompletedAt` é preenchido.
- **Dado** qualquer tela do produto, **então** não existe configuração, aviso ou bloqueio de WIP.

## Impacto de dados e workflow

- Mover `Stage.BoardId` para `Stage.ProjectId` e remover `WorkItemBoardPlacement` altera schema: exige
  `G-MIGRATION`.
- Remover WIP e a exclusividade de posição por quadro altera regra de workflow: exige `G-WORKFLOW`.
- Nenhuma alteração é proposta na estrutura imutável de `StageHistory` ou `TaskEvent`, portanto `G-HISTORY`
  não se aplica; se a implementação precisar alterá-las, o gate passa a ser exigido.

## Test Gate Mapping

- **.NET:** fluxo pertencente ao projeto; projeto sem visão continua operável; movimentação com posição única;
  `CompletedAt` por categoria; ausência de qualquer regra de WIP; migração consolidando colunas equivalentes.
- **React/Vitest:** aba Kanban sem visão salva; criação, renomeação e exclusão de visão sem efeito sobre
  tarefas; ausência de campo de WIP.
- **Playwright:** abrir projeto sem visão e operar o Kanban; mover card e confirmar em outra visão e no
  detalhe; excluir todas as visões e continuar operando.

## Human Gates

- `G-SPEC` — **pendente**. Esta spec está em `draft` e não autoriza implementação.
- `G-MIGRATION` — **pendente**. Movimentação de `Stage` e remoção de `WorkItemBoardPlacement`.
- `G-WORKFLOW` — **pendente**. Remoção de WIP e da posição por quadro.

## Decisões que faltam ao PO

1. **Visão transversal a vários projetos.** A `SPEC-BOARDS-STAGES-WIP` vigente define o quadro como transversal
   à organização, reunindo tarefas de projetos diferentes. Se o fluxo passa a pertencer ao projeto, uma visão
   transversal precisa lidar com projetos que têm colunas diferentes. Alternativas:
   - **A.** A visão transversal agrupa por projeto, cada bloco com as colunas do seu projeto. Preserva a
     verdade de cada projeto; a tela fica mais alta.
   - **B.** As colunas passam a pertencer à organização, e todos os projetos compartilham o mesmo fluxo. Visão
     transversal fica trivial; projetos perdem fluxo próprio.
   - **C.** Visões transversais deixam de existir; toda visão pertence a um projeto. É o mais simples e o mais
     alinhado a "quadro é opcional", mas remove uma capacidade hoje aprovada.
   - *Recomendação técnica:* **C**, com **A** como evolução caso a necessidade apareça.
2. **Nome do objeto na interface.** Manter "Quadro" para a visão salva, ou renomear para "Visão"? Manter o nome
   evita reaprendizado; renomear deixa explícito que ela não contém nada.
3. **Equipes e permissões hoje associadas ao quadro.** Passam a ser do projeto, ou a visão continua podendo
   restringir quem a enxerga?

## Rastreabilidade

Decisão do PO de 2026-09-08 → `SPEC-BOARD-AS-VIEW` → sucede `SPEC-BOARDS-STAGES-WIP` → lote `core-domain-v2` →
testes .NET, React e Playwright → homologação manual.
