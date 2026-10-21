---
id: ADR-0071
titulo: "Compositor visual de consultas por projeto com filtros salvos e estado na URL"
status: aceita
data: 2026-09-03
origem: "DECISIONS.md D71"
substitui: ""
---

# ADR-0071 · Compositor visual de consultas por projeto com filtros salvos e estado na URL

## Decisão

- **D71** — A primeira entrega de consultas por projeto usa um compositor visual, definição JSON interna, filtros
  pessoais salvos e estado não sensível serializado na URL. Não inclui linguagem textual, SQL livre, API pública ou
  exportação. Sem criar schema, `SavedFilter` é reutilizado no quadro padrão — ou primeiro quadro — do projeto como
  âncora técnica, com `scope: project-items-v1`; filtros desse escopo não aparecem como filtros comuns do Kanban.
  Projetos sem quadro podem executar a consulta e compartilhar sua URL, mas não salvá-la. (2026-09-03)
