---
id: ADR-0046
titulo: "Nome funcional de usuário por organização via OrganizationMember.DisplayName"
status: aceita
data: 2026-08-20
origem: "DECISIONS.md D46"
substitui: ""
---

# ADR-0046 · Nome funcional de usuário por organização via OrganizationMember.DisplayName

## Decisão

- **D46** — O nome funcional exibido para responsáveis e participantes pertence à associação do usuário com
  a organização (`OrganizationMember.DisplayName`), permitindo identificação adequada por tenant. O campo é
  editável por administrador da organização. O fallback aceita `UserName` somente quando não tiver formato
  de e-mail; caso contrário, usa o rótulo neutro `Usuário {shortId}`. O e-mail fica restrito a contexto
  administrativo secundário e nunca é o nome principal em superfícies de tarefa. (2026-08-20)
