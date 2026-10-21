---
id: ADR-0036
titulo: "Hierarquia funcional Organização, Equipe, Projeto, Tarefa, Subtarefa e Checklist"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D36"
substitui: ""
---

# ADR-0036 · Hierarquia funcional Organização, Equipe, Projeto, Tarefa, Subtarefa e Checklist

## Decisão

- **D36** — A hierarquia funcional apresentada ao usuário será **Organização → Equipe → Projeto →
  Tarefa → Subtarefa → Checklist**; em projetos de desenvolvimento, os tipos do mesmo `WorkItem`
  formam **Épico → História de usuário → Tarefa → Subtarefa**. `Board`, `Stage`, backlog e sprint
  ~~continuam como configurações e projeções operacionais do projeto, preservando D10/D21 e o banco
  existente~~ não criam níveis organizacionais adicionais; após D52, `Board`/`Stage` são operacionais e
  transversais, enquanto backlog e sprint permanecem ligados ao projeto. `Feature` permanece tipo opcional e
  nunca é um nível obrigatório. (2026-07-20)
