---
titulo: Quadro Kanban
resumo: Quadros, colunas, criar e mover cards, filtros, ações em massa, visões e Meu trabalho.
ordem: 1
atualizado_em: 2026-11-27
revisar_ate: 2027-05-27
paginas:
  - /projects/:projectId/boards#quadros
  - /boards/:boardId#quadros
  - /me/tasks#meu-trabalho
relacionados:
  - quadro-backlog-e-sprints/backlog
  - quadro-backlog-e-sprints/sprints
---

## Abas do projeto {#abas-do-projeto}

Cada projeto tem as abas **Itens**, **Backlog**, **Sprints**, **Kanban**, **Relatórios**, **Wiki** e **Configurações**. Abrir um projeto leva ao Backlog. No Backlog e no Kanban, o alternador **Backlog | Quadro** da barra de contexto troca entre os dois.

## Quadros {#quadros}

A aba **Kanban** abre o quadro do projeto. Use **Selecionar quadro** quando o projeto tiver mais de um.

- **Novo Quadro** abre **Criar Novo Quadro**: informe o **Nome do Quadro**, a **Equipe do quadro** (opcional) e a **Estrutura de colunas** (estrutura básica ou cópia das colunas de outro quadro). Exige ser administrador do projeto.
- **Excluir Quadro** só aparece quando o projeto tem mais de um quadro. Antes de excluir, escolha em **Mover tarefas para:** o quadro e a coluna que recebem as tarefas. Esta ação não pode ser desfeita.

### Visões {#visoes}

O mesmo quadro pode ser visto como **Kanban**, **Lista**, **Calendário**, **Gantt** ou **Dashboard**.

## Colunas {#colunas}

- **Nova Coluna** cria uma coluna. Toda coluna nova nasce classificada como "Em andamento".
- O lápis ou a lixeira da coluna abrem **Editar Coluna**: nome, **Classificação da coluna** (Backlog, Pronta, Em andamento, Revisão ou Concluída) e exclusão.
- Para excluir uma coluna com tarefas, escolha em **Ao excluir, transferir tarefas para** a coluna que recebe as tarefas.
- Para reordenar, arraste o título da coluna ou use as setas de mover para a esquerda e para a direita.
- Cada coluna pode ordenar os próprios cards: ordem manual, prioridade, prazo, título ou mais recentes.

Não há limite de cards por coluna (WIP).

### Concluir uma tarefa {#concluir-uma-tarefa}

Uma tarefa está concluída quando está numa coluna classificada como **Concluída**. Mudar a classificação de uma coluna conclui ou reabre as tarefas que estão nela; quando há tarefas filhas abertas, o Prisma pede a confirmação "Aceito concluir também todos os descendentes abertos".

## Criar um card {#criar-um-card}

1. Clique em **Novo Card** no pé da coluna.
2. Preencha **Título do Card** e, se quiser, subtítulo e descrição em Markdown.
3. Escolha o **Tipo do Card**, a prioridade (Baixa, Média ou Alta), as horas estimadas e o **Responsável principal** (obrigatório). Responsáveis adicionais são opcionais.
4. Clique em **Criar Card**. O card novo entra no topo da coluna.

Também dá para criar de qualquer tela pelo botão **Novo item** da barra superior (veja [Backlog](/ajuda/quadro-backlog-e-sprints/backlog#criar-itens)).

## Mover cards {#mover-cards}

- Arraste o card com o mouse ou com o toque até outra coluna; ele entra no fim da coluna de destino.
- Pelo teclado, use os botões **Mover tarefa para a coluna anterior** e **Mover tarefa para a próxima coluna** do card.
- Clique no card para abrir o detalhe: descrição, responsáveis, checklist, comentários, anexos e apontamento de horas.

> Nos projetos com Integrações, quem autoriza uma task é sempre uma pessoa, movendo o card para "Em desenvolvimento" no quadro. Veja [Integrações](/ajuda/integracoes/integracoes#quem-move-o-card).

## Filtros {#filtros}

- **Buscar nos cartões** procura pelo texto do card.
- **Filtros** abre o painel com pessoas e equipes, classificação, prazo e origem, outros critérios e agrupamento. O contador mostra quantos cards aparecem do total.
- **Ordenar cartões** muda a ordem de todas as colunas.
- **Salvar filtro atual** guarda a combinação com um nome; **Filtros salvos** reaplica; **Limpar filtros** volta ao quadro completo.
- **Conteúdo do cartão** escolhe o que aparece em cada card; **Salvar visual** guarda a escolha.

## Ações em massa {#acoes-em-massa}

**Selecionar visíveis** marca até 200 cards. Com a seleção, escolha a ação (mover para etapa, atribuir ou remover pessoa, alterar prioridade, adicionar ou remover tag, definir sprint) e clique em **Aplicar**. **Limpar seleção** desfaz a seleção.

## Meu trabalho {#meu-trabalho}

**Meu trabalho**, na barra superior, junta o que pede a sua atenção em todos os projetos: atribuídas, para hoje, desta semana, atrasadas, bloqueadas e solicitações atribuídas. Na **Fila de atenção**, cada linha tem o botão **Kanban**, que abre o quadro já filtrado nas suas tarefas, e o cronômetro de horas.
