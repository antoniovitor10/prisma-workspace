---
id: ADR-0100
titulo: "Adaptador MCP stdio para o Prisma atuar como rastreador do método (fase 1)"
status: aceita
data: 2026-10-16
origem: "DECISIONS.md D100"
substitui: ""
---

# ADR-0100 · Adaptador MCP stdio para o Prisma atuar como rastreador do método (fase 1)

## Decisão

- **D100** — Adaptador MCP do método de trabalho com agentes (fase 1 do rastreador Prisma), decidido pelo PO em 2026-10-16. O Prisma passa a poder ser o rastreador de status do método (`rastreador: prisma`): o conteúdo das tasks continua no repositório do projeto; status, comentários e vínculo com PR ficam no Prisma. A fase 1 é um **adaptador fino**: um servidor MCP por stdio em `tools/prisma-mcp`, escrito em **TypeScript (Node 22, SDK oficial `@modelcontextprotocol/sdk`)**, separado da API, que só chama a API REST existente (login JWT, projetos, quadros, estágios, work items, campos personalizados, vínculos e comentários). Sem migration e sem alteração em Domain, Infrastructure ou Api. Isso amplia a stack com um pacote Node fora do front; não introduz linguagem nova. Mapeamentos: o ID da task e da HU do método (`T-0001`, `HU-0001`) fica no campo personalizado de projeto "ID do metodo" (chave estável, um projeto por repositório); as tasks são work items de topo do quadro e a HU não vira work item: fica no campo personalizado "HU" e numa tag de mesmo nome (o Kanban só lista itens sem pai, e pelo contrato o status da HU continua no `hu.md`); `depende_de` vira vínculo de dependência; cada status do método é uma coluna do quadro (Backlog, Pronta, Em desenvolvimento, Em revisão, Em teste, Concluída, Bloqueada). Configuração só por variáveis de ambiente (`PRISMA_URL`, `PRISMA_EMAIL`/`PRISMA_PASSWORD` ou `PRISMA_TOKEN`, `PRISMA_PROJECT`, `PRISMA_ROLE`), nunca em arquivo versionado. O token de papel agente nunca move uma task de `backlog`/`pronta` para `em-desenvolvimento`; na fase 1 isso é checado no adaptador. **Pendência:** levar a matriz de transições por papel e essa recusa para a API (tokens com papel), junto das fases 2 e 3 (webhooks e gate), especificadas em `specs/metodo-git-integration.md`.
