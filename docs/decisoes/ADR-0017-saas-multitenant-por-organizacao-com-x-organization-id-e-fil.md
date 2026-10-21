---
id: ADR-0017
titulo: "SaaS multitenant por organização com X-Organization-Id e filtros globais"
status: aceita
data: 2026-07-15
origem: "DECISIONS.md D17"
substitui: ""
---

# ADR-0017 · SaaS multitenant por organização com X-Organization-Id e filtros globais

## Decisão

- **D17** — O SaaS será multitenant por **organização**. Agregados-raiz recebem
  `OrganizationId` obrigatório; entidades-filhas herdam o tenant pelo agregado. A organização ativa
  é enviada em `X-Organization-Id`, validada contra uma associação ativa e aplicada por filtros
  globais do EF Core. Ausência de contexto não libera leitura cruzada. (2026-07-15)
