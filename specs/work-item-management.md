# SPEC-WORK-ITEM-MANAGEMENT: Gerenciamento de Itens de Trabalho

**Status:** approved

## Objetivo

Definir criação, edição, atribuição, movimentação, conclusão, transferência e arquivamento do `WorkItem`,
alinhados ao quadro transversal e à posição operacional singular definidos na D52.

**Spec canônica:** este documento concentra o contrato vigente de tarefas. `SPEC-WORK-ITEMS` permanece apenas
como histórico `superseded` da antiga regra de Story Points condicionada à metodologia.

## Estado atual comprovado (gap)

- O produto possui CRUD rico, responsável principal, participantes, subtarefas, anexos, horas, dependências,
  comentários e histórico.
- O código implantado usa `WorkItemBoardPlacement` N:N, quadro padrão por projeto, coluna `Backlog` e
  sincronização de `WorkflowStatusId` entre quadros.
- O alvo aprovado deixou de usar projeções simultâneas. O estado atual deve ser migrado futuramente, sem
  apagar conteúdo ou histórico.

## Escopo

- Criação, consulta, edição, exclusão para lixeira, arquivamento e restauração da tarefa.
- Título, projeto, responsável principal, participantes e demais metadados existentes.
- Uma única posição operacional atual em quadro/coluna.
- Movimentação entre colunas do mesmo quadro.
- Transferência entre quadros.
- Conclusão/reabertura a partir da classificação da coluna.
- Nome da coluna atual como único status textual da tarefa.
- Regra de conclusão de tarefa pai e subtarefas.
- Autorização derivada do quadro e permissões operacionais.

## Fora do escopo

- Administração de quadros/colunas, coberta por `SPEC-BOARDS-STAGES-WIP`.
- Armazenamento detalhado de anexos, apontamentos, filtros, relatórios e automações.
- Alteração de schema ou código nesta revisão documental.

## Atores e permissões

- Usuários e equipes vinculados ao quadro veem todas as tarefas nele posicionadas.
- O acesso ao quadro concede automaticamente acesso aos projetos representados, no mesmo nível de permissão
  do quadro, sem substituir concessões independentes mais amplas.
- O responsável principal continua sendo o accountable da tarefa.
- Participantes autorizados podem editar, mover, concluir e apontar horas.
- Somente o responsável principal e administradores podem excluir, arquivar ou restaurar a tarefa.
- A API valida tenant, vínculo com o quadro, nível de permissão e operação solicitada.

## Regras

1. Na criação são obrigatórios: título, projeto, responsável principal, quadro e coluna.
2. Todo item pertence a exatamente um projeto e uma organização.
3. Toda tarefa ativa no fluxo possui exatamente uma posição operacional atual: um quadro, uma coluna e uma
   posição na coluna.
4. A mesma tarefa não aparece simultaneamente em vários quadros.
5. A coluna deve pertencer ao quadro e quadro/projeto/tarefa devem pertencer à mesma organização.
6. Mover para coluna classificada como concluída conclui automaticamente a tarefa.
7. Mover de coluna concluída para coluna aberta reabre automaticamente a tarefa.
   O status textual é sempre o nome da coluna atual; escolher status no detalhe executa um movimento para a
   coluna escolhida e não altera uma propriedade paralela.
8. O movimento respeita autorização, limite WIP e concorrência no backend.
9. Transferir para outro quadro preserva integralmente o mesmo ID, projeto, conteúdo, responsável,
   participantes, anexos, comentários, horas, dependências e histórico.
10. Na transferência, o usuário pode selecionar a coluna de destino ou escolher a opção automática, que usa a
    primeira coluna aberta do quadro de destino.
11. Se não houver coluna aberta e nenhuma coluna válida for escolhida, a transferência é recusada sem alteração
    parcial.
12. A tarefa pai não pode ser concluída enquanto houver subtarefas abertas.
13. A interface oferece `Concluir todas as subtarefas e concluir a tarefa`; a operação conclui toda a família
    prevista ou nenhuma tarefa.
14. O status da tarefa pai é independente das subtarefas, exceto pelo bloqueio de conclusão enquanto houver
    qualquer subtarefa aberta.
15. A tarefa nunca pode ficar sem responsável principal. Remover/desativar o responsável exige indicar um
    substituto válido na mesma operação.
16. `Excluir` e `Arquivar` são ações diferentes.
17. Excluir envia a tarefa para a lixeira por 7 dias; durante esse período pode ser restaurada. Após o prazo,
    a remoção definitiva segue processo automático, preservando os registros que a política de auditoria exigir.
18. Ao excluir uma tarefa pai, o diálogo pergunta se as subtarefas também serão excluídas.
19. Se o usuário excluir o pai com as subtarefas, a família vai junta para a lixeira; restaurar o pai excluído
    junto restaura a família inteira.
20. Se o usuário preservar as filhas, elas são reatribuídas à tarefa avó quando ela existir; sem avó, tornam-se
    tarefas independentes. A operação é atômica e não deixa órfãos inválidos.
21. Arquivar preserva a tarefa fora das telas normais, sem enviá-la à lixeira. Tarefas arquivadas aparecem em
    filtro específico e podem ser restauradas pelo responsável principal ou administrador.
22. Tarefas arquivadas junto com um quadro ficam invisíveis nas telas normais e somente voltam pela restauração
    administrativa do quadro, sem reativar tarefas que já estavam arquivadas antes.
23. Comentários permanecem fora do histórico automático.
24. O detalhe possui seis abas: Descrição, Comentários, Subtarefas, Anexos, Histórico e Linha do tempo; esta última
    mostra somente criação na coluna inicial e movimentações entre colunas conforme `SPEC-TASK-HISTORY`.
25. No histórico e na Linha do tempo, pessoas são exibidas pelo nome completo preservado; o e-mail fica oculto
    quando houver nome e só pode ser fallback para autoria legada sem nome, conforme D63.
26. Edições concorrentes usam `last write wins`: a última alteração aceita pelo servidor vence. Operações
    compostas continuam transacionais e falhas não podem persistir estado parcial.

## Estados

- A coluna atual é o estado operacional visível do Kanban.
- O nome da coluna atual é o único status textual exibido em detalhe, listas, filtros e relatórios.
- Cada coluna é classificada internamente como aberta ou concluída, independentemente do nome.
- `WorkflowStatusId` permanece no modelo as-built somente como compatibilidade até migração aprovada.
- Lixeira e arquivamento são estados funcionais separados de aberto/concluído.

## Persistência futura

- Substituir placements N:N por uma posição singular requer `G-MIGRATION` e backfill controlado.
- Placements múltiplos legados exigem regra explícita de escolha do quadro/coluna canônicos e relatório de
  ambiguidades antes da alteração.
- Transferência deve atualizar a posição singular e escrever histórico na mesma transação.
- Arquivamento causado por quadro precisa ser distinguível do arquivamento prévio da tarefa.
- A lixeira precisa registrar `DeletedAt`, origem e família afetada para expiração/restauração segura em 7 dias.
- Nenhuma mudança de schema está autorizada nesta revisão.

## API desejada

- Criar tarefa com título, projeto, responsável, quadro e coluna explicitamente selecionada, sem depender do
  nome `Backlog` e sem resolver destino automaticamente.
- Consultar e editar tarefa autorizada.
- Mover no mesmo quadro.
- Transferir para outro quadro com `destinationStageId` opcional; quando ausente, resolver a primeira coluna
  aberta.
- Concluir/reabrir de acordo com a classificação da coluna.
- Concluir pai e subtarefas atomicamente.
- Excluir para lixeira/restaurar família e arquivar/restaurar conforme permissão.

## Interface

- O detalhe apresenta o mesmo conteúdo independentemente do quadro de origem.
- O seletor `Status` apresenta colunas e move a tarefa; não existe seletor separado de workflow do projeto.
- A sexta aba é uma Linha do tempo textual e decrescente de movimentações, não um grafo.
- Responsável e participantes usam nome completo como rótulo principal.
- A transferência oferece `Escolher coluna` e `Usar primeira coluna aberta`.
- Falhas mantêm o formulário/contexto e restauram posição otimista.
- A conclusão de pai com subtarefas abertas apresenta a ação explícita de concluir todas.
- Exclusão do pai apresenta escolha de incluir filhas; preservá-las mostra o destino avó/independente.
- O filtro de arquivadas permite localizar e restaurar tarefas arquivadas.
- Acessibilidade por teclado é obrigatória também como alternativa ao drag-and-drop.

## Critérios de aceite

1. Título, projeto, responsável, quadro e coluna são exigidos na criação.
2. A tarefa aparece uma única vez, no quadro e coluna atuais.
3. Usuários/equipes vinculados ao quadro veem todas as suas tarefas e herdam o mesmo nível nos projetos
   representados.
4. Participante autorizado edita, move, conclui e aponta horas.
5. Somente responsável principal ou administrador exclui, arquiva ou restaura a tarefa.
6. Entrada em coluna concluída conclui; saída para aberta reabre automaticamente.
   Em qualquer tela, o status textual permanece exatamente igual ao nome da coluna atual.
7. Transferência preserva integralmente dados e histórico.
8. Transferência usa a coluna escolhida ou a primeira aberta e falha atomicamente quando não há destino válido.
9. Pai não conclui com subtarefas abertas; a ação conjunta conclui todas atomicamente.
10. Tarefa nunca fica sem responsável; a troca exige substituto.
11. Exclusão mantém a tarefa na lixeira por 7 dias e restaura corretamente folha ou família.
12. Preservar filhas ao excluir pai as reatribui à avó ou as torna independentes.
13. Arquivamento é separado da lixeira, aparece em filtro e permite restauração.
14. Conflitos de edição seguem `last write wins` de forma observável e auditável.
15. Acesso horizontal por IDs de outro tenant/quadro é recusado.

## Testes futuros

- .NET: campos obrigatórios, posição única, tenant, vínculo ao quadro e acesso derivado ao projeto.
- .NET: movimento, conclusão/reabertura, WIP e concorrência.
- .NET: transferência escolhida/automática, preservação de conteúdo/histórico e rollback.
- .NET: conclusão atômica de pai/subtarefas e permissões de arquivamento.
- .NET: responsável substituto, lixeira/expiração de 7 dias, restauração de família, reparenting e arquivamento.
- .NET: concorrência `last write wins` e auditoria da última escrita.
- React/Playwright: criação, movimento, transferência e estados de erro em desktop/mobile.

## Gaps conhecidos

- Modelo N:N implantado diverge da posição singular.
- `WorkflowStatusId` por projeto e sincronização de projeções divergem da semântica aberta/concluída.
- DTOs e detalhe ainda podem priorizar `WorkflowStatusName` ou `Concluído` em vez do nome real da coluna,
  contrariando a fonte única definida na D65.
- Criação atual depende de quadro padrão e coluna `Backlog`.
- Autorização atual precisa ser auditada para vínculo de usuário/equipe ao quadro e acesso derivado aos projetos.

## Human Gates

- `G-SPEC`: aprovado pelo PO.
- `G-SCOPE`: D52.
- `G-WORKFLOW`: necessário para conclusão/reabertura e transferência.
- `G-MIGRATION`: necessário para posição singular e dados de arquivamento/restauração.
- `G-HISTORY`: apenas se a estrutura imutável precisar mudar.

## Referências

- D21, D36, D47, D49, D52, D53, D54, D57, D61, D62, D63 (nome completo) e D65.
- `specs/boards-stages-wip.md`.
- `specs/multi-board-views.md` (histórico superseded).
- `specs/work-items.md` (histórico superseded).
- `specs/quick-create-work-item.md`.
