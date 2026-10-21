---
id: ADR-0098
titulo: "Risco aceito de network_mode host nos ambientes de desenvolvimento e QA do servidor"
status: aceita
data: 2026-10-13
origem: "DECISIONS.md D98"
substitui: ""
---

# ADR-0098 · Risco aceito de network_mode host nos ambientes de desenvolvimento e QA do servidor

## Decisão

- **D98** — Rede host no ambiente de desenvolvimento do servidor, risco aceito pelo PO em 2026-10-13. O serviço `app` de `compose.server-dev.yaml` usa `network_mode: host` porque o firewall do servidor bloqueia o tráfego de bridge para o host e agentes não alteram firewall; sem isso a API não alcança o provedor falso dos testes E2E. Consequência conhecida: o app de desenvolvimento alcança o loopback do servidor e outros serviços locais, e fica acessível pela prévia protegida por senha. Atenuantes: connection string fixa no SQL próprio em `127.0.0.1:3102`, scripts recusam outro banco, senhas geradas localmente e fora do Git. Vale somente para os projetos Docker `prisma-dev` e `prisma-qa` (este estendido pelo PO em 2026-10-14, instância de QA visual na porta 3110 com banco próprio); produção e `compose.yaml` não mudam. Os achados baixos da revisão ficam como melhorias do ambiente.
