---
id: ADR-0083
titulo: "Quadro como visão opcional do projeto com fluxo compartilhado e sem WIP"
status: substituida # substituida por ADR-0089
data: 2026-09-08
origem: "DECISIONS.md D83"
substitui: "ADR-0052"
substituida_por: "ADR-0089"
---

# ADR-0083 · Quadro como visão opcional do projeto com fluxo compartilhado e sem WIP

## Decisão

- **D83** — Quadro é visão opcional; fluxo pertence ao projeto; WIP removido. O PO decidiu em 2026-09-08,
  textualmente *"eu queria de uma forma que quadros fossem descartáveis ou opcionais porque não to vendo
  sentido pra eles"*, confirmado com *"pode seguir minha ideia"*, e *"tira o wip por favor"*. O conjunto
  ordenado de colunas passa a pertencer ao `Project`; `Board` deixa de ser contêiner e vira visão salva, sem
  colunas próprias; um projeto opera sem nenhum quadro. `WorkItemBoardPlacement` é removido e a tarefa passa a
  ter uma única `StageId`/`Position` — a duplicidade que causou o defeito corrigido em `94980fc`. O limite de
  WIP sai do modelo, da API, da interface e da configuração de projeto. Nas três decisões em aberto o PO
  escolheu: quadro transversal a vários projetos é **revogado** (todo quadro pertence a um projeto); o nome
  **"Quadro"** é mantido na interface; equipe e permissão passam a ser **do projeto**, e o quadro não restringe
  quem o enxerga, o que tira o propósito de `PermissionScope.Board` e obriga a reavaliá-lo no lote de perfis e
  permissões. Esta decisão sucede a `SPEC-BOARDS-STAGES-WIP` pela `SPEC-BOARD-AS-VIEW`. `G-SPEC`,
  `G-MIGRATION` e `G-WORKFLOW` aprovados pela instrução direta do PO; `G-HISTORY` não se aplica porque
  `StageHistory` e `TaskEvent` não mudam de estrutura. (2026-09-08)
  **Constatação em implementação Stage→Project (2026-09-09):** `PermissionScope.Board` permanece no enum e
  no código até decisão explícita do PO no lote de perfis — **não remover nesta migration**.
