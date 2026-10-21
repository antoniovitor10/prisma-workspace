---
id: ADR-0097
titulo: "Migração do desenvolvimento dos agentes para o servidor Contabo com Batuta"
status: substituida # substituida por ADR-0103, ADR-0104
data: 2026-10-13
origem: "DECISIONS.md D97"
substitui: "ADR-0096"
substituida_por: "ADR-0103, ADR-0104"
---

# ADR-0097 · Migração do desenvolvimento dos agentes para o servidor Contabo com Batuta

## Decisão

- **D97** — Desenvolvimento migrado para o servidor Contabo e coordenação pelo Batuta, pedido pelo PO em 2026-10-13. Substitui D96 no que trata de onde os agentes rodam e de quem faz commit. O Maestro (Claude Code) continua no PC como planejador: despacha, revisa, integra e publica. Tear (Codex, papel `Executor Prisma`) e Prumo (Claude Code com Opus 5.5 em esforço alto, papel `Revisor Prisma`) rodam no servidor, host SSH `contabo-agentes`, usuário `agentes`, em `/home/agentes/projetos/prisma`, clonado do GitHub. Farol (Codex, papel `QA Prisma`) permanece no PC porque o portal do Batuta só existe aqui. O GitHub fica no meio: o executor faz commit e push somente da branch da tarefa; a `main` só recebe integração do Maestro com autorização do PO, porque push na `main` dispara o deploy de produção (`.github/workflows/ci.yml`). Agente remoto não tem o comando `batuta`, as notas nem o ai-memory: o trecho do plano vai dentro da tarefa, o relatório volta pelo retorno do `ask`, e as regras do servidor são repassadas em toda tarefa. O ambiente de desenvolvimento no servidor usa projeto Docker próprio (`prisma-dev`), banco próprio e portas 3000 a 3999; os containers `prisma-workspace-api` e `prisma-ai-bridge` e `/home/dev/prisma-deploy` são produção e não são tocados por agentes. Segredos e arquivos fora do Git (`.env`, `.local/`) não são copiados para o servidor. Revisor continua somente leitura; gates humanos inalterados.
