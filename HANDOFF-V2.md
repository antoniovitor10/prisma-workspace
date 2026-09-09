# HANDOFF — programa Prisma WorkSpace v2

Estado em 2026-09-09, ao fim da sessão. **Suíte E2E verde: 65 passed, 4 skipped, 0 failed.**
xUnit 140/140, `tsc -b` limpo, Vitest 51/51. Leia `AGENTS.md`, `DECISIONS.md`, `ROADMAP.md`, as duas
últimas entradas de `PROGRESS.md` e `context/index.yaml` antes de tocar em código.

Branch de trabalho: **`integration/all-specs-v2`**, criada a partir de `01bb081`. Nada foi mesclado em `main`.
Nenhum deploy foi feito.

---

## 1. Leia isto primeiro: o que muda em relação ao plano original

O plano inicial do PO previa Sprint ↔ Project **N:N**. **Isso foi revertido** em 2026-09-09 (D84). Se você
encontrar `SprintProject` em qualquer texto antigo, está obsoleto. O contrato vigente é: **uma sprint pertence
a um projeto; um projeto tem várias sprints.**

O plano também previa não fazer deploy. O PO **autorizou deploy sem restrição** em 2026-09-08 (D80), com alvo
`prisma.nordevs.com.br` passando a servir este repositório. Nenhum deploy foi executado ainda.

O PO declarou que **os dados atuais são descartáveis** ("todos os dados atuais são fakes"). Isso dispensa
backfill em vários pontos e reduz o risco das migrations.

---

## 2. O que foi entregue

Vinte e cinco commits na `integration/all-specs-v2`. Todos com build limpo e testes verdes.
A tabela abaixo lista os principais; `git log --oneline 01bb081..HEAD` traz o conjunto completo.

| Commit | Entrega |
|---|---|
| `94980fc` | Bug do "concluído": o quadro lia a etapa do placement e o update gravava só `WorkItem.StageId` |
| `efeff3f` | Auditoria das 40 specs contra o código real |
| `16b76e9` | D81 e D82 registradas |
| `154c74a` | "Novo item" que **sempre** respondia 400: enviava `position: Date.now()` e o validador recusa ≥ 999.999.999.999 |
| `73ca31c` | Classificação de coluna escolhida na tela; antes toda coluna nascia `InProgress`, inclusive "Concluído" |
| `df86d81` | Convite: o retorno de `SendAsync` era descartado, então o e-mail podia não sair sem ninguém saber |
| `47b1629` | Impressão com folha própria e PDF com margem e cabeçalho |
| `8114357`, `fe175c5` | `SPEC-BOARD-AS-VIEW` e D83 |
| `86de6da` | WIP removido do produto |
| `c8df35b`, `2ae75bc` | Módulo de SLA removido por inteiro |
| `b107193` | `WorkItemBoardPlacement` eliminado; a tarefa passa a ter etapa e posição únicas |
| `680a0d1` | D84 reverte o N:N; D85 registra banco escolhível como aberto |

Três migrations novas, todas com `Down` reversível: `Remove_Stage_WipLimit`, `Remove_Sla`,
`Remove_WorkItemBoardPlacement`.

---

## 3. Estado da validação

**Tudo que foi entregue está validado ponta a ponta.** O risco herdado que existia — SLA e
placement sem E2E — foi resolvido: a suíte completa rodou contra banco recriado do zero e
fechou **65 passed / 4 skipped / 0 failed**.

Para levantar o ambiente:

```
# recriar o banco do zero (o seed exige instalação vazia)
docker exec -i prisma-workspace-e2e-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
  -P "<senha em .env.e2e.connection>" -C -b < scripts/e2e-create-database.sql
.\scripts\run-api-e2e.ps1                     # API em 127.0.0.1:5400, semeia ao subir
npm run dev -- --port 5450 --host 127.0.0.1   # front em modo e2e
npx playwright test                            # suíte completa
```

**Padrão a conhecer:** desde que o estado da sprint virou derivado das datas (D84), todo
teste com data fixa apodrece sozinho. Três fixtures já foram corrigidos para datas
relativas. Se um cenário de sprint começar a falhar sem mudança de código, é a primeira
hipótese.

**Memória é o gargalo real da máquina.** Com menos de 1 GB livre, API, Vite e Playwright
não convivem. Parar os containers do Supabase (projeto yumply) e o `nodecast-tv` libera o
suficiente.

## 4. O que falta, em ordem

### 4.1 Concluído nesta sessão

- **Onda 0**: `WorkflowMoveGuard` (status desativado no sync de template) e responsivo do Kanban.
- **Onda 1**: wiki/solicitações (B) e acessibilidade/resíduos (C).
- **Sprints v3**: os nove gaps da `SPEC-S-003 v3`, incluindo o defeito relatado pelo PO —
  sprint de janeiro aceitava tarefas em setembro porque o estado era coluna persistida.
- **Meu trabalho**: projetos vigentes, sprints em curso com progresso geral e pessoal
  separados, horas da semana.
- **Tipos de item**: `features/workItems/workItemKinds.ts` como fonte única de rótulo, cor,
  descrição e hierarquia. Antes eram dez tipos com três cores e nenhuma explicação.
- **Arquivadas**: filtro no backlog, selo na linha, `includeArchived` no endpoint. Antes
  arquivar era caminho sem volta.
- **Seed**: conjunto que mostra o produto — hierarquia completa, os dez tipos, três sprints
  em três estados, uma arquivada e apontamentos na semana.

### 4.2 Terminar a D83 — mover `Stage` para o projeto

Metade feita: `WorkItemBoardPlacement` foi eliminado e a tarefa já tem etapa e posição
únicas. Falta mover `Stage.BoardId` para `Stage.ProjectId`.

**Análise já feita (2026-09-09), para não ser refeita.** A implementação foi iniciada e
descartada de propósito: é um refactor que não se entrega pela metade, e uma branch parada
no meio deixaria código quebrado sem sinalizar o que era intencional. O que ficou apurado:

**O backfill é trivial, ao contrário do que a spec temia.** Consultado no banco real:

```sql
SELECT p.Name, COUNT(DISTINCT b.Id) AS Quadros, COUNT(s.Id) AS Colunas
FROM Projects p LEFT JOIN Boards b ON b.ProjectId=p.Id LEFT JOIN Stages s ON s.BoardId=b.Id
GROUP BY p.Name;
-- todo projeto tem exatamente 1 quadro

SELECT COUNT(*) FROM Boards WHERE ProjectId IS NULL;              -- 0
SELECT COUNT(*) FROM Stages s JOIN Boards b ON b.Id=s.BoardId
WHERE b.ProjectId IS NULL;                                        -- 0
```

Nenhum projeto tem dois quadros e nenhum quadro é órfão, então **não existe consolidação de
colunas equivalentes** a fazer. O backfill é uma cópia direta:
`UPDATE Stages SET ProjectId = (SELECT ProjectId FROM Boards WHERE Id = Stages.BoardId)`.

**Alcance medido:**

| Camada | Volume |
|---|---|
| Erros de compilação após mudar a entidade | 19, em 8 arquivos |
| Cadeias de `Include` no EF a revisar | 9 |
| Rotas de API afetadas | 3 (`/api/Stages/board/{boardId}` vira project-scoped) |
| Frontend | `Kanban.tsx`, 2400 linhas |
| Migration | 1, com backfill |

**Os 19 sites, por padrão de erro:**

- `Stage.BoardId` → `Stage.ProjectId`: `CreateBoardCommandHandler:64`,
  `CreateStageCommandHandler:36,94`, `GetStagesByBoardIdQueryHandler:35`,
  `ProductivityFeature:304,319,508`, `MoveWorkItemCommandHandler:74`.
- `Board.Stages` → stages do projeto: `ExternalFormsFeature:213,339,340,342`,
  `ExternalPortalFeature:427,429`, `ExternalRequestTriageFeature:201,202`,
  `WorkflowFeature:54`.
- `Stage.Board` → `Stage.Project`: `WorkflowFeature:230,258`.

**Decisões semânticas que o refactor exige, e que já foram identificadas:**

1. `CreateBoardCommandHandler` cria hoje uma etapa "Backlog" junto com o quadro. Com o fluxo
   no projeto, criar quadro deixa de criar coluna — quem passa a garantir o fluxo é a
   criação do projeto.
2. A validação "a etapa pertence ao quadro da tarefa" vira "pertence ao projeto da tarefa",
   em `CreateWorkItemCommandHandler`, `MoveWorkItemCommandHandler` e
   `WorkItemManagementFeature`.
3. `portal.Board.Stages` vira `portal.Project.Stages`, o que muda o que cada consulta
   precisa incluir — daí as 9 cadeias de `Include`.

**Ordem sugerida:** entidade e configuration → migration com backfill → os 19 sites →
`Include` chains → rotas de API → `Kanban.tsx` → E2E. Não faça sem banco de pé: o
compilador não valida movimentação de dados.

### 4.3 Defeitos confirmados e ainda abertos

- **Solicitações** — investigado por duas frentes independentes, ambas chegando à mesma conclusão: a tela é
  **fila de leitura, não defeito**. O fluxo público→fila→resposta funciona e tem cobertura E2E. Se o relato
  persistir, é preciso saber o que a pessoa clicou.
- ~~Imagem na wiki~~ — **resolvido** pelo agente B: faltava `allowBase64` na extensão Image do TipTap.
- **Excluir tarefa não existe.** `DELETE /api/WorkItems/{id}` **arquiva**, não exclui. E arquivar some de todas
  as listagens, sem tela para achar e restaurar — caminho sem volta. `specs/backlog.md` prevê lixeira de sete
  dias com exclusão hierárquica e restauração; é o que falta construir.
- ~~Responsivo da barra de ações~~ e ~~Equipes no mobile~~ e ~~acessibilidade de selects~~ — **resolvidos** nas
  Ondas 0 e 1.
- (histórico) o defeito era: no mobile o quadro estourava (803 px em viewport de 393) e os botões de
  visão passam por cima do "Nova Coluna", que fica inalcançável. O Sergio relatou o mesmo na resolução dele:
  *"atropelou na minha resolução o botão"*.
- **Equipes estoura no mobile** (509 px em 393).
- **Acessibilidade** — 10 botões só de ícone sem nome no quadro, 7 selects sem rótulo em Empresa, mais
  ocorrências em wiki e configurações.
- **SignalR** — a varredura acusou falha de negociação no quadro, mas a mensagem é típica de o cliente abortar
  ao desmontar, que é o que o próprio teste fazia. **Confirmar antes de tratar como bug.**
- **Resíduos institucionais** — assunto do e-mail de confirmação ainda diz "Detran Kanban"
  (`AuthController.cs:53` e `:161`) e o cookie de refresh se chama `detran_refresh` / `__Host-detran_refresh`
  (`AuthController.cs:204`).
- **Código morto** — `legacyTaskModalEnabled = false` no `Kanban.tsx` com centenas de linhas embaixo, e
  `pages/Dashboards.tsx` não é importado em lugar nenhum.

### 4.4 Pedidos dos devs ainda não implementados
Do PDF de análise e da conversa com o Sergio, já filtrados contra as decisões do PO:

- Gantt **volta** (o PO decidiu manter o pedido do Sergio, contrariando a spec que mandava ocultar).
- Menu lateral recolhível estilo ClickUp — **o comportamento**, não a identidade visual, que a
  `prisma-visual-system` protege.
- Backlog mais amplo, inspirado no Azure DevOps, com hierarquia visível.
- Cards mais compactos em projetos, quadros e sprints.
- Relatórios organizados por abas.
- Configurações do projeto com menu lateral por categoria.
- Remoção do ícone de responsável ausente no topo da tarefa.

### 4.5 Decisões abertas para o PO

- **`G-SPEC` da `specs/my-work-hub.md`.**
- **D85 — banco escolhível.** Ver seção 5.
- Licença do projeto, que bloqueia publicação e a `v0.1.0`.
- Mapeamento dos 10 perfis atuais para os 5 aprovados na `user-access-permissions`.
- Destino de `PermissionScope.Board`, que perdeu propósito com a D83.

---

## 5. Banco de dados escolhível (D85)

Pedido do PO: para open source, a pessoa escolher o banco, com um gratuito por padrão. **É viável**, e é o
padrão do EF Core — um provider por configuração e **um assembly de migrations por provider**. Recomendação:
**PostgreSQL como padrão gratuito** e SQL Server como opção; SQLite só se aceitarem as limitações de
concorrência.

Acoplamentos concretos a SQL Server já mapeados neste código, todos com solução conhecida:

| Onde | O quê | Saída |
|---|---|---|
| `WorkItemManagementRepository.AcquireOrganizationDependencyLockAsync` | `sys.sp_getapplock` como lock consultivo | `pg_advisory_xact_lock` no Postgres; abstrair atrás de uma interface |
| Configurations | `HasColumnType("nvarchar(max)")` | `text` no Postgres; mover para convenção por provider |
| `WorkItem.Number` | sequence do SQL Server | sequence do Postgres, ou `identity` |
| Vários | `datetimeoffset` | `timestamptz` |

O trabalho real não é o provider: é ter **duas cadeias de migrations** e CI rodando os testes contra os dois
bancos. Precisa de `G-SCOPE` e spec própria antes de começar.

---

## 6. Regras que o próximo agente precisa respeitar

- O PO é a autoridade máxima. **Não autoaprove Human Gate.** Quando o PO decidir por instrução direta,
  registre citando as palavras dele e a data, como está em D80 a D85.
- Mudança funcional exige história e spec `approved`. Spec `draft` bloqueia código de produto.
- Documentação em pt-BR; código e identificadores em inglês.
- Controllers sem lógica de negócio; usar MediatR. Banco sempre assíncrono.
- **Não editar migration já aplicada.** As três de hoje ainda não foram aplicadas em lugar nenhum.
- Toda tarefa concluída precisa de entrada no topo do `PROGRESS.md`.
- Teste de regressão só vale se você provar que ele **falha sem a correção**. Foi assim que os bugs do
  "concluído" e do "Novo item" foram fechados.
- Não versionar segredo. Os arquivos `.env.e2e.*` estão fora do Git e contêm a senha do banco local.
