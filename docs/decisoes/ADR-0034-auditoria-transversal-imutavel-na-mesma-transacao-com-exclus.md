---
id: ADR-0034
titulo: "Auditoria transversal imutável na mesma transação com exclusão de segredos"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D34"
substitui: ""
---

# ADR-0034 · Auditoria transversal imutável na mesma transação com exclusão de segredos

## Decisão

- **D34** — A auditoria transversal é imutável e gravada na mesma transação das alterações pelo
  `AppDbContext`, usando o contexto autenticado da requisição para usuário, origem, IP e correlação.
  Somente entidades funcionais previstas são auditadas; senhas, hashes, tokens, chaves, segredos e
  conteúdo binário são excluídos dos snapshots anterior/novo. Consultas de auditoria exigem
  permissão administrativa e permanecem isoladas por organização. (2026-07-20)
