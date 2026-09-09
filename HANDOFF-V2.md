# HANDOFF — programa Prisma WorkSpace v2

Estado em 2026-09-09, ao fim da sessão do coordenador. Leia `AGENTS.md`, `DECISIONS.md`, `ROADMAP.md`, as duas
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

Quinze commits. Todos com build limpo, xUnit e Vitest verdes.

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

## 3. Risco herdado — leia antes de continuar

**Os lotes de SLA e de placement não foram validados ponta a ponta.** Estão cobertos por build, testes
unitários e typecheck, mas **não por E2E**, porque a máquina ficou sem memória (~0,5 GB livres) e o engine do
Docker parou de subir, deixando o ambiente sem banco.

O de SLA é o mais delicado: mexeu fundo no portal externo, que é **justamente o módulo que o PO relatou como
quebrado**. Primeira coisa a fazer quando houver banco:

```
# resetar o banco E2E (o seed exige instalação vazia)
docker exec -i prisma-workspace-e2e-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
  -P "<senha em .env.e2e.connection>" -C -b < scripts/e2e-create-database.sql
.\scripts\run-api-e2e.ps1                     # API em 127.0.0.1:5400
npm run dev -- --port 5450 --host 127.0.0.1   # front em modo e2e
npx playwright test                            # suite completa
```

---

## 4. O que falta, em ordem

### 4.1 Validar o que já foi feito
Rodar a suíte E2E completa contra os três lotes sem validação. Corrigir o que aparecer.

### 4.2 Terminar a D83 — mover `Stage` para o projeto
Metade feita (o placement saiu). Falta mover `Stage.BoardId` → `Stage.ProjectId`, tornar `Board.ProjectId`
obrigatório e passar equipe e permissão do quadro para o projeto. **É a única parte com migração de dados**:
as colunas de quadros diferentes do mesmo projeto precisam ser consolidadas, fundindo equivalentes pelo
`WorkflowStatusId`. Não faça sem banco. Contrato completo em `specs/board-as-view.md`.

### 4.3 Sprints — `SPEC-S-003 v3`
Nove gaps listados na spec. Os mais visíveis para o PO: estado calculado pelas datas, fim do botão "Iniciar
sprint", várias sprints ativas, e **sprint encerrada recusando novas tarefas** (relatado por ele). Não exige
migration, salvo se remover `Sprint.Status` da tabela.

### 4.4 "Meu trabalho" completo — `specs/my-work-hub.md`
Spec nova, em `draft`, aguardando `G-SPEC`. Seis blocos: resumo do dia, minhas tarefas agrupáveis, projetos
vigentes, sprints em curso com progresso geral e pessoal, "precisa de você" e horas da semana. Sem mudança de
schema.

### 4.5 Defeitos confirmados e ainda abertos

- **Solicitações não funciona** — relatado pelo PO, não reproduzido. A tela abre limpa, sem erro de console
  nem de rede, mas é fila só de leitura. Faltam os passos exatos.
- **Imagem na wiki não insere** — a API foi testada e salva `<img>` com data URI de 1,4 MB corretamente. O
  defeito está no editor TipTap, não isolado.
- **Excluir tarefa não existe.** `DELETE /api/WorkItems/{id}` **arquiva**, não exclui. E arquivar some de todas
  as listagens, sem tela para achar e restaurar — caminho sem volta. `specs/backlog.md` prevê lixeira de sete
  dias com exclusão hierárquica e restauração; é o que falta construir.
- **Responsivo da barra de ações** — no mobile o quadro estoura (803 px em viewport de 393) e os botões de
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

### 4.6 Pedidos dos devs ainda não implementados
Do PDF de análise e da conversa com o Sergio, já filtrados contra as decisões do PO:

- Gantt **volta** (o PO decidiu manter o pedido do Sergio, contrariando a spec que mandava ocultar).
- Menu lateral recolhível estilo ClickUp — **o comportamento**, não a identidade visual, que a
  `prisma-visual-system` protege.
- Backlog mais amplo, inspirado no Azure DevOps, com hierarquia visível.
- Cards mais compactos em projetos, quadros e sprints.
- Relatórios organizados por abas.
- Configurações do projeto com menu lateral por categoria.
- Remoção do ícone de responsável ausente no topo da tarefa.

### 4.7 Decisões abertas para o PO

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
