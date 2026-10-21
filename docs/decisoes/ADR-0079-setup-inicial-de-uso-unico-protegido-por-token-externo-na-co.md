---
id: ADR-0079
titulo: "Setup inicial de uso único protegido por token externo na Community Edition"
status: aceita
data: 2026-09-05
origem: "DECISIONS.md D79"
substitui: ""
---

# ADR-0079 · Setup inicial de uso único protegido por token externo na Community Edition

## Decisão

- **D79** — O primeiro acesso de uma instalação Community usa setup anônimo de uso único protegido por token externo
  forte, estado singleton persistido e transação serializável no SQL Server compartilhada entre Identity e domínio.
  O primeiro e-mail nasce confirmado para permitir operação sem SMTP; o token trafega somente no header redigido
  `X-Prisma-Setup-Token`. O seed demonstrativo deixa de ser implícito, passa a ser Development-only, opt-in e neutro,
  e também encerra o setup. O PO aprovou diretamente `G-SPEC` e `G-MIGRATION` em 2026-09-05. (2026-09-05)
