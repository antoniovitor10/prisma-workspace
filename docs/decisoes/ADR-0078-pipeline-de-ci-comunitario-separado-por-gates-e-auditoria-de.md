---
id: ADR-0078
titulo: "Pipeline de CI comunitário separado por gates e auditoria de segurança"
status: substituida # substituida por ADR-0103
data: 2026-09-05
origem: "DECISIONS.md D78"
substitui: ""
substituida_por: "ADR-0103"
---

# ADR-0078 · Pipeline de CI comunitário separado por gates e auditoria de segurança

## Decisão

- **D78** — O CI comunitário separa gates de backend, frontend, governança e imagem Docker; usa permissões mínimas,
  cancelamento de execuções obsoletas e Actions fixadas por SHA, mantidas pelo Dependabot. Dependency Review será
  ativado na promoção pública, pois no repositório privado pessoal sua disponibilidade não é garantida. Advisories
  críticos bloqueiam o frontend; o passivo alto/moderado conhecido é corrigido em lotes testados, sem `audit fix`
  major automático. (2026-09-05)
