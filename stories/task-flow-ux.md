# US-TASK-FLOW-UX-001: Criação e detalhe completo da tarefa

**Status:** approved

**Aprovação humana:** o PO autorizou diretamente nesta conversa, em 2026-09-22, resolver este recorte sem migration.

## História

Como pessoa autorizada, quero definir responsável principal e responsáveis adicionais já ao criar a tarefa, escolher ou alterar a sprint no detalhe e abrir uma subtarefa no mesmo detalhe completo, para organizar o trabalho sem perder acesso, contexto ou recursos da tarefa.

## Critérios de aceite

- A criação apresenta somente pessoas elegíveis no projeto para responsável principal e adicionais; a API revalida conforme D90.
- O detalhe permite escolher uma sprint disponível do projeto ou retornar a tarefa ao Product Backlog pelos fluxos existentes.
- A subtarefa é aberta no mesmo detalhe completo da própria WorkItem, com descrição, anexos e apontamento de horas disponíveis.
- Não é criada entidade nova para subtarefa e não há migration.

