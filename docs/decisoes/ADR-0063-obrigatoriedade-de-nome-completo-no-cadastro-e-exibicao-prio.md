---
id: ADR-0063
titulo: "Obrigatoriedade de nome completo no cadastro e exibição prioritária na interface"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D63"
substitui: ""
---

# ADR-0063 · Obrigatoriedade de nome completo no cadastro e exibição prioritária na interface

## Decisão

- **D63** — Nome completo é obrigatório no cadastro de novos usuários. Toda interface e todo histórico exibem o
  nome como identificação principal e ocultam o e-mail quando o nome existir. E-mail pode aparecer como fallback
  somente para dados legados sem nome; ausência de ambos usa identificação neutra sem inventar autoria. Snapshots
  históricos preservam o nome observado no momento do evento. Esta decisão especializa a regra de identificação
  funcional da D46/D47; contratos de autenticação e cadastro serão detalhados em revisão própria. (2026-08-24)
