---
id: ADR-0075
titulo: "Distribuição open source em novo repositório a partir de snapshot sanitizado"
status: aceita
data: 2026-09-05
origem: "DECISIONS.md D75"
substitui: ""
---

# ADR-0075 · Distribuição open source em novo repositório a partir de snapshot sanitizado

## Decisão

- **D75** — A distribuição open source do Prisma WorkSpace nasce em um novo repositório `prisma-workspace`, criado
  inicialmente como privado a partir de snapshot sanitizado por allowlist. O histórico do repositório `runrun` não
  será importado, reescrito nem publicado e permanecerá como arquivo privado recuperável. A promoção pública exige
  auditoria de segredos/dados institucionais, escolha humana da licença e `G-DEPLOY`. Cursor CLI pode executar lotes
  mecânicos delimitados, mas não aprova gates, decide arquitetura/licença, acessa segredos ou publica sem revisão.
  Migrations aplicadas são preservadas até estratégia específica aprovada em `G-MIGRATION`. (2026-09-05)
