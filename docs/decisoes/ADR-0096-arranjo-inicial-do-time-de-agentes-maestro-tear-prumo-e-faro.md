---
id: ADR-0096
titulo: "Arranjo inicial do time de agentes Maestro, Tear, Prumo e Farol no Maestri"
status: substituida # substituida por ADR-0097, ADR-0103, ADR-0104
data: 2026-10-07
origem: "DECISIONS.md D96"
substitui: ""
substituida_por: "ADR-0097, ADR-0103, ADR-0104"
---

# ADR-0096 · Arranjo inicial do time de agentes Maestro, Tear, Prumo e Farol no Maestri

## Decisão

- **D96** — Time de agentes no Maestri, solicitado e fechado pelo PO em 2026-10-07. O terminal Maestro (Claude Code) passa a ser o planejador principal: recebe a demanda do PO, planeja, despacha, aceita e faz os commits locais. O antigo terminal Planejador (Codex) foi convertido no lugar em executor, com o nome Tear e o papel `Executor Prisma`. Um novo terminal Antigravity com Claude Opus 5.5, Prumo, é o revisor, papel `Revisor Prisma`, somente leitura e acionado quando o planejador julgar necessário. Um novo terminal Codex, Farol, é o QA, papel `QA Prisma`: testa de acordo com a tarefa repassada pelo planejador, no navegador (portal) do próprio Maestri, e só escreve testes automatizados quando a tarefa pedir. Fluxo: PO -> Maestro -> Tear -> Prumo (se necessário) -> Farol -> aceite e commit do Maestro. Toda comunicação passa pelo Maestro. Trabalho sequencial na raiz, um escritor por vez; worktree por entrega continua disponível conforme D92 quando houver paralelismo. Executor não faz commit (`agents/executor.yaml` preservado); push, PR e deploy exigem autorização explícita do PO. Por escolha do PO, os três agentes rodam sem prompts de aprovação (`codex --yolo` e `agy --dangerously-skip-permissions`); as proibições de commit, push, deploy e produção valem pelo papel e pelos gates humanos. O terminal Antigravity anterior permanece no canvas sem papel, apenas com as notas do fichário. Os papéis são restritos ao workspace Prisma; o papel global `Planejador reserva` e o papel `Revisor` de outro projeto não foram alterados. Esta decisão substitui, em D92, a orientação de reutilizar o terminal Planejador como PM.
