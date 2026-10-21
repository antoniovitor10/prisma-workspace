---
id: ADR-0028
titulo: "Fronteiras persistentes distintas entre Comment interno e ExternalRequestMessage"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D28"
substitui: ""
---

# ADR-0028 · Fronteiras persistentes distintas entre Comment interno e ExternalRequestMessage

## Decisão

- **D28** — Comunicação externa e colaboração interna usam fronteiras persistentes distintas.
  `Comment` é sempre interno e só circula em endpoints autenticados da tarefa;
  `ExternalRequestMessage` é sempre uma resposta pública e pode ser devolvida ao solicitante.
  A consulta por protocolo usa um DTO público mínimo, sem triagem, roteamento, responsáveis,
  campos privados, eventos internos ou horas. Anexos só atravessam essa fronteira quando
  `IsExternalVisible` estiver explicitamente marcado. (2026-07-20)
