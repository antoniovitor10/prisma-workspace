---
id: ADR-0061
titulo: "Substituição do Grafo de Estados pela aba Linha do tempo no detalhe da tarefa"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D61"
substitui: ""
---

# ADR-0061 · Substituição do Grafo de Estados pela aba Linha do tempo no detalhe da tarefa

## Decisão

- **D61** — O detalhe da tarefa mantém seis abas: **Descrição, Comentários, Subtarefas, Anexos, Histórico e Linha
  do tempo**. A Linha do tempo substitui o `Grafo de estados` por uma lista simples, textual e legível, limitada
  à criação na coluna inicial e às movimentações entre colunas. A ordem é da movimentação mais recente para a
  mais antiga; cada movimento mostra data/hora, pessoa, coluna anterior e nova coluna, e o evento inicial aparece
  como `Tarefa criada na coluna X`. Comentários permanecem somente em sua aba e fora do histórico automático
  conforme D47; Histórico continua sendo a auditoria geral separada. Esta decisão sucede somente a projeção em
  React Flow da D44/D49 e preserva o modal centralizado, a auditoria e os dados históricos existentes. (2026-08-24)
