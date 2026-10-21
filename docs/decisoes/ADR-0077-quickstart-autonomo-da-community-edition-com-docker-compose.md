---
id: ADR-0077
titulo: "Quickstart autônomo da Community Edition com Docker Compose v2"
status: aceita
data: 2026-09-05
origem: "DECISIONS.md D77"
substitui: ""
---

# ADR-0077 · Quickstart autônomo da Community Edition com Docker Compose v2

## Decisão

- **D77** — O quickstart autônomo da Community Edition usa Docker Compose v2 com aplicação e SQL Server isolados da
  infraestrutura Nordevs, segredos aleatórios em `.env` não versionado, healthchecks e volumes persistentes separados
  para banco, anexos e Data Protection. O perfil local padrão usa SQL Server 2022 Developer somente para
  desenvolvimento/testes e exige aceite explícito da EULA; produção requer edição/instância devidamente licenciada e
  gestão externa de segredos. O banco não publica a porta 1433 no host por padrão. (2026-09-05)
