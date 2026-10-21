---
name: distribuicao-community
description: Regras da distribuição open source do Prisma WorkSpace (Community Edition) - repositório público, licença pendente, snapshot auditado, instalação com Docker Compose e setup de uso único, documentação comunitária, política de segurança, dependências e o que nunca pode entrar no repositório. Use em task que mexa em README, CONTRIBUTING, SECURITY, instalação, compose, setup inicial, release, licença, dependência ou em qualquer arquivo que vá ficar público.
metadata:
  origem: projeto
  versao: "1"
  revisar_em: "2026-12-31"
---

# Distribuição Community

## Regras

- **O repositório `antoniovitor10/prisma-workspace` é público.** Tudo que é versionado (código, HUs, tasks, relatórios, ADRs, comentários de PR) é público. Nada de dado real do DETRAN, nome de pessoa da operação, print de produção, segredo, `.env*` ou sessão de navegador (princípio 4).
- **Licença pendente:** não existe arquivo `LICENSE`. A escolha é do Vitor; nenhum agente cria, troca ou sugere como fato uma licença (`README.md:5-6,58`). Até lá, a visibilidade pública não concede licença de uso.
- **Snapshot por allowlist (D75):** o histórico do repositório antigo `runrun` não é importado nem publicado. Conteúdo institucional, dados reais e configuração de infraestrutura ficam fora.
- **O que nunca entra** (`scripts/audit-public-snapshot.sh:15-39`): `.agent-state/`, `.artifacts/`, `.runlogs/`, `docs/entrada/`, `migracao/`, `*/e2e/.auth/*`, `.env` e `.env.*` (exceto `.env.example`) e arquivos `.bak`, `.docx`, `.pptx`, `.xlsx`, `.7z`. Insumos brutos do cliente vão para `docs/insumos/brutos/`, fora do git.
- **Identidade técnica `Prisma.Workspace`** em solution, projetos, assemblies e namespaces (D76); IDs e classes de migrations antigas e nomes de banco existentes não mudam.
- **Instalação Community (D77, D79):** Docker Compose v2 com app e SQL Server isolados, segredos aleatórios em `.env` não versionado, healthchecks, volumes separados para banco, anexos e Data Protection, banco sem porta 1433 no host. SQL Server 2022 Developer só para desenvolvimento e teste, com aceite explícito da EULA (`scripts/setup-docker.sh --accept-sql-server-eula`). Primeiro acesso por setup anônimo de uso único com token externo (`X-Prisma-Setup-Token`); seed demonstrativo só em Development e opt-in.
- **Superfície neutra:** nenhuma menção institucional ao DETRAN no produto (D68, princípio 15).
- **Dependências:** Dependabot semanal para GitHub Actions, npm e NuGet (`.github/dependabot.yml`); Actions fixadas por SHA; advisory crítico de npm reprova o CI; nada de `npm audit fix` com versão major automática (D78). Dependência nova pede ADR (skill `stack`).
- **Vulnerabilidade:** nunca detalhe em issue, discussão ou PR público; siga `SECURITY.md` (advisory privado).

## Pendências da fase 13 (ROADMAP)

Licença, SBOM, proveniência, versionamento SemVer e primeira release `v0.1.0`, backup e restauração documentados, ensaio de instalação e upgrade em ambiente limpo, habilitar private vulnerability reporting e Dependency Review. Viram HUs quando entrarem na fila.

## Exemplos do projeto

- `compose.yaml:42-54`: app na porta `PRISMA_HTTP_PORT` (padrão 8080) com healthcheck em `/health`.
- `scripts/audit-public-snapshot.sh:7-10`: a auditoria exige `gitleaks` e falha sem ele.
- `docs/open-source/INVENTORY.md`, `BASELINE.md`, `LEGACY-REFERENCES.md`: inventário, linha de base e referências antigas do snapshot.
- `src/Prisma.Workspace.Api/Controllers/SetupController.cs:13`: setup anônimo.

## Armadilhas conhecidas

- `SECURITY.md:11-12` fala de repositório privado; o repositório já é público.
- A auditoria do snapshot não roda no CI: rode `node metodo/nucleo/scripts/segredos.mjs` em toda entrega.

## Origem das regras

- D68, D75, D76, D77, D78, D79, D80; `README.md`, `SECURITY.md`, `CONTRIBUTING.md`, `docs/open-source/*`, `scripts/audit-public-snapshot.sh`, `.github/dependabot.yml`, o antigo `ROADMAP.md` (fase 13, hoje resumida em `metodo/projeto/contexto.md` e no histórico do git).
- Decisão do Vitor de 2026-10-21: método versionado no repositório público.
