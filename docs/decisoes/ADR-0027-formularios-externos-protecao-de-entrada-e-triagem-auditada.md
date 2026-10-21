---
id: ADR-0027
titulo: "Formulários externos, proteção de entrada e triagem auditada"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D27"
substitui: ""
---

# ADR-0027 · Formulários externos, proteção de entrada e triagem auditada

## Decisão

- **D27** — Formulários externos pertencem ao Portal Externo e persistem definição, campos,
  condições simples, limites de anexo e regras ordenadas de atribuição. Cada envio cria, numa única
  operação, o `ExternalRequest` e o mesmo `WorkItem` de origem externa, preservando os valores
  submetidos e anexos no item interno. A proteção de entrada combina rate limiting nativo do
  ASP.NET Core por endereço/IP, honeypot, tempo mínimo de preenchimento e listas permitidas de
  extensão/MIME. A triagem usa estado explícito e eventos append-only; toda ação administrativa
  fica auditada, inclusive recusa justificada, pedido de informação, roteamento e vínculos.
  (2026-07-20)
