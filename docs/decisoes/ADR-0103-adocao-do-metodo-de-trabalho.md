---
id: ADR-0103
titulo: "Adoção do método de trabalho v0.5.1 com rastreador Prisma e reforma do fluxo AI-Native"
status: aceita
data: 2026-10-21
origem: "onboarding"
substitui: "ADR-0067, ADR-0078, ADR-0096, ADR-0097"
---

# ADR-0103 · Adoção do método de trabalho v0.5.1 com rastreador Prisma e reforma do fluxo AI-Native

## Contexto

O repositório operava com o arranjo próprio AI-Native V0 (`agents/*.yaml`, `workflows/feature.yaml`, `profiles/prisma-loop.yaml`, `context/index.yaml`, `specs/*.md`, `stories/*.md`, `backlog.md`, `DECISIONS.md`, `PROGRESS.md` e `NOTAS.md`), definido em D67, D78 e nas regras de processo de D96 e D97. Com a evolução do método de trabalho padronizado (versão 0.5.1) e a entrada em produção da integração Git nativa no próprio Prisma WorkSpace (Fases 1 e 2a, D100 a D102), manter dois sistemas paralelos de governança, rastreamento e memória operacional gerava duplicidade de artefatos e atrito entre o estado real do código e o backlog estático.

## Decisão

Adotar integralmente o método de trabalho versão 0.5.1 (`metodo/nucleo/` e `metodo/projeto/`), com reformulação completa do fluxo do projeto e remoção do legado após a migração:

1. **Rastreador e configuração (`metodo/projeto/config.yaml`):**
   - Rastreador: `prisma`, organização `Nordevs` (uso exclusivo do Vitor), instância `https://prisma.nordevs.com.br`.
   - Autonomia: nível `2` para tarefas normais e `1` para risco alto (com aprovação do revisor Esquadro e CI verde, o Maestro integra; mudanças de risco alto aguardam o Vitor).
   - `tem_homologacao: false`.
   - Método versionado no repositório público.
   - Revisão de segurança: `quando-sensivel`.
2. **Unidade de trabalho e especificações:**
   - Trabalho novo nasce como História de Usuário (`HU-XXXX` em `docs/trabalho/`), que substitui o fluxo de spec individual com `G-SPEC` da D67.
   - As 48 especificações canônicas com status `approved` em `specs/` migram como referências de contrato vigente na skill de projeto `metodo/projeto/skills/dominio/referencias/`. As 5 especificações `superseded` não migram. Toda HU nova cita a referência da área correspondente.
3. **Migração do backlog:**
   - `backlog.md` é migrado sob demanda a partir da auditoria do PR #29 (`origin/docs/backlog-audit-20261020`): tarefas já concluídas (`completed`/`done`/`implemented`/`cancelled`), `feitas` ou `sucedidas` não migram; tarefas vivas (`parcial`, `não feita` e `blocked`) são agrupadas por área em HUs abertas (`status: aberta`), sem tasks filhas pré-criadas.
4. **Decisões e memória operacional:**
   - `DECISIONS.md` é convertido integralmente em ADRs numeradas (`ADR-0001` a `ADR-0102`) em `docs/decisoes/`, mantendo o mesmo número (`D5` = `ADR-0005`), com `ADR-0102` em status `proposta`.
   - `PROGRESS.md` e `NOTAS.md` deixam de existir; o registro operacional passa a ser o relatório da task no PR e os comentários/fatos no Prisma.
5. **Regras fixas do modo Prisma:**
   - O token do agente nunca move uma task para `em-desenvolvimento`.
   - Nunca se grava `status:` no frontmatter de `task.md` (no modo `prisma`, o status da task vive no Prisma; o status da HU permanece no frontmatter de `hu.md`).
   - Branches de task seguem `task/T-XXXX-slug` e PRs trazem `[T-XXXX]` no título.
6. **Direção visual:**
   - A criação de `DESIGN.md` na raiz e da skill `metodo/projeto/skills/design-system/SKILL.md` fica registrada na `HU-0001` (aberta, sem escopo de landing page), preservando a direção visual vigente de D68 e D70.

Esta decisão substitui D67 (`ADR-0067`), D78 (`ADR-0078`) e a parte de processo de D96 (`ADR-0096`) e D97 (`ADR-0097`).

## Alternativas descartadas

- **Manter AI-Native V0 em paralelo ao método 0.5.1:** descartada por duplicar contratos (`specs/` vs `HU`, `backlog.md` vs Prisma, `DECISIONS.md` vs `docs/decisoes/`) e exigir manutenção dupla de ferramentas e validadores.
- **Migrar todas as 118 entradas do `backlog.md` como tasks individuais imediatas:** descartada porque 46 entradas já estão concluídas, feitas ou sucedidas, e criar dezenas de tasks sem sprint ativo poluiria o quadro antes do refinamento por área.

## Consequências

### Positivas

- Fonte única de verdade para decisões (`docs/decisoes/`), contratos de domínio (`metodo/projeto/skills/dominio/`) e trabalho vivo (`docs/trabalho/` + quadro no Prisma).
- Eliminação de conflitos de merge em arquivos monolíticos como `PROGRESS.md`, `DECISIONS.md` e `backlog.md`.
- Validação determinística da estrutura de HUs, tasks e ADRs via `metodo/nucleo/scripts/tasks.mjs`.

### Negativas / custos

- Exige uma etapa de transição em dois PRs (primeiro criação/cópia dos novos artefatos, depois limpeza do legado e ajuste do CI/testes que leem `specs/`, `stories/` e `context/index.yaml`).

## Como reverter

Restaurar os arquivos legados a partir do histórico Git anterior ao PR de remoção do legado e reverter a pasta `metodo/`, `docs/decisoes/` e `docs/trabalho/`.
