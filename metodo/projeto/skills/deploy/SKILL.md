---
name: deploy
description: Como o Prisma WorkSpace chega à produção (prisma.nordevs.com.br) - CI no GitHub Actions, imagem candidata, E2E contra a imagem, publicação por SSH no próprio servidor, backup, verificação de saúde, rollback automático e manual, manifesto de migrations e quem pode publicar. Use em task que mexa em CI, Dockerfile, scripts de deploy ou configuração de produção, ao integrar uma task e ao investigar uma publicação que falhou.
metadata:
  origem: projeto
  versao: "1"
---

# Deploy

## Regras

- **Agente nunca publica** (princípio 1): nada de push na `main`, merge, `gh pr merge`, `gh pr ready` sem ordem, `gh workflow run`, tag, release ou execução de `scripts/deploy-production.py`. Merge na `main` **é** o deploy.
- **Quem integra é o Maestro** (autonomia 2), com o revisor aprovando e o CI do PR verde. Task de risco alto (migration, permissão, dado pessoal, histórico, workflow, deploy, CI) espera o ok do Vitor (`autonomia_risco_alto: 1`).
- **Não existe homologação** (`tem_homologacao: false`): a task conclui depois da integração e o Vitor confere em produção.
- **Migration nova não publica sozinha:** o deploy para se o manifesto de migrations da imagem for diferente do aprovado (`scripts/deploy-production.py:111-114`). Publicar migration exige G-MIGRATION, ensaio em base isolada, backup e atualização manual do manifesto pelo Vitor (`docs/installation/deployment.md:23-26`).
- O PR roda a mesma bateria, sem acesso aos segredos de publicação; o deploy só acontece em `push` ou `workflow_dispatch` na `main` (`.github/workflows/ci.yml:127`).

## Caminho até a produção

1. `push` ou `pull_request` para a `main` dispara o `CI` (`.github/workflows/ci.yml:3-8`); execuções antigas do mesmo ref são canceladas, exceto na `main` (linhas 13-15).
2. Em paralelo: **Backend** (restore e build Release, linhas 18-31), **Frontend** (lint, Vitest em série, build, `npm audit --audit-level=critical`, linhas 34-58), **Context governance** (linhas 60-74) e **Container** (testes Python de deploy e da ponte, `docker build` com label do commit, exporta imagem, sha256 e `migrations.json`, linhas 76-100).
3. **E2E** carrega a candidata e roda `scripts/ci-e2e.sh` com SQL descartável, desktop e mobile (linhas 104-122).
4. **Deploy produção** depois de todos (linha 126), environment `production`, concorrência `prisma-production` sem cancelar publicação em andamento (linhas 130-135): copia imagem e script por `scp` para `/home/dev/prisma-deploy/incoming/<sha>` e executa `deploy-production.py <sha>` por SSH (linhas 144-165). Segredos `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_PORT`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS` ficam no GitHub.
5. `deploy-production.py` confere checksum e label da imagem, compara o manifesto de migrations, faz backup SQL `COPY_ONLY` com `CHECKSUM` e `RESTORE VERIFYONLY` mais cópia dos anexos em `/home/dev/backups/prisma/<data>-<commit>`, renomeia o contêiner atual para `prisma-workspace-api-rollback-<data>`, sobe a candidata na rede `slc_default` e verifica a saúde interna e `https://prisma.nordevs.com.br/health`.

## Rollback

- **Automático:** falha de saúde interna ou pública remove só a candidata e devolve o contêiner anterior (`scripts/deploy-production.py:180-198`, `docs/installation/deployment.md:39-42`).
- **Manual (só o Vitor):** parar e renomear o contêiner atual, devolver o nome ao `rollback-container` registrado no backup e iniciá-lo, preservando os dois e os volumes. Como o manifesto bloqueia mudança de schema, o rollback não desfaz migration.
- Um revert na `main` também publica: é um deploy como outro qualquer.

## O que verificar depois de integrar

- O run do `CI` na `main` com os seis jobs verdes (`gh run list --branch main --limit 1`; `gh run view <id>`), só leitura.
- `https://prisma.nordevs.com.br/health` respondendo. Logs e contêiner de produção não são lidos pelos agentes do servidor.

## Exemplos do projeto

- `.github/workflows/ci.yml:124-165`: job de deploy.
- `scripts/deploy-production.py:14-17`: alvos fixos (`/home/dev/prisma-deploy`, `prisma-workspace-api`, `detran-kanban-db`, `/home/dev/painel-projects/prisma-runtime`).
- `scripts/deployment-manifest.py:1-7`: hash das migrations.
- `scripts/tests/`: testes das travas do deploy (rodam no job Container).

## Armadilhas conhecidas

- Produção, backups e o banco estão na mesma máquina dos agentes: um comando Docker sem nome exato pode atingir a produção. Nunca use `docker compose up --build` na pasta antiga de produção nem limpe imagens, volumes ou backups (`docs/installation/deployment.md:44-46`).
- A `main` não tem proteção de branch nem regra de aprovação no environment `production`: a disciplina do princípio 1 é a única barreira.
- `docs/installation/deployment.md:14` lista quatro segredos; o workflow também lê `DEPLOY_PORT` (porta SSH, padrão 22).

## Origem das regras

- D78, D80, D93, D97; `docs/installation/deployment.md`; `.github/workflows/ci.yml`; `scripts/deploy-production.py` (lido, nunca executado).
- Decisão do Vitor de 2026-10-21 (autonomia 2, risco alto 1, sem homologação).
