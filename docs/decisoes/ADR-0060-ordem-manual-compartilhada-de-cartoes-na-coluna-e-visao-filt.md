---
id: ADR-0060
titulo: "Ordem manual compartilhada de cartões na coluna e visão filtrada temporária"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D60"
substitui: ""
---

# ADR-0060 · Ordem manual compartilhada de cartões na coluna e visão filtrada temporária

## Decisão

- **D60** — A ordem das colunas permanece livre e compartilhada conforme D52. Dentro de cada coluna, cartões
  possuem uma ordem-base manual, persistida e comum a todos os usuários autorizados; uma tarefa recém-criada
  entra no topo e pode ser reordenada verticalmente depois. Data de criação ou outra ordenação automática não
  substitui essa base. Filtros produzem somente uma visão pessoal e temporária: reordenar verticalmente o
  subconjunto filtrado não grava a posição compartilhada, não afeta outros usuários, não integra filtro salvo e
  é descartado ao alterar ou remover o filtro. Com filtro ativo, mover a tarefa entre colunas continua sendo uma
  alteração real e persistente; apenas sua ordenação vertical na visão filtrada é temporária. Esta decisão
  substitui a ordem institucional fixa e o padrão “Mais recentes” antes documentados em
  `SPEC-KANBAN-VISUAL-ORDER`, sem alterar workflow, WIP ou o caráter transversal do quadro. (2026-08-24)
