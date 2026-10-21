---
id: HU-0012
titulo: "Distribuição Open Source, segurança de dependências, release Community e consolidação de migrations (pendências do backlog)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0012 · Distribuição Open Source, segurança de dependências, release Community e consolidação de migrations (pendências do backlog)

## História

**Como** mantenedor e adotante da Community Edition do Prisma WorkSpace
**Quero** concluir o saneamento de dependências (npm e NuGet), documentação de operação/backup, geração de SBOM/proveniência no CI, definição de licença/release v0.1.0 e consolidação segura de migrations
**Para que** a edição comunitária possa ser distribuída, instalada e atualizada com segurança, reprodutibilidade e conformidade pública.

## Critérios de aceite

### CA-01 · Atualização testada de dependências npm e NuGet e geração de SBOM/proveniência (TASK-502, TASK-503, TASK-505)
- **Dado que** o passivo de vulnerabilidades altas/moderadas em pacotes npm e a dependência transitiva AngleSharp no backend
- **Quando** os lotes de atualização compatível forem aplicados (sem audit fix major automático — ADR-0038) e o job de CI for estendido
- **Então** os advisories são saneados com suíte verde e o pipeline gera artefatos de SBOM e proveniência.

### CA-02 · Documentação de instalação/backup, ensaio de upgrade, licença e release v0.1.0 (TASK-041, TASK-500, TASK-501, TASK-504, TASK-506, TASK-507)
- **Dado que** que a escolha de licença (TASK-501) e a publicação de release (TASK-506) exigem decisão humana explícita do PO (G-SCOPE e G-DEPLOY)
- **Quando** os ensaios de instalação limpa, backup/restauração e upgrade forem executados em ambiente isolado de teste e os gates humanos forem deliberados
- **Então** a documentação e o pacote da Community Edition comprovam preservação de dados e ausência de segredos ou referências institucionais.

### CA-03 · Inventário e consolidação de migrations do EF Core (TASK-600, TASK-601, TASK-602, TASK-603)
- **Dado que** que qualquer estratégia ou aplicação de consolidação de migrations exige aprovação humana prévia (G-MIGRATION e G-DEPLOY)
- **Quando** o inventário for fechado e a estratégia aprovada pelo PO for ensaiada em cópia isolada antes de qualquer aplicação
- **Então** o histórico __EFMigrationsHistory e os dados existentes são integralmente preservados com rollback documentado.

## Pendências herdadas do `backlog.md` (auditoria do PR #29)

Esta HU agrupa as pendências vivas (`parcial`, `não feita` e `blocked`) da área, migradas sem tasks filhas pré-criadas conforme `ADR-0103`. Ao puxar esta HU para refinamento/execução, cada pendência elegível será quebrada em tasks (`T-XXXX`) respeitando os gates humanos listados abaixo:

| ID original | Estado no backlog / auditoria PR #29 | Requisito original | O que falta / evidência da auditoria | Gate humano pendente | Referência da área |
|---|---|---|---|---|---|
| `TASK-041` | in_progress (auditoria PR #29: **parcial**) | Criar uma distribuição Community Edition sanitizada, instalável, documentada e segura em novo repositório. | [compose.yaml:1](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/compose.yaml#L1) e [ci.yml:1](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/.github/workflows/ci.yml#L1) entregam distribuição/CI; licença, SBOM, proveniência e documentação operacional seguem pendentes. — Licença/G-SCOPE e publicação/G-DEPLOY ainda humanos; não escolher nem publicar. Gate humano no backlog original: sim (G-SCOPE e G-SPEC aprovados em 2026-09-05; licença, G-MIGRATION condicional e G-DEPLOY pendentes). | sim (G-SCOPE e G-SPEC aprovados em 2026-09-05; licença, G-MIGRATION condicional e G-DEPLOY pendentes) | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-500` | pending (auditoria PR #29: **parcial**) | Neutralizar os residuos institucionais Detran/runrun remanescentes em seed, documentacao e nomes visiveis. | [DbInitializer.cs:99](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Infrastructure/Persistence/DbInitializer.cs#L99) usa Produto Demo; README/src têm identidade Prisma. Documentação/histórico ainda requer auditoria sanitizada separada. — Preservar migrations/compatibilidade D76 e histórico verdadeiro. Auditoria de distribuição não equivale a licença/publicação. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-501` | blocked (**bloqueada**) | Escolher e aplicar a licenca do projeto; bloqueia a promocao publica. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | sim (decisao humana obrigatoria; nenhum agente escolhe licenca) | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-502` | pending (auditoria PR #29: **não feita**) | Corrigir o passivo npm de 5 altas e 28 moderadas em lotes testados, sem audit fix major automatico (D78). | [package-lock.json:1](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Web/package-lock.json#L1) auditado em20/10:8altas/34moderadas/0críticas, em vez dos5/28históricos. — D38/D78 aprovam correções compatíveis testadas; não usar audit fix major automático. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-503` | pending (auditoria PR #29: **não feita**) | Fixar a dependencia transitiva moderada do NuGet (AngleSharp) na menor familia estavel compativel (D38). | [Prisma.Workspace.Infrastructure.csproj:8](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/src/Prisma.Workspace.Infrastructure/Prisma.Workspace.Infrastructure.csproj#L8) fixa HtmlSanitizer9.0.967; dotnet audit confirma AngleSharp0.17.1/GHSA-pgww-w46g-26qg moderado. — Verificar versão corrigida/compatibilidade antes de alterar pacote; sem upgrade major automático. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-504` | pending (auditoria PR #29: **não feita**) | Escrever e verificar a documentacao de instalacao, backup e restauracao da Community Edition. | [docker.md:106](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/docs/installation/docker.md#L106) diz que backup/restauração/upgrade ainda serão documentados após ensaio. — Sem nova migration; ensaio somente em ambiente próprio, nunca usar caminho/segredo de produção. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-505` | pending (auditoria PR #29: **não feita**) | Gerar SBOM e proveniencia no CI, publicados junto aos artefatos. | [ci.yml:87](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/.github/workflows/ci.yml#L87) exporta imagem/checksum/migrations; não gera SBOM ou proveniência. — Depende de TASK-502/503. Pode gerar artefato de CI, sem release/tag/publicação institucional neste goal. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-506` | blocked (**bloqueada**) | Adotar SemVer e preparar a primeira release v0.1.0. | Item bloqueado em backlog.md (prioridade P1, risco medio). Requer deliberação do gate antes de quebrar em task. | sim (G-DEPLOY e licenca) | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-507` | pending (auditoria PR #29: **parcial**) | Ensaiar instalacao limpa e upgrade em ambiente novo, comprovando preservacao de dados. | [ci.yml:104](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/.github/workflows/ci.yml#L104) testa imagem em SQL E2E; não há evidência de ensaio Community completo de upgrade com preservação. — Riscoalto: não integra implementações baixas/médias deste goal; instalação/upgrade pendentes de ensaio próprio. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-600` | pending (auditoria PR #29: **parcial**) | Inventariar as migrations aplicadas em producao e as criadas durante o programa, com hashes e ordem. | [deployment-manifest.py:5](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/scripts/deployment-manifest.py#L5) gera ordem/hashes; [deployment.md:23](https://github.com/antoniovitor10/prisma-workspace/blob/a2797a7/docs/installation/deployment.md#L23) descreve comparação operacional, sem inventário atual do banco no repo. — Não consultar produção nem iniciar consolidação; G-MIGRATION da estratégia continua humano. Campo human_gate não declarado no backlog original. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-601` | blocked (**bloqueada**) | Definir a estrategia de consolidacao preservando __EFMigrationsHistory e os dados reais. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | sim (estrategia exige aprovacao humana antes de qualquer script) | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-602` | blocked (**bloqueada**) | Ensaiar a consolidacao em copia restaurada da base de producao, com verificacao de dados antes e depois. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | não declarado no backlog original | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |
| `TASK-603` | blocked (**bloqueada**) | Aplicar a consolidacao com backup, verificacao pos-aplicacao e rollback documentado. | Item bloqueado em backlog.md (prioridade P0, risco alto). Requer deliberação do gate antes de quebrar em task. | sim | `metodo/projeto/skills/dominio/referencias/open-source-distribution.md` |

## Fora de escopo

- Tarefas já concluídas em `backlog.md` (`completed`, `done`, `implemented`, `cancelled`) ou descartadas na auditoria do PR #29 como `feita` (`TASK-100`, `TASK-101`, `TASK-104`, `TASK-108`, `TASK-508`) ou `sucedida` (`TASK-102`, `TASK-303`, `TASK-305`, `TASK-400`, `TASK-403`).
- Execução imediata de itens com `G-MIGRATION`, `G-WORKFLOW`, `G-HISTORY`, `G-SCOPE` ou `G-DEPLOY` sem aprovação explícita prévia do Vitor (PO).

## Notas e referências

- **Referências de contrato vigente da área (skill `dominio`):**
- `metodo/projeto/skills/dominio/referencias/open-source-distribution.md`
- `metodo/projeto/skills/dominio/referencias/installation-setup.md`
- **ADRs relacionadas:** `ADR-0038`, `ADR-0074`, `ADR-0075`, `ADR-0077`, `ADR-0079`, `ADR-0080`, `ADR-0086`, `ADR-0093`, `ADR-0103`
- **Origem:** migração sob demanda de `backlog.md` reconciliado pela auditoria do PR #29 (`origin/docs/backlog-audit-20261020:relatorios/oficina-backlog-audit.md`).
