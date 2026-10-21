---
id: ADR-0074
titulo: "Domínio público canônico prisma.nordevs.com.br"
status: aceita
data: 2026-09-04
origem: "DECISIONS.md D74"
substitui: ""
---

# ADR-0074 · Domínio público canônico prisma.nordevs.com.br

## Decisão

- **D74** — O endereço público canônico e exclusivo do Prisma WorkSpace passa a ser
  `https://prisma.nordevs.com.br`. O host `runrun.nordevs.com.br` foi removido do Caddy após validação do novo DNS e
  não funciona como alias; seu registro DNS legado deve ser removido da zona. Links gerados pela aplicação usam o
  domínio Prisma. O TLS é terminado no Caddy do VPS e encaminha para `detran-kanban-api:8080`. (2026-09-04)
