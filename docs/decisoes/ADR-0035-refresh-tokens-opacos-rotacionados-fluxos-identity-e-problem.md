---
id: ADR-0035
titulo: "Refresh tokens opacos rotacionados, fluxos Identity e Problem Details"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D35"
substitui: ""
---

# ADR-0035 · Refresh tokens opacos rotacionados, fluxos Identity e Problem Details

## Decisão

- **D35** — Refresh tokens são opacos, armazenados somente como hash, rotacionados a cada uso e
  enviados à SPA em cookie HttpOnly/SameSite; revogação de uma família invalida sua reutilização.
  Confirmação de e-mail e recuperação de senha usam tokens nativos e temporários do Identity,
  lockout contabiliza falhas reais e segredos de banco/JWT saem dos arquivos versionados para
  configuração externa. Erros HTTP seguem Problem Details, com mapa de campos em validações.
  (2026-07-20)
