---
id: HU-0011
titulo: "Wiki, portal externo, dashboards analíticos e homologação visual sistêmica (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0011 · Wiki, portal externo, dashboards analíticos e homologação visual sistêmica (pendências do backlog)

## História

**Como** usuário interno e solicitante externo do Prisma WorkSpace
**Quero** concluir a homologação funcional da Wiki por projeto, do Portal Externo por protocolo, dos Dashboards/Relatórios e da consistência visual ponta a ponta das telas autenticadas (D68/D70)
**Para que** todas as superfícies complementares do produto estejam verificadas contra seus contratos aprovados.

## Critérios de aceite

### CA-01 · Homologação de Wiki, Portal Externo e Dashboards/Relatórios (TASK-405, TASK-406, TASK-407)
- **Dado que** as implementações existentes de Wiki com histórico de versões, Portal Externo público/triagem e Dashboards/Construtor de relatórios
- **Quando** os roteiros de teste automatizado e QA visual forem executados com dados de demonstração/teste (sem uso de dados institucionais de produção)
- **Então** todos os critérios de wiki-knowledge.md, external-portal.md e dashboards-reports.md são comprovados sem vazamento de dados internos.

### CA-02 · Homologação de consistência visual ponta a ponta das telas autenticadas (TASK-408)
- **Dado que** a direção visual D68/D70 e a referência prisma-visual-system.md
- **Quando** todas as telas autenticadas forem inspecionadas em 1440x900 e 390x844 nos modos claro e escuro
- **Então** não restam desvios de contraste, alinhamento, estados vazios ou responsividade.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-405` | pending (auditoria PR #29: **parcial**) | Homologar wiki, anexos, organizacao de paginas, busca e restauracao de revisao. | [WikiFeature.cs:44](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Wiki/WikiFeature.cs#L44) mantém lock/versões; [ProjectWiki.tsx:481](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/src/pages/ProjectWiki.tsx#L481) expõe histórico, com defeitos D18/D19 ainda na fila. — Implementação existe; homologação ainda não comprovada. C2/C4 cobrem defeitos visuais/acessibilidade. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/wiki-knowledge.md` |
| `TASK-406` | pending (auditoria PR #29: **parcial**) | Homologar portal externo, protocolo, triagem, SLA e aprovacoes com dados reais. | [portal-requests.spec.ts:1](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/e2e/portal-requests.spec.ts#L1) e módulos ExternalPortal/Sla implementam fluxos; não comprovam homologação com dados reais e casos completos. — Não usar produção/dados institucionais neste goal; QA oficial continua pendente. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/external-portal.md` |
| `TASK-407` | pending (auditoria PR #29: **parcial**) | Homologar dashboards, construtor de relatorios e exportacao CSV sob autorizacao. | [OrganizationHoursReportFeature.cs:37](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Application/Features/Reports/OrganizationHoursReportFeature.cs#L37) recebe ActorId mas consulta em:62 sem filtro obrigatório; B07 corrigido fora da main. — Não marcar homologado por existência de relatórios. Usar fixtures próprias; preservar regra de privacidade das horas. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/dashboards-reports.md` |
| `TASK-408` | pending (auditoria PR #29: **não feita**) | Regressao visual e de acessibilidade das rotas principais depois das mudancas dos lotes anteriores. | [kanban-responsive.spec.ts:8](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/e2e/kanban-responsive.spec.ts#L8) existe, mas QA anterior registrou falhas repetidas e C2-C5 ainda não concluídos. — Depende dos lotes anteriores e QA/regressão; não considerar screenshots antigos como aprovação atual. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/prisma-visual-system.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/wiki-knowledge.md`
- `metodo/projeto/skills/dominio/referencias/external-portal.md`
- `metodo/projeto/skills/dominio/referencias/dashboards-reports.md`
- `metodo/projeto/skills/dominio/referencias/prisma-visual-system.md`
- **ADRs relacionadas:** `ADR-0026`, `ADR-0027`, `ADR-0028`, `ADR-0031`, `ADR-0068`, `ADR-0070`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
