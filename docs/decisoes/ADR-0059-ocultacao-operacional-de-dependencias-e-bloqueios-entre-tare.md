---
id: ADR-0059
titulo: "Ocultação operacional de dependências e bloqueios entre tarefas"
status: substituida # substituida por ADR-0099
data: 2026-08-24
origem: "DECISIONS.md D59"
substitui: ""
substituida_por: "ADR-0099"
---

# ADR-0059 · Ocultação operacional de dependências e bloqueios entre tarefas

## Decisão

- **D59** — Dependências, pré-requisitos e bloqueios entre tarefas ficam ocultos e sem função operacional nesta
  fase. A interface não exibe nem permite criar, editar, remover ou pesquisar dependências; vínculos preservados
  não bloqueiam tarefas e não alimentam workflow, automações, notificações, filtros, indicadores, dashboards ou
  relatórios. Estruturas, código defensivo e dados internos atuais podem permanecer temporariamente apenas por
  compatibilidade/auditoria, sem migration ou limpeza agora e sem inferir novos vínculos em importações. Reativar
  a funcionalidade exige nova decisão e `G-SCOPE`; remover ou transformar schema/dados exige `G-MIGRATION`.
  (2026-08-24)
