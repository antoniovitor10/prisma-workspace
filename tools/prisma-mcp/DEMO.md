# Roteiro da demonstração (fase 1)

Ambiente: prisma-dev pelo túnel SSH (`http://127.0.0.1:13100`), projeto **<projeto>**, com as 4 tasks da HU-0001 do repositório de exemplo já importadas (todas em **Em revisão**, com campo HU e tag HU-0001, dependências T-0001 → T-0002 → T-0003/T-0004). O agente roda no repositório do repositório de exemplo com o MCP `prisma` em escopo local (`PRISMA_ROLE=agente`).

## Antes de começar
1. Túnel aberto (terminal Tunel no Batuta). Conferir `http://127.0.0.1:13100/health` = 200.
2. Navegador logado no Prisma, no Kanban do projeto <projeto>.
3. Claude Code aberto em `<pasta-do-projeto>`; `/mcp` mostra `prisma` conectado.
4. Não reordenar nem criar colunas no quadro (o adaptador exige as 7 colunas do método).

## Passos
1. **Importação** (opcional, ao vivo): `node tools\prisma-mcp\dist\import.js <pasta-do-projeto>\docs\trabalho` com `PRISMA_PROJECT=<projeto>` e `PRISMA_ROLE=humano`. Resposta: `Criados: 0; atualizados: 0; sem mudança: 4` (idempotente).
2. **Agente lê as regras:** "chame regras_do_projeto e depois listar_tasks". Mostra as 4 tasks em em-revisao.
3. **Agente tenta se autorizar:** "mova a T-0002 para em-desenvolvimento". O MCP recusa: autorizar é do humano.
4. **Humano autoriza na tela:** arrastar o card T-0001 para **Em desenvolvimento** no Kanban.
5. **Agente pega a próxima:** "proximas_tasks". Aparece a T-0001 (autorizada e sem dependência pendente).
6. **Agente comenta:** "comente na T-0001: iniciando a troca da paleta". O comentário aparece no detalhe do card.
7. **Agente bloqueia e volta:** "mova a T-0001 para bloqueada, motivo: aguardando arquivo de logo" (o card vai para a coluna Bloqueada); depois "volte a T-0001 para em-desenvolvimento" (permitido, porque o bloqueio foi dele).
8. **Vínculo com PR:** "vincule o PR https://github.com/... à T-0001". Campo PR preenchido e comentário no card.

## O que dizer
- O conteúdo da task continua no repositório; status, comentários e PR ficam no Prisma.
- Nesta fase a regra "agente não autoriza" é aplicada pelo adaptador MCP. A proteção na API (tokens com papel) e a integração com o Git (webhooks, reconciliação e gate de merge) estão especificadas em `specs/metodo-git-integration.md`, inclusive para o repositório da equipe, onde o método fica fora do Git e o PR não leva o ID da task: lá a task é achada pelo vínculo explícito e pela branch.
- O selo "Bloqueada" em T-0002 e T-0003 indica dependência ainda não concluída (D99), e não a coluna Bloqueada.
