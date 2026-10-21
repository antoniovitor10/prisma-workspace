# Rastreador Prisma

Usado quando `rastreador: prisma` em `config.yaml`. O Prisma passa a ser a **verdade do status**; o repositório continua sendo a verdade do **conteúdo** (HU, PRD, task).

## Divisão

| Fica no repositório | Fica no Prisma |
|---|---|
| `hu.md`, `prd.md`, `tasks/T-XXXX.md` (objetivo, contexto, critérios, relatório) | status e histórico (quem, quando, por qual canal) |
| ADRs, skills do projeto, `principios.md` | comentários: progresso, bloqueios, link do PR |
| branch `task/T-0007-slug`, PR com `[T-0007]` no título | autorização: tokens com papel humano / agente / sistema |
| | vínculo repositório ↔ projeto ↔ task ↔ PR |

**Em modo prisma o frontmatter da task não tem `status:`.** Duas verdades para o mesmo campo é o erro que este desenho evita. Ao migrar um projeto do modo local para o Prisma, uma importação lê o frontmatter uma vez e o campo é removido.

## Quem fala com quem

```
agente ──MCP──► Prisma (/mcp)          lê task, comenta, bloqueia, vincula PR
agente ──gh/glab──► GitHub/GitLab      branch, commits, PR, checks, merge
GitHub/GitLab ──webhook──► Prisma      branch criada, PR aberto, PR mergeado, deploy
desenvolvedor ──UI──► Prisma           autoriza, acompanha, relatórios
```

O Prisma **escuta** o git e **aplica regras**. Ele não cria branch nem abre PR.

## Operações do MCP (contrato)

| Operação | Uso no método | Quem pode |
|---|---|---|
| `regras_do_projeto()` → status permitidos por papel, autonomia, convenções | no início de toda sessão; substitui `status.*` do `config.yaml` | todos |
| `obter_task(id)` → status, título, caminho do arquivo, PR vinculado | checar e confirmar antes de executar | todos |
| `listar_tasks(status?)` | "quais estão autorizadas?", "qual é a do botão?" | todos |
| `proximas_tasks()` → autorizadas com dependências concluídas | escolher a próxima | todos |
| `criar_task(id, titulo, hu, arquivo, cobre, risco, status?)` | ao quebrar a HU (`backlog`); task de atalho ou incidente pode nascer `concluida` | agente, humano |
| `mover_status(id, novo_status, motivo)` | `bloqueada`, `em-revisao` no nível 0, e transições de agente permitidas | conforme a matriz por papel |
| `comentar(id, texto)` | relatório de execução, bloqueios, handoff | todos |
| `vincular_pr(id, url)` | redundância ao webhook; obrigatório no nível 0 (sem push não há webhook) | agente, humano |

A matriz de transições por papel vive no Prisma. **O token do agente nunca autoriza** (`backlog`/`pronta` → `em-desenvolvimento`): a API recusa. A única volta permitida ao agente é `bloqueada` → `em-desenvolvimento` de um bloqueio que ele mesmo registrou.

## O que o webhook move (fases 2 e 3)

| Evento no git | Transição | Regra |
|---|---|---|
| branch `task/T-0007-*` criada | nenhuma; registra o vínculo | ignora branches fora do padrão |
| PR aberto (não draft) ou pronto para revisão | `em-desenvolvimento → em-revisao` | task identificada por: vínculo explícito > nome da branch > `[T-0007]` no título |
| PR fechado sem merge ou "changes requested" | `em-revisao → em-desenvolvimento` | |
| PR mergeado na branch principal | `em-revisao → em-teste` | só se não há outro PR aberto da mesma task |
| deploy bem-sucedido (fase 3) | `em-teste → concluida` | |

Webhooks se perdem; o Prisma reconcilia periodicamente lendo os PRs recentes. O agente não precisa compensar: se o status não mudou depois do PR, comente e siga.

## Regras para o agente em modo prisma

1. Leia `regras_do_projeto()` no início da sessão.
2. Nunca escreva `status:` no frontmatter. Nunca "corrija" o status no arquivo.
3. Nunca mova um status que o git vai provar (PR aberto, merge). Mova só `bloqueada`, a volta do seu próprio bloqueio e, no nível 0 (sem push), `em-revisao`.
   O status da HU continua no frontmatter de `hu.md` em qualquer modo; só o das tasks vai para o Prisma.
4. Sempre `vincular_pr` depois de abrir o PR, e `comentar` o relatório de execução.
5. Se o MCP não responde: **pare e avise**. Não troque para o modo local.

## Fases de implantação (no Prisma)

1. **MCP de status e comentários**, tokens com papel, máquina de estados, importação do frontmatter. Sem git.
2. **Webhooks** do GitHub/GitLab, idempotentes, com reconciliação. Identificação pela branch e pelo título do PR.
3. **Gate** para o nível 2: o Prisma publica um status `prisma/autorizacao` no commit; a proteção de branch exige esse status para o merge. Deploy observado move para `concluida`.

Até a fase 1 existir, use `rastreador: local`.

## Métricas

As métricas da task (`voltas`, `testes_criados`, `revisao_humana`…) continuam no frontmatter do arquivo, mesmo em modo prisma: são conteúdo, não status. Ao saber que o PR foi aprovado ou mergeado, pergunte ao desenvolvedor por `revisao_humana`, como em `rastreadores/local.md`.
