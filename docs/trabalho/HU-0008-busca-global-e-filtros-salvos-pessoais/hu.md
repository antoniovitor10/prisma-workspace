---
id: HU-0008
titulo: "Busca global e filtros salvos pessoais por quadro (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0008 · Busca global e filtros salvos pessoais por quadro (pendências do backlog)

## História

**Como** usuário autenticado que pesquisa itens e utiliza filtros salvos no Prisma WorkSpace
**Quero** concluir as pendências de escopo estritamente pessoal dos filtros salvos por quadro e remover controles de compartilhamento não suportados pela ADR-0064
**Para que** cada usuário gerencie seus próprios filtros rápidos por quadro sem vazamento entre membros da organização.

## Critérios de aceite

### CA-01 · Isolamento por OwnerUserId e BoardId e remoção de compartilhamento na UI (TASK-031, TASK-211, TASK-212)
- **Dado que** filtros salvos criados por usuários distintos em um mesmo quadro
- **Quando** a API listar, criar, atualizar ou excluir filtros salvos e a UI renderizar a barra de filtros do quadro
- **Então** todas as operações filtram estritamente por OwnerUserId do usuário autenticado e BoardId (com G-MIGRATION se houver ajuste de schema) e nenhum toggle de compartilhamento organizacional é exibido.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-031` | in_progress (auditoria PR #29: **parcial**) | Implementar painel lateral contextual com Aplicar/Salvar/Limpar e filtros exclusivamente pessoais de quadro ou globais; revalidar globais por quadro e sinalizar critérios ignorados, preservando ordem filtrada temporária e Product Backlog inicial Sem sprint. | [SavedFilter.cs:11](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Entities/SavedFilter.cs#L11) mantém BoardId/UserId/FilterJson; [KanbanFilterBar.tsx:1](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.tsx#L1) não oferece escopo global e chips de incompatibilidade. — TASK-211 declara G-MIGRATION; não implementar novo schema; TASK-212 depende dela. Gate humano no backlog original: sim (G-SPEC da SPEC-SEARCH-SAVED-FILTERS). | sim (G-SPEC da SPEC-SEARCH-SAVED-FILTERS) | `metodo/projeto/skills/dominio/referencias/search-saved-filters.md` |
| `TASK-211` | pending (auditoria PR #29: **parcial**) | Adicionar escopo Quadro atual ou Qualquer quadro acessivel ao SavedFilter. | [SavedFilter.cs:11](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Entities/SavedFilter.cs#L11) mantém BoardId/UserId/FilterJson; [KanbanFilterBar.tsx:1](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.tsx#L1) não oferece escopo global e chips de incompatibilidade. — TASK-211 declara G-MIGRATION; não implementar novo schema; TASK-212 depende dela. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/search-saved-filters.md` |
| `TASK-212` | pending (auditoria PR #29: **parcial**) | Revalidar criterios do filtro global por quadro e sinalizar por chip os criterios ignorados. | [SavedFilter.cs:11](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Entities/SavedFilter.cs#L11) mantém BoardId/UserId/FilterJson; [KanbanFilterBar.tsx:1](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/board/KanbanFilterBar.tsx#L1) não oferece escopo global e chips de incompatibilidade. — TASK-211 declara G-MIGRATION; não implementar novo schema; TASK-212 depende dela. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/search-saved-filters.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/search-saved-filters.md`
- `metodo/projeto/skills/dominio/referencias/project-work-item-queries.md`
- **ADRs relacionadas:** `ADR-0033`, `ADR-0064`, `ADR-0071`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
