---
id: ADR-0031
titulo: "Construtor declarativo de relatórios sobre catálogo fechado e exportação CSV"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D31"
substitui: ""
---

# ADR-0031 · Construtor declarativo de relatórios sobre catálogo fechado e exportação CSV

## Decisão

- **D31** — O construtor de relatórios é declarativo e executado no servidor sobre um catálogo
  fechado de fontes, colunas, métricas, filtros, agrupamentos, ordenações e visualizações.
  `SavedReport` persiste a definição JSON validada, proprietário, projeto opcional e
  compartilhamento na organização; nunca armazena nem executa SQL fornecido pelo usuário.
  Dashboards são projeções dos mesmos agregados e aceitam filtros básicos, evitando tabelas de
  resumo divergentes. A exportação inicial usa CSV da projeção autorizada. (2026-07-20)
