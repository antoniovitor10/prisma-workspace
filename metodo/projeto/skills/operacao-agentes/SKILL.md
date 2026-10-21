---
name: operacao-agentes
description: Como o time de agentes do Prisma WorkSpace trabalha desde 2026-10-21 - Maestro, Agulha (agy), Linha (Claude Code) e Esquadro (Codex), quem implementa, testa, revisa e integra, briefing e relatório, worktrees por agente, faixas de portas e projetos Docker, carga do servidor (uptime), trava do navegador (flock), limpeza só pelo nome e produção intocável na mesma máquina. Use ao delegar, ao executar ou revisar como agente no servidor Contabo, antes de subir ambiente, rodar Playwright ou execução pesada, e ao encerrar uma sessão.
metadata:
  origem: projeto
  versao: "1"
  revisar_em: "2026-12-31"
---

# Operação dos agentes

## Quem faz o quê (ADR-0104)

| Agente | Ferramenta | Onde | Papel no método |
|---|---|---|---|
| Maestro | Claude Code | PC do Vitor | arquiteto e planejador; despacha as tasks, recebe os relatórios e integra na `main` (autonomia 2: só com o revisor aprovando e o CI verde; risco alto espera o Vitor) |
| Agulha | agy (Antigravity) | servidor Contabo | implementador ou testador |
| Linha | Claude Code | servidor Contabo | implementador ou testador |
| Esquadro | Codex | servidor Contabo | revisor de código e revisor de segurança (único terminal Codex) |

- **Quem revisa ou testa nunca é quem implementou a mesma task.** Se a Agulha implementa, a Linha testa, e vice-versa; o Esquadro revisa as duas.
- O Vitor autoriza as tasks (move para `em-desenvolvimento` na UI do Prisma) e decide o risco alto.
- Agentes não criam agentes, não chamam outros agentes e não falam entre si: a comunicação passa pelo Maestro. Sem polling: nenhum agente fica esperando trabalho futuro.
- Não ampliar escopo em silêncio: o que estiver fora do briefing vira pergunta ou proposta no relatório.

## Briefing e relatório

- O Maestro manda a task (arquivo autossuficiente em `docs/trabalho/`), as skills obrigatórias, a worktree e a branch, o SHA base, os arquivos permitidos, os comandos de verificação e onde gravar o relatório.
- Agentes do servidor não têm Batuta nem ai-memory. O plano vem no briefing; o relatório volta na resposta ao Maestro, com a saída real dos comandos (`verificar-antes-de-concluir`).
- O registro da task é o relatório na própria task e os comentários no Prisma. **Neste projeto não existem mais `PROGRESS.md` nem `NOTAS.md`** (decisão do Vitor de 2026-10-21): a regra geral de registrar em `NOTAS.md` é cumprida pelo relatório da task e, em sessão interrompida, pela seção Handoff (`passar-bastao`).
- Status no Prisma: o token de agente nunca move para `em-desenvolvimento`; `status:` nunca vai no frontmatter; o webhook move `em-revisao` (PR aberto, não rascunho) e `em-teste` (merge).

## Git e worktrees

- `/home/agentes/projetos/prisma` serve só para `git fetch`. Cada trabalho acontece numa worktree própria: `/home/agentes/projetos/prisma-wt/<agente>-<branch>` (ex.: `linha-task-T-0007-botao`), criada a partir da base do briefing com `git worktree add -b <branch> <caminho> <base>`.
- Uma task, uma branch, uma worktree, mesmo que a entrega toque API, frontend e landing. Dois agentes nunca editam a mesma worktree. Nunca mexa na worktree de outro agente.
- Branch `task/T-XXXX-slug`; commit `{tipo}(T-XXXX): {resumo em inglês}`; título do PR `[T-XXXX] título`.
- Push só da própria branch. Nunca push na `main`, merge, `gh pr merge`, `gh pr ready`, `gh workflow run`, release, tag ou deploy (princípio 1). Nunca force push em branch que não seja sua.
- PR aberto pelo agente nasce **rascunho**, salvo ordem do Maestro; quem marca como pronto (e com isso move a task para `em-revisao`) é o Maestro.
- Ao terminar, remova a própria worktree (`git worktree remove <caminho>`) se o trabalho já está no remoto.

## Portas, Docker e ambiente

| Agente | Portas | Projeto Docker |
|---|---|---|
| Agulha | 3140 a 3149 | o do briefing |
| Linha | 3150 a 3159 | `prisma-linha` |
| Esquadro | 3160 a 3169 | o do briefing |
| proibidos | 3100/3102 (`prisma-dev`, demonstração), 3130 a 3139 (`prisma-metodo`) | `prisma-dev`, `prisma-metodo` |

- App exposto escuta em `0.0.0.0` numa porta da sua faixa e abre em `https://<porta>.dev.169-58-61-117.sslip.io` (usuário e senha com o Vitor).
- Os scripts do servidor estão fixos em `prisma-dev` (skill `stack`): não os rode como estão. Para E2E use `scripts/ci-e2e.sh` com imagem de nome próprio (`prisma-<agente>-e2e:<sha>`, skill `testes`), que não abre porta no host.
- Nunca use a tag `prisma-workspace:<sha>`: é o nome das imagens que o deploy carrega na produção, no mesmo Docker.
- Limpeza só do que você criou, pelo nome (imagens, contêineres, volumes, bancos). Nunca `prune`.
- Configuração só por variável de ambiente; nunca imprima nem grave segredo em arquivo versionado, relatório ou log; nunca commite `.env*`.

## Carga do servidor e navegador

- Antes de execução pesada (build da imagem, E2E inteiro, xUnit ou Vitest inteiro), rode `uptime`. Se a carga de 1 minuto passar de **14**, espere e confira de novo.
- Todo Playwright roda dentro da trava compartilhada: `flock /home/agentes/projetos/.trava-navegador <comando>`.
- Execuções focadas primeiro; a suíte inteira só quando o briefing pedir ou antes da entrega.

## Produção na mesma máquina

A produção que atende o DETRAN roda neste servidor. Nunca toque nos contêineres `prisma-workspace-api`, `prisma-ai-bridge`, `detran-kanban-*` e `prisma-workspace-api-rollback-*`, nem em `/home/dev`, `/opt`, Caddy, firewall, cron ou SSH; nunca rode `ssh vpscontaboo` nem `scripts/deploy-production.py` e nunca leia `method-git.env` (princípio 14, skill `areas-sensiveis`). Também não mexa em pastas, contêineres e portas de outros projetos (distribuidora, cooperarpay, yumply, iflores e demais).

## Outras regras do time

- Migration nova só com G-MIGRATION do Vitor (skill `dados`).
- Teste vermelho é diagnóstico; typecheck é `npx tsc -b` (skill `testes`).
- Não modificar hooks do Claude Code, do Codex ou do agy. Não enviar mensagem externa (e-mail, chat, issue de terceiros). Não ler históricos de conversas sem conferir nomes e identificadores com o Vitor.
- Textos sem emojis; código em inglês, texto em pt-BR.

## Armadilhas conhecidas

- O checkout principal pode estar em outra branch (estava em `docs/spec-metodo-git` em 2026-10-21): sempre crie a worktree a partir de `origin/<base>` depois do `git fetch`, nunca do `HEAD` local.
- `gh run view --log` pode voltar vazio; use `gh api repos/antoniovitor10/prisma-workspace/actions/jobs/<id>/logs`.
- Outras worktrees antigas (Oficina, metodo-git, maestro-int, lupa, horas) existem no servidor e não são suas.

## Origem das regras

- `AGENTS.md` antigo (Coordenação e memória), D96 e D97 (substituídos na parte de processo pelo ADR-0103 e pelo ADR-0104), D98.
- Instruções do Vitor e do Maestro aos agentes do servidor (2026-10-21): portas, `flock`, `uptime`, prune proibido, produção intocável, PR em rascunho.
