---
titulo: Backlog e hierarquia de itens
resumo: Criar itens, tipos e hierarquia, filtros, priorizar por arraste e planejar a sprint pelo backlog.
ordem: 2
atualizado_em: 2026-11-27
revisar_ate: 2027-05-27
paginas:
  - /projects/:projectId/backlog#backlog
  - /projects/:projectId/items#itens-do-projeto
relacionados:
  - quadro-backlog-e-sprints/sprints
  - quadro-backlog-e-sprints/quadro-kanban
---

## Backlog {#backlog}

A aba **Backlog** do projeto abre o **Planejamento do backlog**: o **Product backlog** de um lado e, ao lado, a sprint em planejamento. **Ampliar backlog** esconde a sprint para ver só o backlog; **Ver sprint ao lado** traz de volta.

## Criar itens {#criar-itens}

- **Adição rápida no backlog**: clique em **Novo item** no topo do backlog, escolha o tipo, digite só o título no campo "Digite somente o título e pressione Enter" e tecle Enter (ou clique em **Adicionar**). O item entra no primeiro quadro do projeto, com prioridade Média. Sem nenhum quadro, o Prisma pede: "Crie um quadro antes de adicionar itens."
- **De qualquer tela**: o botão **Novo item** da barra superior mostra os tipos (Épico, Feature, História, Tarefa e Bug) e abre **Criar item rapidamente**. Informe "O que precisa ser feito?", o tipo, o projeto e o quadro, e clique em **Criar item**.

## Tipos e hierarquia {#tipos-e-hierarquia}

Os tipos que se criam diretamente são **Épico**, **Feature**, **História**, **Tarefa** e **Bug**. A hierarquia segue esta ordem:

- Épico contém Features e Histórias;
- Feature contém Histórias;
- História contém Tarefas;
- Tarefa contém Subtarefas.

Em cada linha do backlog dá para mudar tipo, título, prioridade, pontos e o épico, ver as relações, abrir os detalhes e expandir ou recolher os filhos.

## Filtros e busca {#filtros-e-busca}

A busca aceita número, título, quadro ou solicitante. Os filtros cobrem tipo, prioridade, quadro, arquivadas e relações (bloqueados, com dependências, sem épico). **Agrupar por** organiza a lista; **Limpar** volta ao normal.

Os filtros ficam no endereço da página: copie a URL para mandar a mesma visão a outra pessoa.

## Priorizar {#priorizar}

Arraste os itens dentro do Product backlog para mudar a prioridade. Com um agrupamento ativo o arraste fica desligado e o Prisma avisa: "Desative o agrupamento para priorizar por arraste."

## Planejar a sprint pelo backlog {#planejar-a-sprint}

1. Escolha a sprint em **Sprint de planejamento**.
2. Arraste itens do backlog para o painel da sprint, ou selecione vários e use **Mover para sprint**.
3. **Retornar ao backlog** tira os itens selecionados da sprint.

## Itens do projeto {#itens-do-projeto}

A aba **Itens** lista todos os itens do projeto, com filtros rápidos por tipo: Todos, Épicos, Features, Product backlog, Bugs e Tarefas.
