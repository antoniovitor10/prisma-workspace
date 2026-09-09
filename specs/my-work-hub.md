# SPEC-MY-WORK-HUB: "Meu trabalho" como painel completo da pessoa

**Status:** draft

**Sucede parcialmente:** `SPEC-AUTHENTICATED-HOME` (`specs/authenticated-home.md`) quanto ao conteúdo da
página inicial autenticada. A D23 permanece válida: "Meu trabalho" é **projeção de leitura** sobre o mesmo
`WorkItem`, nunca uma fila própria com cópias.

**Origem:** pedido do PO em 2026-09-09 — *"uma ideia de 'meu trabalho' mais completa, com a visão dos projetos
que estão vigentes, com a visão das sprints, queria algo mais completão mesmo"*.

**Natureza:** contrato proposto. Exige `G-SPEC` antes da implementação.

---

## Objetivo

Transformar "Meu trabalho" na primeira parada do dia: a pessoa abre e entende, sem navegar, o que é dela, o
que está atrasado, em que projetos ela está e como vão as sprints em curso. Hoje a tela entrega apenas a lista
de tarefas atribuídas e alguns alertas.

## Princípios

1. **Projeção, nunca cópia.** Todo dado vem do `WorkItem`, `Project` e `Sprint` reais. Nada é duplicado.
2. **Só o que é da pessoa.** Cada bloco é filtrado pelo vínculo dela: responsável, participante, seguidor,
   autor, membro do projeto ou da equipe. Nunca mostra trabalho a que ela não teria acesso.
3. **Multi-tenant respeitado.** Tudo é escopado à organização ativa. Um bloco vazio nunca vaza a existência de
   trabalho de outro tenant.
4. **Ação direta.** Todo item leva à tarefa, ao projeto ou à sprint em um clique.
5. **Vazio honesto.** Cada bloco tem estado vazio que explica o porquê, sem gráfico falso nem número zero
   disfarçado de indicador.

## Blocos

### 1. Resumo do dia

Faixa de indicadores no topo, cada um clicável e levando à lista filtrada correspondente:

- **Atrasadas** — tarefas da pessoa com prazo vencido e não concluídas. Destaque de atenção.
- **Vencem hoje** — prazo igual à data de hoje no fuso da organização.
- **Em andamento** — tarefas dela em coluna de categoria `InProgress`.
- **Bloqueadas** — tarefas dela com dependência aberta, quando o módulo de dependências estiver visível.
- **Concluídas na semana** — encerra o resumo com o que saiu, não só com o que falta.

### 2. Minhas tarefas

Lista principal, agrupável por **prazo**, **projeto** ou **sprint**, com a escolha lembrada por pessoa.

- Cada linha mostra número, título, projeto, coluna atual, prioridade e prazo.
- Ordenação padrão: atrasadas primeiro, depois por prazo crescente, depois por prioridade.
- Filtro rápido por projeto e por sprint.
- A tarefa abre na gaveta de detalhe, sem sair da página.

### 3. Projetos vigentes

Cartões dos projetos **ativos** em que a pessoa participa. Um projeto é vigente quando está `Ativo` e não
arquivado.

Cada cartão traz:

- nome e sigla do projeto;
- quantas tarefas dela estão abertas ali, e quantas estão atrasadas;
- a **sprint em curso**, quando houver, com nome e quantos dias faltam para o encerramento;
- progresso do projeto, em tarefas concluídas sobre o total, no recorte da pessoa;
- atalho para o backlog e para o quadro.

Ordenação: projetos com tarefas atrasadas primeiro, depois pelos de maior volume aberto dela.

### 4. Sprints em curso

Bloco dedicado, porque é a pergunta mais frequente do dia a dia.

- Lista as sprints **ativas** dos projetos da pessoa, com estado derivado das datas conforme a `SPEC-S-003 v3`.
- Cada linha mostra nome da sprint, projeto, período, dias restantes e progresso.
- O progresso é apresentado **duas vezes**: o da sprint inteira e o **recorte da pessoa**, para separar "a
  sprint está bem" de "eu estou em dia".
- Sprints que encerram nos próximos dois dias recebem destaque de atenção.
- Sprints encerradas não aparecem aqui; ficam no histórico do projeto.

### 5. Precisa de você

Fila do que está parado esperando uma ação dela, e não apenas atribuído:

- menções em comentários ainda não lidas;
- aprovações pendentes;
- solicitações externas atribuídas a ela sem primeira resposta;
- tarefas em que ela é responsável e que estão numa coluna de revisão.

### 6. Minhas horas na semana

Resumo compacto de apontamento: total da semana, comparação com o previsto das tarefas dela e os dias sem
apontamento nenhum. Atalho para a tela de tempo. Nunca exibe custo, valor-hora ou qualquer métrica financeira.

## Critérios de aceite

- **Dado** que a pessoa não tem nenhuma tarefa, **então** cada bloco mostra seu estado vazio explicando o
  critério, e nenhum indicador aparece como zero sem contexto.
- **Dado** um projeto arquivado, **então** ele não aparece em "Projetos vigentes", mesmo com tarefas dela.
- **Dado** uma sprint cuja data final já passou, **então** ela não aparece em "Sprints em curso".
- **Dado** que a pessoa troca de organização, **então** todos os blocos recarregam no novo tenant e nenhum
  dado do anterior permanece em tela.
- **Dado** um indicador do resumo, **quando** clicado, **então** a lista abre já filtrada por aquele critério.
- **Dado** que a pessoa participa de um projeto sem ser responsável por nenhuma tarefa, **então** o projeto
  aparece com contagem zero, e não some da lista.
- **Dado** um agrupamento escolhido em "Minhas tarefas", **então** ele é lembrado na próxima visita.

## Contratos de API

Um único endpoint de leitura, para evitar seis chamadas em paralelo no carregamento:

- `GET /api/me/work-hub` retorna `summary`, `tasks`, `projects`, `sprints`, `needsYou` e `hours`.
- Aceita `groupBy` e filtros opcionais de projeto e sprint.
- Escopado pela organização do header `X-Organization-Id` e pelo usuário do token.
- Cada bloco é independente: falha em um não derruba os demais; o bloco falho retorna vazio com indicação de
  erro para a interface mostrar o estado adequado.

## Impacto

- **Sem alteração de schema.** Tudo é projeção sobre entidades existentes.
- Depende da `SPEC-S-003 v3` para o estado da sprint calculado por datas.
- O bloco de bloqueadas depende de as dependências estarem visíveis; enquanto ocultas por `specs/dependencies.md`,
  o indicador não é exibido.

## Test Gate Mapping

- **.NET:** filtro por vínculo da pessoa; isolamento multi-tenant; projeto arquivado fora da lista; sprint
  encerrada fora do bloco; progresso geral versus recorte pessoal; independência entre blocos na falha.
- **React/Vitest:** estados vazios de cada bloco; agrupamento lembrado; indicador que filtra a lista.
- **Playwright:** abrir "Meu trabalho" com dados de seed; clicar num indicador e conferir o filtro; trocar de
  organização e conferir a recarga; desktop e mobile.

## Human Gates

- `G-SPEC` — **pendente**.
- `G-MIGRATION` — não se aplica; nenhuma mudança de schema.

## Rastreabilidade

`D23` → pedido do PO em 2026-09-09 → `SPEC-MY-WORK-HUB` → lote `community-modules-v2` → testes .NET, React e
Playwright → homologação manual.
