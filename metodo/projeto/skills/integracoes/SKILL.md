---
name: integracoes
description: Integrações externas e internas do Prisma WorkSpace - GitHub App e webhook do método Git, servidor MCP do rastreador (tools/prisma-mcp), provedores de IA por API e a ponte das CLIs de IA (prisma-ai-bridge), e-mail SMTP do portal e convites, SignalR do quadro - com configuração, autenticação, onde está no código e armadilhas. Use em task que mexa em qualquer uma delas, ao configurar o rastreador prisma e ao depurar webhook, MCP, chat de IA ou e-mail.
metadata:
  origem: projeto
  versao: "1"
  revisar_em: "2026-12-31"
---

# Integrações

## Regras

- Configuração só por variável de ambiente ou configuração do contêiner; as chaves sensíveis ficam **vazias** em `src/Prisma.Workspace.Api/appsettings.json` (princípio 4). Nunca grave token, segredo de webhook, chave privada da GitHub App, senha SMTP ou credencial de IA em arquivo versionado, log ou relatório.
- Integração real com terceiros em teste só com dublê (GitHub falso, provedor de IA falso). Nada de chamar o GitHub, provedores de IA ou SMTP reais a partir de testes.
- Toda mudança aqui é área sensível (skill `areas-sensiveis`) e passa pela revisão de segurança.

## Método Git (GitHub App e webhook)

- **Para que serve:** o Prisma acompanha os PRs do repositório de um projeto do método e move a task sozinho: PR aberto e não rascunho vai para `em-revisao`; merge vai para `em-teste` (D100 a D102, corte 2a).
- **Auth:** GitHub App instalada por repositório, permissão mínima (leitura de PRs e reviews); webhook assinado por HMAC SHA-256, conferido em tempo constante (`src/Prisma.Workspace.Infrastructure/Services/MethodGit/GitHubCryptography.cs:18`, cabeçalho `X-Hub-Signature-256` em `src/Prisma.Workspace.Api/Controllers/MethodGitWebhookController.cs:22`).
- **Configuração:** `MethodGit:Enabled` (sem ele o webhook responde 503, `MethodGitWebhookController.cs:56`), `MethodGit:GitHubApiBaseUrl` (padrão `https://api.github.com/`, `GitHubProvider.cs:40`; os testes apontam para o GitHub falso) e `MethodGit:Secrets:*` lidos por `ConfigurationMethodGitSecretStore.cs`. Em produção os valores vêm de `method-git.env`, que nenhum agente lê.
- **Onde:** `Services/MethodGit/` (`GitWebhookIngress`, `GitDeliveryProcessor`, `GitDeliveryWorker`, `EfMethodGitStore`, `MethodProjectProvisioner`), `Services/MethodSecurity/` (matriz de transições, guarda de escrita, histórico), controllers `MethodGit*` e `Method*`. Contratos em `docs/method-git/contracts-phase2a.md`; spec de referência `specs/metodo-git-integration.md`.
- **Estado em 2026-10-21:** ligado em produção só para o repositório `cooperarpay`, com a App `prisma-metodo-vitor`. Para este repositório falta criar o projeto `prisma-workspace` na organização Nordevs e instalar a App aqui. Fases 2b e 3 não aprovadas.

## MCP do rastreador (`tools/prisma-mcp`)

- **Para que serve:** as 8 operações do contrato do método (`regras_do_projeto`, `obter_task`, `listar_tasks`, `proximas_tasks`, `criar_task`, `mover_status`, `comentar`, `vincular_pr`) por stdio, chamando a API REST (`tools/prisma-mcp/README.md:17-30`). Também importa tasks de `docs/trabalho/HU-*` (`npm run import`, mesmo README).
- **Auth:** token opaco do método (`pmt_` + 43 caracteres), com papel `agente`, `humano` ou `sistema`, emitido na UI do projeto e mostrado uma vez. JWT de login não é aceito. Com 401 ou 403 o adaptador manda parar e avisar.
- **Configuração:** `PRISMA_URL`, `PRISMA_TOKEN`, `PRISMA_PROJECT` (obrigatórias, `tools/prisma-mcp/src/config.ts:12-24`), `PRISMA_BOARD` e `PRISMA_ROLE` (opcionais). Neste projeto `prisma.url` é `https://prisma.nordevs.com.br` (`metodo/projeto/config.yaml`).
- **Regra do papel `agente`:** nenhum token autoriza task nem libera retomada; isso é ação de uma pessoa na UI.
- **Onde:** `tools/prisma-mcp/src/` (`server.ts`, `api.ts`, `rules.ts`, `tasks.ts`, `import.ts`), testes em `tools/prisma-mcp/test`, Node >= 22. Os testes não rodam no CI.

## IA (provedores e ponte CLI)

- **Para que serve:** assistente de IA do produto (D91): leitura do workspace, chat privado, sugestões de escrita com aprovação humana.
- **Provedores por API:** OpenAI, Anthropic, Gemini, OpenRouter e compatíveis com OpenAI, com chave cadastrada pela organização (`src/Prisma.Workspace.Infrastructure/Services/Ai/AiHttpProvider.cs:25-31`). A chave fica cifrada no banco (`EncryptedSecret`, `src/Prisma.Workspace.Domain/Entities/AiAssistant.cs:14`, aberta pelo Data Protection em `Services/Ai/AiService.cs:236`), nunca em log.
- **Ponte CLI (assinatura):** contêiner `prisma-ai-bridge` (`infra/ai-bridge/server.py`), adaptadores `claude`, `codex` e `gemini` sem ferramentas nem MCP (linhas 18-35); a API fala com ela em `Ai:BridgeUrl` (padrão `http://prisma-ai-bridge:8080/v1`, `Services/Ai/AiService.cs:76`), autenticada por `Ai:BridgeToken` (`Services/Ai/AiService.cs:236`, `AiConnectionOnboarding.cs:120`). As credenciais de assinatura ficam nos volumes `prisma_ai_*_auth` da produção.
- **Privacidade:** texto passa por `AiRedactionService` antes de sair (`Services/AiRedactionService.cs:31`).
- **Onde:** `Application/Features/Ai`, `Infrastructure/Services/Ai/`, `Controllers/AiController.cs`, testes `AiFoundationTests.cs`, `AiRedactionTests.cs`, `infra/ai-bridge/test_bridge.py`, E2E `e2e/ai-assistant.spec.ts`.

## E-mail (SMTP)

- Convites, portal externo e notificações por `PortalEmailSender` (`src/Prisma.Workspace.Infrastructure/InfrastructureServiceExtensions.cs:70-72`), configurado em `PortalEmail:Host`, `Port`, `Sender`, `User`, `Password`, `EnableSsl` (`src/Prisma.Workspace.Api/appsettings.json:30-37`, vazios). Sem SMTP a instalação funciona: o primeiro usuário nasce confirmado (D79).

## Tempo real (SignalR)

- Hub `BoardHub` em `/hubs/boards` (`src/Prisma.Workspace.Api/Program.cs:324`), com `[Authorize]` (`src/Prisma.Workspace.Api/Realtime/BoardHub.cs:13`); o frontend usa `@microsoft/signalr`. Atualiza o quadro entre sessões.

## Armadilhas conhecidas

- O Prisma é o rastreador deste próprio projeto: se um deploy derrubar a produção, o MCP para junto. Hotfix do Prisma segue pela skill `hotfix-producao` sem rastreador e é registrado depois (ADR-0105).
- Os 34 E2E do método Git só rodam com `E2E_METHOD_GIT=1` (skill `testes`); sem isso, uma regressão no webhook não aparece no CI.
- D102 (parâmetros do corte 2a: validade de token, janelas, quadro do método) está como proposta, não aceita: não trate os valores como definitivos.

## Origem das regras

- D79, D91, D94, D95, D100, D101, D102; `tools/prisma-mcp/README.md`; `docs/method-git/contracts-phase2a.md`; arquivos citados acima.
- Decisões do Vitor de 2026-10-21 (rastreador prisma na organização Nordevs, ADR-0105).
