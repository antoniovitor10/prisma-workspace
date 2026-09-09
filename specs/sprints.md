# SPEC-S-003: Sprints, planejamento e quadro operacional

**Status:** approved

**Versão:** 3

**Histórico do status:** a v1 (`approved` em 2026-08-24) definia o ciclo manual com uma única sprint ativa por
projeto. Em 2026-09-08 o PO pediu Sprint ↔ Project **N:N** e a v2 foi escrita e aprovada. Em 2026-09-09 o PO
**reverteu** essa decisão: *"a sprint pode ter apenas 1 projeto, mas 1 projeto pode ter várias sprints"*. Esta
v3 mantém tudo que a v2 trouxe de correto — estado calculado por datas, fim do "Iniciar sprint", sprints
simultâneas, exclusão transacional — e **descarta a associação N:N**. Registrado em D84, que sucede D81.

**Autoridade:** decisão humana explícita do PO. Não é autoaprovação de agente.

---

## Contrato aprovado

### Relação com projeto

1. Uma `Sprint` pertence a **exatamente um** `Project`. `Sprint.ProjectId` é obrigatório.
2. Um `Project` pode ter **várias** sprints, inclusive com períodos sobrepostos.
3. **Não existe** `SprintProject`, nem `Sprint.OrganizationId` próprio: o tenant da sprint é o do seu projeto.
4. Um `WorkItem` pertence a um único `Project` e a **no máximo uma** sprint.
5. A sprint só aceita tarefas do seu próprio projeto.

### Ciclo de vida

6. Os estados funcionais são `Planejada`, `Ativa` e `Encerrada`.
7. O estado é **calculado a partir das datas** e da data atual, no fuso da organização. A API é a única fonte
   do estado funcional; a interface nunca o recalcula por conta própria.
8. **Não existe** ação manual `Iniciar sprint`.
9. Várias sprints do mesmo projeto podem estar ativas ao mesmo tempo quando os períodos se sobrepõem.
10. Uma sprint encerrada continua visível em histórico e filtros.
11. **Uma sprint encerrada não aceita novas tarefas.** O seletor de sprint no backlog oferece apenas sprints
    `Planejada` ou `Ativa`, e a API recusa o vínculo com sprint encerrada.
12. Ao encerrar com tarefas abertas, o usuário autorizado escolhe, para elas: mover para outra sprint do mesmo
    projeto, ou remover o vínculo, voltando ao Product Backlog. Enquanto a escolha não for confirmada, nenhum
    vínculo muda parcialmente.
13. Tarefas concluídas permanecem como evidência histórica da sprint encerrada.

### Criação, edição, exclusão e autorização

14. Criar exige `nome`, `data inicial` e `data final`; a final não pode ser anterior à inicial.
15. Objetivo e equipe são opcionais.
16. Criar, editar e excluir exigem permissão configurável por usuário/perfil, nunca um papel fixo como
    `ScrumMaster`. A API é a autoridade final, mesmo quando a interface oculta a ação.
17. Excluir uma sprint remove apenas o `SprintId` das tarefas. Nunca exclui, arquiva ou movimenta tarefa.
18. Quadro, coluna, posição, conteúdo, hierarquia e histórico da tarefa são preservados.
19. A desvinculação e a exclusão ocorrem na mesma transação; falha em qualquer etapa causa rollback integral.

### Backlog e Kanban

20. Product Backlog, Kanban e sprint são recortes do mesmo `WorkItem`; não existem cópias.
21. O Product Backlog mostra por padrão tarefas sem sprint e oferece filtro para incluir as já planejadas.
22. Incluir, transferir ou retirar de sprint **não altera** quadro, coluna nem posição.

### Planejamento hierárquico

23. Ao planejar uma tarefa pai, todas as descendentes ativas entram na mesma sprint.
24. Quando todas as filhas diretas ativas de um pai são selecionadas, o pai entra também, recursivamente.
25. A interface informa quantos itens foram selecionados diretamente e quantos entraram automaticamente.
26. O backend recalcula a família e persiste atomicamente; em falha, nenhum item muda de `SprintId`.
27. Sem sprint disponível, a interface oferece `Criar sprint`, preserva a seleção e conclui o planejamento sem
    exigir nova seleção.

### Capacidade

28. Não existe cálculo automático de capacidade. Valores manuais, se mantidos, são informativos e nunca
    impedem criar, ativar ou encerrar sprint.

## Critérios de aceite

- **Dado** hoje anterior à data inicial, **então** o estado é `Planejada`.
- **Dado** hoje entre as datas, inclusive, **então** o estado é `Ativa`, sem ação manual.
- **Dado** duas sprints do mesmo projeto com períodos sobrepostos, **então** ambas podem estar ativas.
- **Dado** uma sprint com data final no passado, **quando** o usuário abre o seletor no backlog, **então** ela
  não aparece; e **quando** a API recebe o vínculo mesmo assim, **então** recusa.
- **Dado** encerramento com tarefas abertas, **então** o responsável escolhe destino antes de confirmar.
- **Dado** exclusão de sprint com tarefas, **então** elas ficam sem sprint, no mesmo quadro e coluna.
- **Dado** uma tarefa de outro projeto, **quando** se tenta vinculá-la, **então** a API recusa.
- **Dado** um pai com descendentes ativos, **quando** planejado, **então** a família inteira é vinculada
  atomicamente.

## Estado atual e gaps

Já existe: entidade `Sprint` com nome, objetivo, datas, equipe, estado persistido e snapshots; validação de
nome e intervalo; listagem por projeto; criação e edição; encerramento com escolha de destino; planejamento
pelo mesmo `SprintId`; dashboard com quadro, métricas e histórico.

Gaps a resolver:

1. **Estado manual:** `Sprint.Status` é persistido e alterado por `PUT /api/sprints/{id}/status`, em vez de
   calculado pelas datas.
2. **Botão manual:** a interface ainda exibe `Iniciar sprint`.
3. **Uma ativa por projeto:** domínio, handler e repositório bloqueiam a ativação quando já existe outra ativa.
4. **Sprint encerrada aceita tarefas:** relatado pelo PO em 2026-09-09. O backlog lista todas as sprints e não
   há recusa na API.
5. **Autorização fixa:** exige no mínimo `ScrumMaster`, em vez de permissão configurável.
6. **Exclusão ausente:** não existe `DELETE /api/sprints/{id}` nem fluxo transacional de desvinculação.
7. **Capacidade exposta** no dashboard sem contrato homologado.
8. **Sem encerramento automático** por data, nem mecanismo para solicitar o destino dos itens abertos.
9. **D25 desatualizada:** a regra de uma única ativa e a transição manual conflitam com este contrato.

## Impacto de dados e workflow

- O modelo de dados **não muda**: `Sprint.ProjectId` continua obrigatório e não há tabela nova. Se a
  implementação do estado calculado exigir apenas leitura, **não há migration**.
- Se `Sprint.Status` for removido da tabela, ou se índices mudarem, aí sim exige `G-MIGRATION`.
- Estado calculado e fim da exclusividade de sprint ativa alteram regra de workflow: exige `G-WORKFLOW`.
- `SprintItemSnapshot` não muda de estrutura; `G-HISTORY` não se aplica.

## Test Gate Mapping

- **.NET:** estado por datas no fuso da organização; sprints simultâneas; recusa de vínculo em sprint
  encerrada; recusa de tarefa de outro projeto; autorização configurável; exclusão transacional preservando
  quadro, coluna e histórico; destino atômico dos itens abertos; planejamento hierárquico; concorrência.
- **React/Vitest:** ausência do botão iniciar; seletor que oculta sprints encerradas; formulários; diálogo de
  exclusão; escolha de destino; capacidade apenas informativa.
- **Playwright:** criar sprint; observar transição por data; manter duas ativas; tentar planejar em sprint
  encerrada; excluir com tarefas; encerrar para backlog e para outra sprint; filtrar backlog.

## Human Gates

- `G-SPEC` — **aprovado pelo PO em 2026-09-09** para esta v3.
- `G-WORKFLOW` — **aprovado pela mesma instrução**. Estado calculado e fim da sprint ativa única.
- `G-MIGRATION` — **exigido apenas se** a implementação alterar schema. O contrato em si não pede.
- `G-HISTORY` — não se aplica.

## Rastreabilidade

`D25` (sucedida) → `D81` (revogada) → `D84` → `SPEC-S-003 v3` → lote `sprint-planning-v2` → testes .NET, React
e Playwright → homologação manual.
