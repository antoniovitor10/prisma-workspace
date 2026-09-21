# SPEC-WORK-ITEM-ASSIGNEES: múltiplos responsáveis por tarefa

**Status:** approved

## Aprovação

Pedido direto do PO em 2026-09-21: corrigir o erro que impede alocar vários responsáveis e implementar a solução.

## Contrato

1. Uma tarefa aceita zero ou mais responsáveis adicionais por meio da relação existente `WorkItemAssignee`.
2. A escolha de um novo responsável não remove os demais responsáveis já alocados.
3. O campo `ResponsibleId` continua representando o responsável principal/accountable e deve ser identificado como tal na interface.
4. A área de responsáveis lista todas as pessoas alocadas, permite adicionar outra pessoa elegível e remover individualmente um responsável adicional.
5. O responsável principal não é removido pela ação individual de responsável adicional; sua troca continua no campo próprio.
6. A interface usa a palavra `responsável`, e não `participante`, para evitar que a capacidade já existente fique oculta para o gestor.
7. Adições repetidas são idempotentes e a API preserva tenant e permissão `Assign` existentes.
8. Sem alteração de schema ou migration.

## Testes

- React: dois responsáveis permanecem visíveis após adições sucessivas; o seletor não oferece pessoas já alocadas.
- API/.NET: duas atribuições ao mesmo item persistem simultaneamente e repetição não duplica.
- E2E: gestor adiciona duas pessoas à mesma tarefa e ambas continuam visíveis em desktop e mobile.
