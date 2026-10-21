---
id: ADR-0056
titulo: "Chave de projeto gerada internamente pelo backend e oculta na experiência"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D56"
substitui: ""
---

# ADR-0056 · Chave de projeto gerada internamente pelo backend e oculta na experiência

## Decisão

- **D56** — A chave de projeto deixa de ser informação funcional da experiência. Não será exibida no formulário
  de criação, cartões, cabeçalhos, breadcrumbs, seletores, filtros, relatórios ou demais telas operacionais, nem
  poderá ser informada ou editada pelo usuário. O backend permanece como única autoridade para gerar uma chave
  técnica estável e única por organização. `Project.Key`, seu índice e contratos internos podem ser preservados
  enquanto forem necessários para banco, unicidade, importação, busca técnica, compatibilidade ou integrações;
  removê-los futuramente exige auditoria de consumidores e, se houver alteração de schema, `G-MIGRATION`.
  (2026-08-24)
