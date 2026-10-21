---
id: ADR-0073
titulo: "Classificação de projetos por Natureza funcional e Tipo de Trabalho"
status: aceita
data: 2026-09-03
origem: "DECISIONS.md D73"
substitui: ""
---

# ADR-0073 · Classificação de projetos por Natureza funcional e Tipo de Trabalho

## Decisão

- **D73** — A organização do trabalho passa a distinguir três naturezas funcionais escolhidas explicitamente na
  criação: **Projeto**, para iniciativa temporária com início, fim e entrega; **Melhoria**, para evolução incremental
  ou contínua; e **Sustentação**, para operação, suporte, manutenção ou correção. As subdivisões internas continuam
  configuráveis pela pessoa autorizada e não são geradas nem sugeridas por IA. A implementação depende da definição
  técnica na `SPEC-WORK-NATURE`, de `G-SPEC` e de `G-MIGRATION` com estratégia de compatibilidade para dados existentes;
  eventual automação de etapas também exige `G-WORKFLOW`. (2026-09-03)

  **Complemento aprovado:** a estrutura classificada é exatamente o `Project`; `Solicitações` permanece como módulo
  externo e não será substituído por uma entidade central `Demand`. Cada projeto também recebe um Tipo de Trabalho
  fechado entre Desenvolvimento, Infraestrutura, Banco de Dados, Suporte, Segurança, Dados/BI, Integração,
  Documentação e Gestão. Novos projetos exigem os dois valores; registros anteriores ficam tecnicamente como
  `Não classificado` até edição explícita para evitar atribuição falsa. As classificações são exibidas e filtráveis,
  podem ser alteradas pela gestão autorizada com auditoria e não alteram workflow automaticamente. (2026-09-04)
