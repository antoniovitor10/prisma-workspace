## 2026-10-20 — Oficina: checkpoint C6 durante espera de navegador

- Implementação C6 em andamento, sem alteração visual antes das capturas. BoardRepository omite quadros de projeto arquivado sem apagá-los; PermissionService e ProjectAccessService usam lote e preservam regras existentes. Interface tem fallback para implementações alternativas, produção EF possui override em lote.
- Testes focados 17/17, SQL QA3111 com banco aleatório próprio criado/removido; 24 projetos, negação e outro tenant. Matriz de todos os perfis/permissões e escopo de tokens cobertos. Contraprova com serviços antigos falhou nos dois defeitos: 6 contra 145 consultas e quadro arquivado ainda listado. Fontes e binários verdes restaurados; 17/17 final passou.
- E2E C6 preparado mas ainda não executado. C1/C2 baseline aguardam mutex compartilhado; specs adicionais de capturas antes C3/C4 preparadas. Não aplicar ajustes visuais até inspecionar capturas antes. Próximo: browser batch, entrega C1 e capturas C2; depois ajustes/novas validações. REVISAO e QA oficiais pendentes; nada em produção.

# Oficina — 2026-10-20, C1 em validação

Goal inteiro: `/home/agentes/projetos/tarefas/GOAL-prisma-oficina.md`. Trabalhar somente nesta worktree e nas próprias futuras, sem produção/main/deploy/migration. Base a2797a7; merges L06/L07/L08 concluídos. Estado externo em `/home/agentes/projetos/ESTADO-goal-oficina.md`.

C1 implementado: seleção explícita de responsável da subtarefa, erros de argumento400, dependências D99 positivas. Testes xUnit3/3 e Vitest9/9 passaram com contraprovas vermelhas reais; tipos/lint passaram. Primeiro teste Vitest novo precisou aguardar a opção assíncrona; não houve aumento de timeout. O script temporário de contraprova frontend teve erro de caminho no trap após trocar cwd: restaurado imediatamente pela cópia preservada e corrigido o trap absoluto; fonte corrigida confirmada e9/9passaram depois. E2E ainda não executou: trava de navegador ocupada por terceiros, nunca interromper os donos. Build frontend em execução.

QA próprio: SQL `prisma-qa-database-1` iniciado pela Oficina; API3120/Vite3121, banco `Prisma_Oficina_QA_20261020`; scripts/logs privados em `.runlogs/oficina`. Derrubar processos e SQL iniciado ao terminar cada item, preservar volumes existentes. Não usar dev3100/3102 nem metodo3130-3139.

Próximo: E2E afetado C1, autorrevisão/auto-QA e relatório; C2 tem apenas novo teste preparado (`qa-round-responsive.spec.ts`, ainda não commitar junto do C1), capturar D04/D15/D18 antes de editar visual. Depois C3-C6, item2 org, estabilidade, auditoria/backlog e varredura, sempre conforme goal. Conversão Brasília depende de comprovar a implementação, pois horas ainda fora da main indicada; pergunta registrada externamente. Não incorporar branch horas reprovada nem declarar revisão/QA oficial.

---


# Continuidade — tarefa 6 entregue
