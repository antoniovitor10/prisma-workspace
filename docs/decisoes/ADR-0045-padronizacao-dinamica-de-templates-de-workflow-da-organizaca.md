---
id: ADR-0045
titulo: "Padronização dinâmica de templates de workflow da organização para projetos"
status: substituida # substituida por ADR-0065
data: 2026-08-20
origem: "DECISIONS.md D45"
substitui: ""
substituida_por: "ADR-0065"
---

# ADR-0045 · Padronização dinâmica de templates de workflow da organização para projetos

## Decisão

- **D45** — A padronização de workflow será multitenant e dinâmica no sentido **Organização → Projeto**.
  A organização mantém templates de status e transições; cada projeto opera em modo `Inherited` ou
  `Custom`. Projetos herdados acompanham alterações compatíveis do template vinculado, enquanto a primeira
  personalização explícita cria uma configuração independente. Projetos legados entram como `Custom` para
  preservar integralmente o comportamento existente. (2026-08-20)
