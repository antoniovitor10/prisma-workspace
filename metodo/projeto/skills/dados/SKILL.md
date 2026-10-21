---
name: dados
description: Modelo de dados, migrations do EF Core, isolamento por organização, histórico imutável e dados sensíveis do Prisma WorkSpace no SQL Server. Use em qualquer task que mexa em entidade, DbContext, configuração Fluent, migration, consulta, seed, backfill, banco de teste ou dado pessoal.
metadata:
  origem: projeto
  versao: "1"
---

# Dados

## Regras

- **Migration nova só com G-MIGRATION do Vitor** registrado na task (Esclarecimentos) ou em ADR, antes de gerar o arquivo (princípio 2). Toda task com migration é `risco: alto` e espera o ok do Vitor para integrar.
- **Nunca editar migration aplicada.** A cadeia é incremental do início ao fim (D80): nada de migration inicial única nem de consolidação fora do lote final aprovado.
- **Nome descritivo** no padrão do repositório: `<timestamp>_<Acao_Objeto>` (`Add_StageHistory_Table`, `Remove_Stage_WipLimit`, `Add_MethodGit_Security_And_Webhook_Tables`).
- **Todo `Up` tem `Down` funcional**, que desfaz na ordem inversa (`20261016110439_Add_MethodGit_Security_And_Webhook_Tables.cs:799-811`). Mudança destrutiva ou backfill exige ensaio Up/Down/Up em cópia restaurada e backup antes (ADR-0080, antiga D80).
- **A API aplica as migrations ao subir** (`src/Prisma.Workspace.Api/Program.cs:344`). Por isso o deploy compara o manifesto de migrations da imagem com o aprovado e **para** se mudou (`scripts/deploy-production.py:111-114`); a produção só recebe migration nova por operação manual do Vitor (skill `deploy`).
- **Todo dado pertence a uma organização.** Entidade de tenant implementa `IOrganizationOwned` (`src/Prisma.Workspace.Domain/Interfaces/IOrganizationOwned.cs:7`); o `AppDbContext` preenche o `OrganizationId` e recusa gravar dado de outra organização (`src/Prisma.Workspace.Infrastructure/Persistence/AppDbContext.cs:207-221`) e aplica filtro global por organização (linhas 227-274). Entidade nova de tenant entra nos dois lugares. Nunca use `IgnoreQueryFilters()` sem justificar na task.
- **Histórico é imutável** (princípio 13, G-HISTORY): `StageHistory` (permanência por coluna, base do lead time), `AuditLog` (capturado em `CaptureAuditEntries`, `AppDbContext.cs:141,179`) e o histórico de movimentação do método (`Services/MethodSecurity/MethodMovementRecorder.cs`). Não se apaga, não se reescreve e não muda de estrutura sem o Vitor.
- **Configuração por entidade** em `Persistence/Configurations/<Entidade>Configuration.cs` (Fluent API), nunca por atributo na entidade.
- **Bancos de teste** têm prefixo obrigatório: `DetranKanban_E2E*`, `DetranKanban_MigrationTest_*`, `Prisma_BoardColumns_*`; os scripts recusam qualquer outro nome.

## Modelo (resumo)

| Área | Entidades principais | Onde |
|---|---|---|
| tenant e acesso | `Organization`, `OrganizationMember`, `PermissionGrant`, `Team`, Identity (`IdentityUser`), `RefreshToken` | `Domain/Entities/Organization.cs`, `Team.cs`, `RefreshToken.cs` |
| trabalho | `Project`, `Board`, `Stage`, `WorkItem`, `WorkItemAssignee`, `Tag`, `ChecklistItem`, `Comment`, `Attachment`, `TaskType` | `Domain/Entities/` |
| fluxo e histórico | `WorkflowStatus`, `OrganizationWorkflow*`, `StageHistory`, `TaskEvent`, `AuditLog` | idem |
| Scrum e horas | `Sprint`, `TimeEntry`, `DayJustification` | idem |
| módulos | `WikiPage*`, `ExternalPortal`, `ExternalForm`, `Notification`, `SavedFilter`, `SavedReport`, `AutomationRule`, `Approval`, `Client` | idem |
| IA | `AiAssistant.cs` (conexões, conversas, mensagens, uso) | idem |
| método Git | `MethodGit.cs`, `MethodSecurity.cs` (tokens, mapeamento de colunas, entregas de webhook, fatos aplicados) | idem |
| instalação | `InstallationState` (singleton, não reabre: `AppDbContext.cs:199-205`) | idem |

29 migrations em `src/Prisma.Workspace.Infrastructure/Persistence/Migrations/`, a última `20261016110439_Add_MethodGit_Security_And_Webhook_Tables`. Regras de negócio de cada área ficam na skill `dominio`.

## Dados sensíveis

- Pessoais: nome, e-mail e foto de usuários, comentários, anexos, mensagens do portal externo, conversas com a IA. A IA mascara dados antes de enviar ao provedor (`Services/AiRedactionService.cs:9,31`, máscara `[oculto]`); conversa de membro desativado é apagada (`AppDbContext.cs:168-177`).
- Segredos: hash de tokens do método (`MethodApiTokens`), refresh tokens, credenciais de conexões de IA. Nunca em log, relatório ou seed.
- Dado real da produção (DETRAN) nunca vem para teste, fixture, print ou relatório.

## Exemplos do projeto

- `src/Prisma.Workspace.Infrastructure/Persistence/Configurations/StageHistoryConfiguration.cs:13-44`: configuração Fluent de histórico.
- `tests/Prisma.Workspace.Tests/IndependentBoardColumnsMigrationTests.cs` e `StageToProjectBackfillTests.cs`: testes de migration e backfill.
- `scripts/deployment-manifest.py:1-7`: hash das migrations que o deploy compara.

## Armadilhas conhecidas

- O banco de produção é `PrismaWorkspace` no contêiner `detran-kanban-db`, na mesma máquina dos agentes. Nunca conecte nele.
- `CurrentMethodProjectId` restringe o filtro de `Project` e `StageHistory` quando o acesso vem por token do método (`AppDbContext.cs:239,274`): teste com token e com login humano.
- A gravação que toca o método abre transação `Serializable` (`AppDbContext.cs:151-160`); não abra outra transação por fora sem necessidade.

## Origem das regras

- `AGENTS.md` antigo (Convenções: migrations, async), D4, D5, D52, D58, D80, D89, D91, D102.
- `docs/installation/deployment.md:23-26`, arquivos citados acima.
