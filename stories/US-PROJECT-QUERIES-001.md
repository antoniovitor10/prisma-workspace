# US-PROJECT-QUERIES-001: Visões segmentadas e consultas de itens por projeto

**Status:** active
**Módulo:** consultas-de-projeto
**Spec vinculada:** `SPEC-PROJECT-WORK-ITEM-QUERIES`

## História

**Como** pessoa que acompanha a execução de um projeto
**Quero** visualizar separadamente épicos, bugs, features, product backlog items e tarefas e montar consultas sobre esses itens
**Para** analisar cada tipo com suas características sem perder o contexto do projeto

## Cenários esperados

- **Dado** um projeto com diferentes tipos de item de trabalho
  **Quando** acesso a visão segmentada
  **Então** consigo alternar entre todos, épicos, bugs, features, product backlog items e tarefas, com contagens e propriedades pertinentes

- **Dado** que combinei critérios autorizados dentro de um projeto
  **Quando** executo uma consulta
  **Então** vejo somente itens acessíveis que atendem aos critérios e posso reutilizar a definição conforme a política de filtros pessoais

- **Dado** uma viewport desktop ou mobile
  **Quando** altero segmento, filtro, agrupamento ou ordenação
  **Então** a interface preserva o contexto do projeto e oferece uma experiência compacta e previsível

## Observações humanas

O PO adicionou este escopo em 2026-09-03, citando explicitamente os tipos `epic`, `bug`, `feature`,
`product backlog item` e `task`, além de possíveis gerações de queries por projeto. Também pediu que a usabilidade
e os elementos de interface do ClickUp sejam considerados como referência, sem cópia visual.

O significado final de “query” ainda exige decisão humana: pode ser uma composição visual de filtros/visões salvas
ou uma linguagem textual/exportável. A spec vinculada mantém essa decisão aberta e não autoriza implementação até o
novo `G-SPEC`.
