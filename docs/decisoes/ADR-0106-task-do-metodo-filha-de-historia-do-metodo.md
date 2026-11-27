---
id: ADR-0106
titulo: "Task do método pode ser filha de uma História do método no mesmo quadro; religar à História não é troca de identidade"
status: aceita
data: 2026-11-27
origem: "HU-0008, PRD D7 (aprovado pelo desenvolvedor em 2026-11-12), T-0021"
substitui: ""
---

# ADR-0106 · Task do método pode ser filha de uma História do método no mesmo quadro; religar à História não é troca de identidade

Citada no PRD da HU-0008 como "ADR-0004 do espaço de trabalho"; neste repositório ela recebe o número seguinte da série (`ADR-0106`).

## Contexto

O Prisma é o rastreador do método. A HU-0008 pede que cada HU vire uma História no Prisma e que as tasks do método fiquem como Tarefas filhas dela (CA-4, CA-5, CA-10), sem perder status, histórico, autorização nem vínculos de Git.

Até aqui o método inteiro supunha task de topo: a consulta das tasks filtrava `ParentId == null`, o snapshot da task tinha `IsTopLevel` e quem o usava recusava task com pai (`MethodTransitionPolicy`, `GitTaskResolver`, `GitDeliveryProcessor`, `GitPullRequestOperations`, os vínculos de branch e PR e o agendamento de PRs legados). O `MethodWriteGuard` recusava o agente criar item com pai e tratava qualquer mudança de `ParentId` como troca de identidade; o `MethodMovementRecorder` então revogava a autorização, trocava o ciclo e tirava a task do controle do método. Pôr uma task embaixo de uma História a mataria como task do método. É área sensível (G-WORKFLOW e G-HISTORY).

## Decisão

1. **Task do método elegível** = item com ID `T-…` válido, no quadro do método, não arquivado, e com `ParentId` nulo **ou** pai do tipo História (`UserStory`) que tem documento `Hu` do método (tabela `MethodDocuments`), no mesmo quadro, não arquivado. A regra fica num ponto só (`MethodEligibility`, na infraestrutura) e o snapshot passa de `IsTopLevel` a `IsMethodEligible`, usado por todos os pontos acima.
2. **Religar** a task (de nulo para uma História do método, ou de uma História do método para outra, no mesmo quadro e sem mudar o arquivamento) **não** é troca de identidade: não revoga autorização, não troca ciclo, não mexe em bloqueio nem em provas. Qualquer outra mudança de `ParentId` (para um Épico, uma Tarefa, uma História que não é do método ou de outro quadro) continua sendo troca de identidade, como antes.
3. **O agente cria** Épico e História do método sem coluna (`StageId` nulo), fora da matriz de transições (não são tasks), e Tarefa sob História do método. Continua recusado: o agente criar item com pai fora disso, o sistema criar ou reorganizar itens de planejamento e qualquer item em coluna de `em-desenvolvimento`.
4. A task filha continua sendo task do método para todos os fins: status, matriz, autorização, bloqueio, webhook do GitHub, vínculos de branch e PR e `proximas_tasks`.

## Alternativas descartadas

- **Manter a task no topo e mostrar a HU só pelo campo "HU" e pela etiqueta (como antes):** não atende CA-4 nem CA-10 (a hierarquia Épico → História → Tarefa não aparece no Prisma).
- **Aceitar qualquer pai:** abriria caminho para tirar a task do controle do método ou mudar seu escopo sem passar pela regra; a elegibilidade exige pai História **do método** no mesmo quadro.

## Consequências

### Positivas

- O backlog mostra a HU como História com as tasks embaixo, e o quadro mostra a Tarefa filha como card (T-0022).
- A religação das tasks antigas (T-0025) preserva status, autorização, ciclo, bloqueio, comentários e vínculos de Git.

### Negativas / custos

- Toda consulta de elegibilidade passa a olhar o pai e a existência do documento `Hu` (uma junção a mais).
- Uma História do método arquivada torna as filhas não elegíveis até a religação a outra História.

## Como reverter

Voltar a regra de `MethodEligibility` para "só topo" e o `MethodWriteGuard` para tratar toda mudança de `ParentId` como troca de identidade. Antes, religar as tasks filhas ao topo (troca de identidade: revoga autorizações ativas) e avisar o desenvolvedor.
