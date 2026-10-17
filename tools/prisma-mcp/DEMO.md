# Roteiro da demonstração (fase 2a)

Ambiente: Prisma pelo túnel SSH (`http://127.0.0.1:13100`), projeto **<projeto>** com o método
provisionado pelo administrador (quadro, sete colunas, campos **ID do metodo**, **HU**, **Arquivo**,
**Cobre**, **Risco** e **PR**). O agente roda no repositório do projeto com o MCP `prisma` em escopo
local e um token de agente em `PRISMA_TOKEN`.

## Antes de começar
1. Túnel aberto. Conferir `http://127.0.0.1:13100/health` = 200.
2. Navegador logado no Prisma, no Kanban do projeto <projeto>.
3. Token de agente emitido na tela de configurações do método e guardado na variável do usuário
   (o valor aparece só uma vez). Claude Code aberto em `<pasta-do-projeto>`; `/mcp` mostra `prisma` conectado.
4. Tasks importadas com `node tools\prisma-mcp\dist\import.js <pasta-do-projeto>\docs\trabalho`
   (com o token de agente, entram em `backlog`; rodar de novo responde `Criados: 0`).

## Passos
1. **Agente lê as regras:** "chame regras_do_projeto e depois listar_tasks". Mostra papel `agente`,
   o projeto do token e as tasks em backlog.
2. **Agente tenta se autorizar:** "mova a T-0002 para em-desenvolvimento". A API recusa (403):
   autorizar é de uma pessoa na UI. O mesmo vale por chamada REST direta com o token.
3. **Humano autoriza na tela:** arrastar o card T-0001 para **Em desenvolvimento** no Kanban.
4. **Agente pega a próxima:** "proximas_tasks". Aparece a T-0001.
5. **Agente comenta:** "comente na T-0001: iniciando a troca da paleta".
6. **Agente bloqueia:** "mova a T-0001 para bloqueada, motivo: aguardando arquivo de logo".
7. **Agente tenta retomar sozinho:** recusado. **Humano libera a retomada** no detalhe do card;
   o agente então volta a T-0001 para em-desenvolvimento.
8. **Vínculo com PR:** "vincule o PR https://github.com/... à T-0001". O vínculo aparece no campo PR.
9. **Webhook (se a integração GitHub estiver cadastrada):** abrir o PR não draft move a T-0001 para
   Em revisão; fechar sem merge volta; o merge na principal leva para Em teste.

## O que dizer
- O conteúdo da task continua no repositório; status, comentários e vínculos de PR ficam no Prisma.
- A regra "agente não autoriza" agora é da API, para qualquer canal; o MCP só repete a recusa.
- No repositório da equipe, onde o método fica fora do Git e o PR não leva o ID da task, a task é
  achada pelo vínculo explícito (`vincular_pr`) e pela branch `task/T-XXXX-...`. PR sem nenhum dos
  dois aparece na lista de **não vinculados** para alguém vincular.
- O selo "Bloqueada" em cards com dependência pendente indica dependência não concluída (D99), e
  não a coluna Bloqueada.
