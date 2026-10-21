---
id: HU-0006
titulo: "Organizações, plano de controle da plataforma e equipes (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0006 · Organizações, plano de controle da plataforma e equipes (pendências do backlog)

## História

**Como** administrador da plataforma e administrador de organização no Prisma WorkSpace
**Quero** concluir as pendências herdadas de plano de controle multi-organização, vínculo único de usuário comum por organização e homologação da gestão de equipes
**Para que** a governança multi-tenant e a alocação de equipes operem conforme organizations.md e teams.md.

## Critérios de aceite

### CA-01 · Plano de controle da plataforma e vínculo organizacional (TASK-034, TASK-117, TASK-118, TASK-119)
- **Dado que** a distinção entre Administrador da plataforma (flag Identity, ADR-0091) e usuários vinculados a uma organização
- **Quando** os gates de schema e mapeamento (G-MIGRATION e dependência de TASK-112) forem aprovados e implementados
- **Então** o plano de controle de organizações fica restrito ao Administrador da plataforma, com auditoria de criação/suspensão/arquivamento de tenants e regras de vínculo ativo respeitadas.

### CA-02 · Homologação ponta a ponta da gestão de equipes e capacidade (TASK-404)
- **Dado que** a implementação existente de equipes, membros e capacidade semanal
- **Quando** a suíte de testes e o roteiro de QA em 1440x900 e 390x844 forem executados
- **Então** todos os critérios de teams.md ficam homologados com evidência verificável.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-034` | pending (auditoria PR #29: **parcial**) | Consolidar organização como tenant raiz isolado, limitar usuário comum a uma única organização sem seletor e reservar ao Administrador da plataforma a seleção entre tenants, criação, edição, arquivamento, leitura de arquivadas e restauração. | [OrganizationsFeature.cs:130](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Organizations/OrganizationsFeature.cs#L130) só restringe criação adicional por vínculos ativos; [OrganizationRepository.cs:15](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Infrastructure/Repositories/OrganizationRepository.cs#L15) filtra memberships ativas. PR26 não completa membership única/control plane. — Identidade da plataforma resolvida D91; demais gates/mapeamento TASK-112 continuam próprios. PR28 é proposta separada, não conta como main. Gate humano no backlog original: sim (G-SCOPE para identidade do Administrador da plataforma; G-MIGRATION se o schema de controle precisar mudar). | sim (G-SCOPE para identidade do Administrador da plataforma; G-MIGRATION se o schema de controle precisar mudar) | `metodo/projeto/skills/dominio/referencias/organizations.md` |
| `TASK-117` | blocked (**bloqueada**) | Criar a autoridade de Administrador da plataforma fora dos perfis do tenant, com seletor de organizacoes exclusivo. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | G-SCOPE da identidade aprovado em 2026-10-06 (flag Identity, D91); outros gates e TASK-112 continuam próprios do control plane | `metodo/projeto/skills/dominio/referencias/organizations.md` |
| `TASK-118` | pending (auditoria PR #29: **parcial**) | Impor membership unica para usuario comum e ocultar o seletor de organizacao para quem nao e Administrador da plataforma. | [OrganizationsFeature.cs:130](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Organizations/OrganizationsFeature.cs#L130) só restringe criação adicional por vínculos ativos; [OrganizationRepository.cs:15](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Infrastructure/Repositories/OrganizationRepository.cs#L15) filtra memberships ativas. PR26 não completa membership única/control plane. — Identidade da plataforma resolvida D91; demais gates/mapeamento TASK-112 continuam próprios. PR28 é proposta separada, não conta como main. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/organizations.md` |
| `TASK-119` | pending (auditoria PR #29: **não feita**) | Arquivar e restaurar organizacao com leitura restrita ao control plane e bloqueio de escrita. | [Organization.cs:13](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Domain/Entities/Organization.cs#L13) só mantém IsActive; não há endpoints de control plane arquivar/restaurar/read-only no controller. — Control plane amplo depende de TASK-117 e gates próprios; risco alto. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/organizations.md` |
| `TASK-404` | pending (auditoria PR #29: **parcial**) | Homologar equipes, lideranca, capacidade e desativacao logica com preservacao de vinculos. | [TeamsFeature.cs:73](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Teams/TeamsFeature.cs#L73) implementa estado, líder e capacidade. Não há evidência de homologação oficial nesta auditoria. — Implementação existe; validar fluxos e preservação de vínculos, sem declarar QA oficial. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/teams.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/organizations.md`
- `metodo/projeto/skills/dominio/referencias/teams.md`
- `metodo/projeto/skills/dominio/referencias/invitation-onboarding.md`
- **ADRs relacionadas:** `ADR-0017`, `ADR-0019`, `ADR-0053`, `ADR-0091`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
