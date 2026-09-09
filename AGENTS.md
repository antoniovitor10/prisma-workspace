# AGENTS.md — Prisma WorkSpace (.NET + SQL Server)

> **Arquivo-fonte da verdade.** Codex e Antigravity leem isto automaticamente.
> Claude Code lê via `CLAUDE.md` (que aponta pra cá).
> **Antes de codar, SEMPRE leia também:** `DECISIONS.md`, `ROADMAP.md` e as 2 últimas
> entradas de `PROGRESS.md`.

## O que é
**Prisma WorkSpace** — plataforma open source de gestão ágil (Kanban, Scrum, horas,
alocação, dashboards e portal). Community Edition gratuita; Cloud/Enterprise e
serviços (consultoria, treinamentos, suporte, customizações) financiam a evolução.
Identidade visual oficial: D68 (espectro Prisma / LP / mockup de login).

**Escopo completo (tudo dentro):** múltiplos quadros, kanban com drag-and-drop,
subtarefas, alocação de usuários, anexos, contagem de horas, lead time por etapa,
dashboards, Gantt, filtros salvos, ações em massa e automações.

## Stack (não muda sem registrar em DECISIONS.md)
- **Backend:** .NET 8, ASP.NET Core Web API, Clean Architecture
- **ORM:** Entity Framework Core
- **Banco:** SQL Server
- **Auth:** JWT + ASP.NET Core Identity
- **Logs:** Serilog
- **Validação:** FluentValidation
- **Casos de uso:** MediatR (CQRS leve — Command/Query + Handler)
- **Frontend:** React.js (SPA) + Styled Components (ver D7 em DECISIONS.md)

## Estrutura da solution
```
src/Prisma.Workspace.Domain          -> entidades, enums, regras de domínio (sem dependências)
src/Prisma.Workspace.Application      -> casos de uso (MediatR), DTOs, validators, interfaces
src/Prisma.Workspace.Infrastructure   -> EF Core, DbContext, migrations, repositórios
src/Prisma.Workspace.Api              -> controllers/endpoints, DI, middlewares, auth
tests/Prisma.Workspace.Tests          -> xUnit
src/Prisma.Workspace.Web              -> React 19 + TypeScript + Vite + Styled Components
```

## Convenções (valem pra QUALQUER IA — mantêm o código uniforme)
- Entidades em PascalCase singular: `Board`, `Stage`, `WorkItem`, `TimeEntry`.
  (Usamos `WorkItem` em vez de `Task` pra não colidir com `System.Threading.Tasks.Task`.)
- DTOs sufixados com `Dto`: `CreateWorkItemDto`, `WorkItemDetailDto`.
- Um caso de uso = um Command/Query + um Handler. Pasta por feature em Application.
- Controller NÃO tem lógica de negócio: só orquestra o MediatR.
- Migrations com nome descritivo (`Add_StageHistory_Table`). Nunca editar migration já aplicada.
- Tudo que toca banco é `async` (sufixo `Async`).
- Idioma: código e identificadores em inglês; comentários e docs em pt-BR.

## Gate de tipos do frontend — use `tsc -b`

`npx tsc --noEmit` **não checa arquivo nenhum** neste projeto e sempre sai com código 0.
O `tsconfig.json` da raiz tem `"files": []` e só referências de projeto, então o `--noEmit`
resolve a configuração vazia da raiz e não olha `src/`.

O comando correto, dentro de `src/Prisma.Workspace.Web`:

```
npx tsc -b
```

Isso já deixou passar um defeito real em produção: uma chamada com 5 argumentos numa
função de 4 (`TS2554`), que fez a coluna criada pelo Kanban nascer com a categoria
errada. Nunca reporte "typecheck limpo" com base no `--noEmit`.

## Verificação final E2E (obrigatória)

Antes de concluir qualquer tarefa que modifique:
- **Frontend** (componentes React, rotas, lógica de UI)
- **Endpoints da API** (controllers, DTOs, validações, regras de negócio)
- **Schema de banco** (migrations, entidades, relações)

**Você DEVE:**

1. **Garantir ambiente E2E ativo:**
   - API rodando com banco E2E: `.\scripts\run-api-e2e.ps1`
   - Frontend rodando: `npm run dev` (em `src/Prisma.Workspace.Web`)
   - SQL Server acessível

2. **Executar testes E2E:**
   ```bash
   cd src/Prisma.Workspace.Web
   npm run e2e
   ```

3. **Verificar resultado:**
   - **Todos os testes passam:** tarefa pode ser concluída
   - **Testes falham:** investigar e corrigir antes de concluir
   - **Novos testes necessários:** adicionar testes E2E cobrindo a funcionalidade implementada

**Exceções (não exigem E2E):**
- Apenas documentação (README, comentários, specs)
- Configurações de CI/CD sem impacto no comportamento
- Refatorações internas sem mudança de interface pública
- Correções de lint, formatação ou otimizações de build

**Se o ambiente E2E não estiver configurado:**
1. Execute `.\scripts\setup-e2e-database.ps1` para criar o banco dedicado
2. Configure `.env.e2e.local` com credenciais de teste
3. Então execute os testes normalmente

**Documentação:**
- Estrutura e convenções: `src/Prisma.Workspace.Web/e2e/README.md`
- Decisão formal: D40 em `DECISIONS.md`

## Golden rules (multi-IA)
1. Antes de codar: leia `DECISIONS.md` + `ROADMAP.md` + as 2 últimas entradas de `PROGRESS.md`.
2. Mudou arquitetura/biblioteca? Registre em `DECISIONS.md` ANTES de implementar.
3. **Definition of Done de qualquer tarefa inclui escrever 1 entrada em `PROGRESS.md`.**
4. Não re-litigue decisões que já estão em `DECISIONS.md`.
5. Dúvida de escopo: NÃO invente. Pare e pergunte ao PO.
6. Fase e escopo de fase vivem em `ROADMAP.md`. Se a fase não estiver descrita, pare e pergunte ao PO.

## Constituição AI-Native (V0)

### 1. Visão Curta
A arquitetura AI-Native orquestra o desenvolvimento orientado a especificações (SDD), isolamento de contexto por domínio, validação estrita via gates e controle de transições com human gates, garantindo determinismo e zero degradação do código de produto.

### 2. Hierarquia de Autoridade
Em caso de divergência ou ambiguidade, prevalece a seguinte cadeia estrita:
1. Usuário Humano (PO)
2. `AGENTS.md` / `DECISIONS.md` / `ROADMAP.md`
3. Histórias de usuário em `stories/` como intenção funcional humana rastreável
4. Especificações em `specs/*.md` (status `approved`) como contrato funcional e técnico
5. Grafo de Workflow (`workflows/feature.yaml`) e Perfil (`profiles/runrun-loop.yaml`)
6. Agentes (`agents/*.yaml`) e saídas do loop engine

### 3. Spec-Driven Development (SDD)
- Toda mudança funcional começa por uma história de usuário em `stories/`; a IA usa a história para criar ou revisar a spec relacionada.
- Toda alteração em código de produto, entidades ou APIs exige uma especificação aprovada (`specs/*.md` em status `approved`).
- Especificações em status `draft` ou `review` bloqueiam a implementação e exigem o Human Gate `G-SPEC`.
- Histórias não recebem `G-SPEC` individual. A homologação marca aderência ao comportamento e observações geram tarefas de análise/correção conforme D67.
- Mudança da própria história exige revisar a spec e obter novo `G-SPEC` antes de alterar código de produto.

### 4. Context Engineering
- O contexto é carregado estritamente conforme o índice determinístico em `context/index.yaml`.
- Fluxo de carregamento de contexto: task -> AGENTS -> index -> classify -> story -> spec -> code -> tests -> gates -> dependencies -> docs/history sob demanda.
- Fica proibido o carregamento cego do repositório inteiro, mídias brutas, imagens, logs, binários, `node_modules` ou arquivos de configuração com segredos.

### 5. Proibição de Inventar Decisões
- Qualquer requisito, arquitetura ou regra de negócio não coberta por `DECISIONS.md` ou specs aprovadas exige paralisação do agente e abertura de Human Gate `G-SCOPE`.
- Agentes não podem assumir decisões não registradas ou alterar especificações para se adequar a código implementado.

### 6. Human Gates
- Pontos de parada obrigatórios (PAUSED) que exigem intervenção e aprovação humana explícita:
  - `G-SPEC`: Aprovação de especificação (`draft` -> `approved`).
  - `G-MIGRATION`: Alterações de schema no SQL Server ou EF Core Migrations.
  - `G-WORKFLOW`: Alterações nas regras de workflow, transições de status e limites WIP.
  - `G-HISTORY`: Alterações na estrutura imutável de auditoria ou histórico.
  - `G-DEPLOY`: Liberação ou promoção para ambientes institucionais.
  - `G-SCOPE`: Dúvidas ou alterações de escopo não previstas em `DECISIONS.md`.
  - `G-COMPLETION`: Aprovação humana antes da conclusão, exigida somente quando `human_completion_required == true`. Gate condicional: não se aplica a todas as tasks; o profile/classificação determina quando é necessário.
- Nenhum agente possui permissão para autoaprovação de Human Gates.

### 7. Política de Segredos (Secrets Policy)
- Segredos, chaves de API, senhas e tokens de acesso devem permanecer estritamente em variáveis de ambiente externas ou cofre de segredos.
- É expressamente proibido versionar segredos ou incluí-los em arquivos de estado, telemetria (`.agent-state/telemetry/`) ou handoffs (`.agent-state/handoffs/`).

### 8. Paths de Descoberta
- **Histórias de Usuário Canônicas:** `stories/`
- **Especificações Canônicas:** `specs/`
- **Índice de Contexto:** `context/index.yaml`
- **Grafo de Workflow:** `workflows/feature.yaml`
- **Perfil de Execução do Loop:** `profiles/runrun-loop.yaml`
- **Contratos de Agentes:** `agents/`
- **Backlog Estruturado:** `backlog.md`
- **Proposta de Arquitetura AI-Native:** `AI-NATIVE-V0.md`

## Painel de aprovações humanas (Human Gates)

O Context Explorer inclui um painel funcional para visualizar e aprovar Human Gates.

### Como usar

1. Inicie o frontend de desenvolvimento (a API sobe junto automaticamente):
   ```
   cd tools/context-explorer/web
   npm run dev
   ```
   Se preferir rodar a API em terminal separado: `cd tools/context-explorer && node server.js`.
2. Acesse http://localhost:5174 e navegue até "Aprovações humanas" na sidebar.
3. O painel mostra três seções:
   - **Aprovações de especificação (G-SPEC):** specs em draft com botões Aprovar/Rejeitar.
     Aprovar altera o campo `status` diretamente no arquivo `.md` da spec.
   - **Gates operacionais:** G-MIGRATION, G-WORKFLOW, G-HISTORY, G-SCOPE, G-DEPLOY, G-COMPLETION.
     Registram decisões em `.agent-state/gate-decisions.json`.
   - **Histórico de decisões:** timeline de todas as aprovações/rejeições ordenadas por data.
4. A API roda na porta 3847 e expõe:
   - `GET /api/gates` — lista gates com status
   - `POST /api/gates/approve` — aprovar gate
   - `POST /api/gates/reject` — rejeitar gate
   - `GET /api/model` — regenerar e retornar model.json

## Fase atual
**Rebrand Prisma WorkSpace (D68) em andamento — MVP técnico preservado; próximo foco: open source, identidade e gaps do documento de análise (D69).** (Atualizar esta linha a cada fase.)
