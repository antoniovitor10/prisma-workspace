---
name: areas-sensiveis
description: Arquivos, sistemas e áreas do Prisma WorkSpace que não se tocam sem autorização explícita do Vitor - produção na mesma máquina, autenticação, tokens e webhook do método, multi-tenant e permissões, migrations, histórico imutável, workflow, IA e ponte, anexos e portal público, deploy e segredos - e o equivalente no método de cada portão humano antigo (G-SPEC a G-COMPLETION). Use ao planejar ou classificar o risco de uma task, antes de editar qualquer um desses pontos e na revisão de segurança.
metadata:
  origem: projeto
  versao: "1"
---

# Áreas sensíveis

## Nunca tocar (nenhuma task autoriza)

Produção roda **nesta mesma máquina** e atende o DETRAN (princípio 14):

- contêineres `prisma-workspace-api`, `prisma-ai-bridge`, `detran-kanban-*` (inclui `detran-kanban-db`, o banco de produção `PrismaWorkspace`) e `prisma-workspace-api-rollback-*`;
- `/home/dev` (inclui `/home/dev/prisma-deploy` e `/home/dev/backups/prisma`), `/opt`, Caddy, firewall, cron e SSH;
- `method-git.env` (segredos da GitHub App na produção), `scripts/deploy-production.py` (não executar), `ssh vpscontaboo`;
- ambientes de demonstração `prisma-dev` (3100/3102) e `prisma-metodo` (3130 a 3139);
- pastas, contêineres e portas de outros projetos (distribuidora, cooperarpay, yumply, iflores e demais) e worktrees de outros agentes;
- `docker system prune`, `docker image prune`, `docker volume prune` ou qualquer limpeza em massa: remova só o que você criou, pelo nome.

## Arquivos que o agente não lê

Já em `seguranca.arquivos_proibidos` do `config.yaml`: `.env` e `.env.*` (inclui `.env.dev`, `.env.e2e.*`, `.env.qa.*`, `.env.server-*`), `src/Prisma.Workspace.Web/.env.e2e.local`, `src/Prisma.Workspace.Web/e2e/.auth/**` (sessão do navegador), `.local/**` (recuperação privada: nunca carregar em massa, versionar, publicar nem copiar), `.maestri/**`, `.agent-state/**`, `**/secrets.json`, `**/Properties/launchSettings.json`, chaves e certificados, `method-git.env`. Os modelos versionados `.env.example` e `src/Prisma.Workspace.Api/appsettings.json` têm as chaves sensíveis **vazias**: mantenha assim.

## Exige task de risco alto e ok do Vitor

| Área | Onde | Por quê |
|---|---|---|
| migrations e schema (G-MIGRATION) | `Persistence/Migrations/`, entidades, `Configurations/` | a API migra ao subir; produção com dados reais (skill `dados`) |
| histórico imutável (G-HISTORY) | `StageHistory`, `AuditLog`, `Services/MethodSecurity/MethodMovementRecorder.cs` | trilha de auditoria |
| workflow e transições (G-WORKFLOW) | `Features/Workflow`, `OrganizationWorkflow*`, `Services/MethodSecurity/` (matriz de transições do método) | muda o comportamento de todas as equipes |
| autenticação e sessão | `Api/Services/JwtTokenService.cs`, `Infrastructure/Services/RefreshTokenService.cs`, Identity, `AuthController` (anônimo), `SetupController` (anônimo, uso único, D79) | acesso indevido |
| tokens e Git do método | `Api/Authentication/MethodTokenAuthenticationHandler.cs`, `Controllers/MethodTokensController.cs`, `Infrastructure/Identity/MethodTokens/`, `Services/MethodGit/` (`GitHubCryptography.cs:18` HMAC em tempo constante; `MethodGitWebhookController.cs` anônimo, assinado) | um token de agente nunca autoriza task (D101) |
| multi-tenant e permissões | `AppDbContext.cs:207-274`, `IPermissionService`, `IProjectAccessService`, `Middleware/OrganizationContextMiddleware.cs` | vazamento entre organizações; atribuir não concede acesso (D90) |
| IA | `Services/Ai/`, `AiRedactionService.cs`, `infra/ai-bridge` (credenciais de assinatura em volumes `prisma_ai_*_auth`) | dado enviado a terceiros; a IA só sugere escrita (D91) |
| conteúdo de usuário | anexos e wiki (`HtmlSanitizerService.cs`), portal externo (`ExternalPortalController.cs:141`, anônimo) | XSS, upload malicioso, abuso |
| CI e deploy (G-DEPLOY) | `.github/workflows/ci.yml` (job deploy), `scripts/deploy-production.py`, `scripts/deployment-manifest.py`, `Dockerfile` | publica em produção |
| configuração de segurança | `Program.cs:112` (rate limiter), `Middleware/SecurityHeadersMiddleware.cs`, CORS | |

Com `revisao_seguranca: quando-sensivel`, toda task que toca uma linha desta tabela passa pelo revisor de segurança (Esquadro).

## Portões humanos antigos no método

| Gate antigo | No método |
|---|---|
| G-SPEC | "Fecha?" da HU, `clarificar`, `analisar-consistencia` e autorização da task pelo Vitor no Prisma (`backlog` ou `pronta` para `em-desenvolvimento`); PRD aprovado na trilha épico |
| G-MIGRATION | princípio 2; task `risco: alto`; aprovação registrada na task ou em ADR antes de gerar a migration |
| G-WORKFLOW, G-HISTORY | princípio 13; task `risco: alto` |
| G-SCOPE | princípio 6: task `bloqueada` com a pergunta em comentário no Prisma |
| G-DEPLOY | princípio 1: agente nunca integra nem publica; o Maestro integra com revisão aprovada e CI verde; risco alto espera o Vitor |
| G-COMPLETION | `tem_homologacao: false`: no nível 2 a task termina `concluida` depois da integração; o Vitor confere e pode reabrir; `fechar-hu` |

O antigo `.agent-state/gate-decisions.json` não autoriza nada; a autorização é a mudança de status no Prisma, feita pelo Vitor.

## Armadilhas conhecidas

- O repositório é **público** e a `main` não tem proteção de branch: um push na `main` publica direto. Relatório, comentário de PR e task também são públicos.
- `.gitleaks.toml` e `scripts/audit-public-snapshot.sh` existem, mas não rodam no CI; rode `node metodo/nucleo/scripts/segredos.mjs` antes de entregar.
- O app de desenvolvimento do servidor usa `network_mode: host` (D98): ele alcança o loopback da máquina, inclusive serviços de produção. Não exponha porta nova sem necessidade e nunca aponte connection string para fora do seu SQL.

## Origem das regras

- `AGENTS.md` antigo (Constituição 4, 6 e 7; Coordenação: `.local/`), D79, D90, D91, D97, D98, D101.
- Instruções do Vitor aos agentes do servidor (produção intocável, prune proibido), inventário A1 (seção 1.7), `docs/installation/deployment.md`.
