---
id: ADR-0085
titulo: "Estudo de viabilidade de multi-banco de dados configurável na instalação"
status: aceita
data: 2026-09-09
origem: "DECISIONS.md D85"
substitui: ""
---

# ADR-0085 · Estudo de viabilidade de multi-banco de dados configurável na instalação

## Decisão

- **D85 — EM ABERTO, NÃO DECIDIDA.** Suporte a banco de dados escolhível pela pessoa que instala, com um
  padrão gratuito, levantado pelo PO em 2026-09-09 no contexto da distribuição open source. Hoje o produto é
  **SQL Server apenas**. A viabilidade é real e o caminho é o padrão do EF Core (um provider por configuração
  e um assembly de migrations por provider), mas existem acoplamentos concretos a resolver, todos já
  identificados no código: `sys.sp_getapplock` usado como lock consultivo em
  `WorkItemManagementRepository.AcquireOrganizationDependencyLockAsync`; `HasColumnType("nvarchar(max)")` nas
  configurations; a sequence que gera `WorkItem.Number`; e os tipos `datetimeoffset`. Requer `G-SCOPE` e spec
  própria antes de qualquer implementação. (levantado em 2026-09-09)
