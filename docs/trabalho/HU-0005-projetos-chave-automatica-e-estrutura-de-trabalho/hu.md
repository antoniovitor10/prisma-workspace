---
id: HU-0005
titulo: "Gestão de projetos, ocultação de chave técnica e estrutura de trabalho (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0005 · Gestão de projetos, ocultação de chave técnica e estrutura de trabalho (pendências do backlog)

## História

**Como** administrador de organização e gestor de projeto no Prisma WorkSpace
**Quero** concluir as pendências de ocultação da chave técnica nas telas normais, remoção de consumidores visuais de metodologia na criação e simplificação/restauração do estado de projeto
**Para que** a experiência de criação e gestão de projetos siga integralmente ADR-0056, ADR-0057 e ADR-0073.

## Critérios de aceite

### CA-01 · Chave técnica oculta nas superfícies normais de UI e busca humana por título (TASK-024, TASK-122)
- **Dado que** um projeto com chave técnica gerada automaticamente no backend
- **Quando** o usuário visualizar listas, consultas, detalhes, subtarefas ou buscar itens nas telas normais
- **Então** o prefixo project.key-number não é exibido como identificador principal na UI normal, preservando a chave interna para banco, unicidade e integrações.

### CA-02 · Metodologia oculta nos consumidores e ciclo de arquivamento do projeto (TASK-123, TASK-115, TASK-116)
- **Dado que** a diretriz de manter todas as visões habilitadas sem escolha inicial de metodologia e o ciclo de status do projeto
- **Quando** os consumidores de UI forem reconciliados (TASK-123) e, após decisão humana e G-MIGRATION, o mapeamento de status e arquivamento (TASK-115, TASK-116) for aplicado
- **Então** nenhum fluxo condicional indevido depende de metodologia na UI e o arquivamento/reativação preserva o estado anterior dos itens.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-024` | pending (auditoria PR #29: **parcial**) | Gerar a chave técnica exclusivamente no backend, sem entrada, edição ou exibição nas telas normais; preservar o valor interno somente para banco, unicidade, importação, busca técnica, compatibilidade e integrações necessárias. | [ProjectItemsQuery.tsx:202](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/pages/ProjectItemsQuery.tsx#L202) ainda exibe project.key-number; detalhe/subtarefas também usam referência com prefixo. — D56 aprovada; preservar chave interna/contratos técnicos, sem migration. Gate humano no backlog original: não (decisão funcional aprovada; implementação ainda pendente). | não (decisão funcional aprovada; implementação ainda pendente) | `metodo/projeto/skills/dominio/referencias/project-key-auto-generation.md` |
| `TASK-122` | pending (auditoria PR #29: **parcial**) | Remover o prefixo de chave tecnica das referencias de tarefa exibidas na interface, preservando o uso interno. | [ProjectItemsQuery.tsx:202](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/pages/ProjectItemsQuery.tsx#L202) ainda exibe project.key-number; detalhe/subtarefas também usam referência com prefixo. — D56 aprovada; preservar chave interna/contratos técnicos, sem migration. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/project-key-auto-generation.md` |
| `TASK-123` | pending (auditoria PR #29: **parcial**) | Remover methodology dos contratos de leitura consumidos pela UI, mantendo a coluna por compatibilidade. | [BacklogPlanner.tsx:849](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx#L849) e [ReportsHub.tsx:212](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/features/reports/ReportsHub.tsx#L212) ainda variam campos por metodologia. DTO interno pode permanecer por compatibilidade. — SPEC-PROJECT-METHODOLOGY-HIDDEN permite contratos temporários; auditar consumidores antes de removê-los, sem schema. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/project-methodology-hidden.md` |
| `TASK-115` | blocked (**bloqueada**) | Reduzir ProjectStatus a Ativo e Arquivado com backfill dos cinco estados atuais. | Item bloqueado em backlog.md (prioridade P1, risco alto). Requer deliberação do gate antes de quebrar em task. | sim (mapeamento de Planning/OnHold/Completed/Cancelled exige decisao humana) | `metodo/projeto/skills/dominio/referencias/project-management.md` |
| `TASK-116` | pending (auditoria PR #29: **parcial**) | Arquivar e restaurar projeto com suas tarefas de forma transacional, sem arquivar os quadros transversais. | [ProjectManagementFeature.cs:143](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Projects/ProjectManagementFeature.cs#L143) chama Archive/Reactivate e Save do projeto, sem mecanismo de causa/restauração conjunta da família. — Depende de TASK-115/mapeamento de estados e TASK-109; não implementar alteração persistente neste goal. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/project-management.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/project-management.md`
- `metodo/projeto/skills/dominio/referencias/project-key-auto-generation.md`
- `metodo/projeto/skills/dominio/referencias/project-methodology-hidden.md`
- `metodo/projeto/skills/dominio/referencias/work-nature.md`
- **ADRs relacionadas:** `ADR-0020`, `ADR-0056`, `ADR-0057`, `ADR-0073`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
