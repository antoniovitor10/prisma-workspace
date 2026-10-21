---
id: ADR-0089
titulo: "Colunas independentes por quadro com preservação de dados e exclusão assistida"
status: aceita
data: 2026-09-22
origem: "DECISIONS.md D89"
substitui: "ADR-0083"
---

# ADR-0089 · Colunas independentes por quadro com preservação de dados e exclusão assistida

## Decisão

- **D89** - Em 2026-09-22, o PO escolheu explicitamente "Mudar para colunas independentes por quadro" ao comparar o relatório de testes com D83. Sucede a direção de fluxo compartilhado por projeto da D83. Não autoriza apagar dados nem reativar WIP ou quadros transversais. Contrato em `specs/independent-board-columns.md` (`approved`). O PO aprovou G-MIGRATION/G-WORKFLOW com "Aprovo a migration e esse fluxo seguro": cópia incremental das colunas por quadro, tarefas mantidas no quadro atual, IDs históricos preservados, backup verificado e ensaio antes de produção. Exclusão de estrutura ocupada exige destino explícito. Aprovação não significa migração aplicada nem dispensa validação.
