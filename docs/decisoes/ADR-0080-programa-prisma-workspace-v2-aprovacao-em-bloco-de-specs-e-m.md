---
id: ADR-0080
titulo: "Programa Prisma WorkSpace v2, aprovação em bloco de specs e migrations incrementais"
status: aceita
data: 2026-09-08
origem: "DECISIONS.md D80"
substitui: ""
---

# ADR-0080 · Programa Prisma WorkSpace v2, aprovação em bloco de specs e migrations incrementais

## Decisão

- **D80** — Programa **Prisma WorkSpace v2**: decisão humana registrada em 2026-09-08. O PO determinou
  textualmente que *"só no prisma, tudo que há de ser feito agora é no prisma e só"* — todo o trabalho de produto
  passa a acontecer exclusivamente no repositório `prisma-workspace`, e o repositório antigo `runrun` sai do
  caminho como origem de trabalho. O PO também autorizou deploy em produção *"Sim, sem restrição"*, com o alvo de
  `https://prisma.nordevs.com.br` passar a servir o código do `prisma-workspace` (D74 permanece quanto ao domínio
  canônico). Sobre o `G-SPEC` em bloco, o PO declarou *"pode garantir todas as specs como aprovadas exceto isso que
  passei ai agora"*: ficam aprovadas as specs ativas de `specs/`, **exceto** `SPEC-S-003` (`specs/sprints.md`), que
  retorna a `draft` e recebe contrato novo antes de qualquer implementação. Esta decisão é aprovação humana
  explícita, não autoaprovação de agente. A ordem de execução do programa mantém a **cadeia incremental de
  migrations durante todo o programa**, com consolidação apenas no lote final `migrations-consolidation`, porque a
  hipótese de migration inicial única (fresh-install-only, antiga Fase 5 do plano de distribuição) é incompatível
  com a base real de produção, que possui dados e histórico de migrations aplicadas. (2026-09-08)
