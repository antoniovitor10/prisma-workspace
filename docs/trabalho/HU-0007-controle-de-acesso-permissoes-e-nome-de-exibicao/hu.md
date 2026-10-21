---
id: HU-0007
titulo: "Controle de acesso, catálogo de permissões e nome funcional de responsável (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0007 · Controle de acesso, catálogo de permissões e nome funcional de responsável (pendências do backlog)

## História

**Como** administrador de organização e membro de projeto no Prisma WorkSpace
**Quero** concluir as pendências herdadas do modelo de permissões (ADR-0055) e garantir o snapshot e exibição do nome funcional de responsável (ADR-0046, ADR-0063)
**Para que** a autorização por organização/projeto e a identificação humana de autores e responsáveis sejam consistentes em todas as telas e históricos.

## Critérios de aceite

### CA-01 · Reconciliação de escopos, redução de perfis legados e perfis customizados (TASK-110, TASK-111, TASK-112, TASK-113, TASK-114)
- **Dado que** que o modelo de quadros voltou ao escopo de projeto (ADR-0083/ADR-0089) e que a redução de OrganizationRole/ProjectRole exige decisão humana de mapeamento e G-MIGRATION
- **Quando** o PO deliberar sobre o mapeamento de perfis e autorizar as migrations correspondentes
- **Então** o catálogo de permissões, os perfis-base e as concessões por projeto refletem fielmente user-access-permissions.md sem resíduos do quadro transversal.

### CA-02 · Obrigatoriedade de nome completo e snapshot histórico de autor/responsável (TASK-300, TASK-301)
- **Dado que** o cadastro/edição de usuário e o registro de eventos em StageHistory e TaskEvent
- **Quando** um usuário for cadastrado ou realizar uma transição/evento auditado (observado o G-HISTORY/G-MIGRATION caso TaskEvent mude de estrutura)
- **Então** o nome completo é validado e o snapshot do nome funcional no momento do evento é preservado.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-110` | pending (auditoria PR #29: **não feita**) | Criar a permissao configuravel Administrar quadros em PlatformPermission e aplica-la nas operacoes de quadro e coluna. | [PlatformPermission.cs:3](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Enums/PlatformPermission.cs#L3) termina em ManagePermissions14, sem Administrar quadros. — D83/D89 usam autorização de projeto; reavaliar recorte/mapeamento antes de implementar literalmente o plano D52. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/user-access-permissions.md` |
| `TASK-111` | pending (auditoria PR #29: **não feita**) | Adicionar o escopo Board a PermissionScope e implementar o acesso derivado Quadro para Projetos. | [PermissionScope.cs:3](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Enums/PermissionScope.cs#L3) não tem Board; D83 revogou acesso derivado quadro para projetos. — G-SCOPE para reconciliar tarefa antiga; não reintroduzir quadro transversal ou remover enum por inferência. Gate humano no backlog original: sim (G-MIGRATION se o enum persistido mudar de dominio). | sim (G-MIGRATION se o enum persistido mudar de dominio) | `metodo/projeto/skills/dominio/referencias/user-access-permissions.md` |
| `TASK-112` | blocked (**bloqueada**) | Reduzir OrganizationRole aos cinco perfis-base da D55, com mapeamento verificavel dos dez perfis atuais. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | sim (mapeamento de perfis legados exige decisao humana) | `metodo/projeto/skills/dominio/referencias/user-access-permissions.md` |
| `TASK-113` | blocked (**bloqueada**) | Eliminar a hierarquia fixa ProjectRole e migrar suas concessoes para o modelo de escopos. | Item bloqueado em backlog.md (prioridade P1, risco alto). Requer deliberação do gate antes de quebrar em task. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/user-access-permissions.md` |
| `TASK-114` | pending (auditoria PR #29: **não feita**) | Permitir perfis personalizados reutilizaveis por conjunto de permissoes, sem conceder autoridade que o criador nao possui. | [OrganizationRole.cs:5](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Enums/OrganizationRole.cs#L5) mantém 10 perfis; [AppDbContext.cs:51](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Infrastructure/Persistence/AppDbContext.cs#L51) possui grants, sem agregado de perfil customizado. — Depende de TASK-112/mapeamento humano e G-MIGRATION. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/user-access-permissions.md` |
| `TASK-300` | pending (auditoria PR #29: **não feita**) | Exigir nome completo no cadastro de novos usuarios, com fallback restrito a dados legados (D63). | [AuthController.cs:269](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Api/Controllers/AuthController.cs#L269) RegisterRequest só tem email/senha; :49 cria usuário sem nome. Campo de convite não prova cadastro público. — G-SPEC/D99 aprovados; C5 autorizado. Usar persistência existente se possível; nenhuma migration nova. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/responsible-display-name.md` |
| `TASK-301` | pending (auditoria PR #29: **parcial**) | Garantir que novos snapshots historicos preservem o nome completo observado no momento do evento. | [MoveWorkItemCommandHandler.cs:68](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/WorkItems/Commands/MoveWorkItemCommandHandler.cs#L68) resolve snapshot ao mover; [CreateWorkItemCommandHandler.cs:175](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/WorkItems/Commands/CreateWorkItemCommandHandler.cs#L175) não atribui ator à história inicial. — Pode preencher campos já existentes para eventos novos; não reescrever história nem mudar estrutura sem G-HISTORY. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/responsible-display-name.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/user-access-permissions.md`
- `metodo/projeto/skills/dominio/referencias/responsible-display-name.md`
- `metodo/projeto/skills/dominio/referencias/project-assignment-access.md`
- **ADRs relacionadas:** `ADR-0018`, `ADR-0046`, `ADR-0055`, `ADR-0063`, `ADR-0090`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
