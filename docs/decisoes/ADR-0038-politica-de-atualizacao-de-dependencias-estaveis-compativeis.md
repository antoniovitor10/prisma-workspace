---
id: ADR-0038
titulo: "Política de atualização de dependências estáveis compatíveis com .NET 8"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D38"
substitui: ""
---

# ADR-0038 · Política de atualização de dependências estáveis compatíveis com .NET 8

## Decisão

- **D38** — As dependências do fechamento do MVP permanecem nas famílias estáveis compatíveis com
  .NET 8, mas recebem as correções de manutenção e segurança mais recentes dessas famílias. Quando
  uma dependência transitiva vulnerável não puder ser eliminada pelo pacote de nível superior, ela
  será fixada explicitamente na menor família estável compatível e coberta por build, testes e
  auditoria de pacotes. Não serão usadas versões preview. (2026-07-20)
