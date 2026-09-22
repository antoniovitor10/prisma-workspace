# SPEC-TASK-FLOW-UX-001: Criação e detalhe completo da tarefa

**Status:** approved

**História:** stories/task-flow-ux.md.

**Aprovação humana:** o PO autorizou diretamente nesta conversa, em 2026-09-22, implementar este recorte coerente sem migration, incluindo responsáveis na criação, sprint no detalhe e abertura completa de subtarefas.

## Escopo aprovado

1. A criação de tarefa expõe responsável principal e múltiplos responsáveis adicionais elegíveis no projeto.
2. O detalhe da tarefa seleciona ou altera a sprint pelo fluxo de planejamento já existente.
3. Uma subtarefa aberta a partir do detalhe usa o mesmo modal completo de tarefa.

## Contrato

1. A lista de responsáveis da criação é filtrada pelos elegíveis do projeto. A API continua sendo a autoridade e revalida todos os IDs conforme D90; atribuição não concede acesso.
2. Responsável principal e adicionais são enviados no mesmo comando de criação. O responsável principal não é duplicado entre os adicionais.
3. A escolha de sprint no detalhe usa o contrato vigente de SPEC-S-003: a sprint pertence ao mesmo projeto, sprint encerrada não aceita alteração e Product Backlog remove somente o vínculo de sprint.
4. A subtarefa permanece uma WorkItem com ParentId, conforme D4. Ao abri-la, o detalhe completo disponibiliza as mesmas abas e recursos já suportados para qualquer tarefa, inclusive descrição, anexos e horas.
5. O recorte não cria tabela, entidade, migration ou regra de autorização paralela.

## Critérios de aceite

- Na criação, a pessoa escolhe o responsável principal e zero ou mais adicionais dentre os elegíveis do projeto.
- IDs sem acesso, inativos ou de outro tenant continuam recusados pela API conforme D90.
- No detalhe, a pessoa autorizada vê Product Backlog e as sprints disponíveis do projeto; ao alterar, o endpoint de planejamento aplica suas validações existentes.
- Na aba Subtarefas, criar ou selecionar uma subtarefa leva ao detalhe completo daquela mesma WorkItem.
- Descrição, anexos e apontamento de horas estão disponíveis no detalhe da subtarefa sem duplicar dados ou entidade.

## Testes

- Vitest cobre a alteração de sprint pelo endpoint de planejamento e a abertura da subtarefa pelo detalhe completo.
- Playwright cobre a presença do seletor de sprint e a criação/abertura da subtarefa com acesso às superfícies de anexos e horas.
