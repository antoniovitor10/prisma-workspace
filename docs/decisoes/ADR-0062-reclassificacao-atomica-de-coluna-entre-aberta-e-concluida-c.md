---
id: ADR-0062
titulo: "Reclassificação atômica de coluna entre aberta e concluída com confirmação"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D62"
substitui: ""
---

# ADR-0062 · Reclassificação atômica de coluna entre aberta e concluída com confirmação

## Decisão

- **D62** — Reclassificar uma coluna de aberta para concluída é uma operação de impacto coletivo. Se houver
  tarefas na coluna, a interface exige confirmação com contagens do impacto e, ao confirmar, conclui todas as
  tarefas aplicáveis na mesma transação da reclassificação. Quando uma tarefa-pai afetada possuir descendentes
  abertos em qualquer nível, inclusive em outras colunas, a confirmação deve perguntar explicitamente se toda a
  descendência recursiva também será concluída; sem esse consentimento, a reclassificação inteira é cancelada.
  Reclassificar a coluna de concluída para aberta também exige confirmação e reabre todas as tarefas nela. Nos
  dois sentidos, classificação e estados das tarefas mudam em uma única transação, cada tarefa alterada recebe
  seu próprio histórico e o impacto/concorrência é revalidado antes de persistir. A operação coletiva é coerente
  com o movimento individual da D52/D53: coluna concluída implica tarefa concluída; coluna aberta implica tarefa
  aberta. (2026-08-24)
