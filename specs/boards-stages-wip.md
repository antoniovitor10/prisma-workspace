# SPEC-BOARDS-STAGES-WIP: Quadros Kanban e Colunas Operacionais

**Status:** approved

**Decisão vigente (2026-08-24):** o contrato desejado passa a seguir o conceito operacional do Runrun.it
aprovado pelo PO. O quadro é transversal aos projetos, cada tarefa ocupa um único quadro/coluna por vez e
as colunas têm nome e ordem livres, sendo classificadas internamente apenas como abertas ou concluídas.

## Objetivo

Definir o quadro como fluxo operacional Kanban da organização, sem transformá-lo em nível organizacional nem
limitá-lo a um único projeto. O Kanban é a entrada principal da aplicação autenticada e permite alternar entre
quadros e filtrar o trabalho sem uma sidebar global persistente.

## Estado atual comprovado (gap de implementação)

- O código e o banco atuais mantêm `Project -> Boards`.
- A migration `MultiBoard_WorkItemPlacements` implementou projeções N:N, quadro padrão por projeto e
  sincronização de status entre projeções.
- Quadros novos recebem hoje uma coluna `Backlog` e o produto contém regras ligadas a
  `WorkflowStatusId`, `StageCategory`, transições e quadro padrão.
- Criação, exclusão de quadro, persistência da ordem e alternativas acessíveis de mover coluna para a esquerda
  ou direita existem; drag-and-drop de colunas ainda não está disponível.
- Esse estado continua válido apenas como retrato as-built. Ele não altera o alvo desta spec e não autoriza
  migration ou código nesta revisão documental.

## Escopo

- Quadro operacional transversal, capaz de reunir tarefas de projetos diferentes da mesma organização.
- Kanban como primeira tela após autenticação.
- Seletor superior de quadros.
- Colunas com nome e ordem livres.
- Classificação interna de cada coluna como `Aberta` ou `Concluída`.
- Reclassificação segura da coluna com confirmação, conclusão em lote e preservação da hierarquia.
- Posição única da tarefa em um quadro e uma coluna por vez.
- Criação, edição, reordenação, arquivamento e restauração de quadros e colunas.
- Limite WIP opcional por coluna.
- Associação direta de usuários e equipes ao quadro.
- Permissões configuráveis por usuário/perfil para administrar quadros e colunas.
- Arquivamento do quadro junto com suas tarefas.

## Fora do escopo

- Projeção simultânea da mesma tarefa em vários quadros.
- Quadro padrão por projeto.
- Colunas obrigatórias ou nomes reservados como `Backlog`, `Em andamento` ou `Concluída`.
- Sidebar global persistente.
- Alteração de schema, migration, backfill ou código de produto nesta etapa documental.
- Conteúdo detalhado da tarefa, coberto por `SPEC-WORK-ITEM-MANAGEMENT`.

## Atores, acesso e permissões

- Somente usuários e equipes explicitamente vinculados ao quadro podem visualizá-lo.
- Quem possui acesso ao quadro vê todas as tarefas desse quadro, respeitando sempre o isolamento da organização.
- Acesso ao quadro concede automaticamente acesso aos projetos representados pelas tarefas nele contidas.
- O acesso derivado ao projeto herda o mesmo nível de permissão que a pessoa possui no quadro; não eleva nem
  reduz esse nível silenciosamente.
- O acesso derivado existe enquanto o vínculo com o quadro e a representação do projeto forem válidos. Sua
  remoção não pode apagar dados nem revogar outra concessão independente já existente.
- Criar, editar, arquivar ou excluir quadros depende da permissão configurável **Administrar quadros** atribuída
  ao usuário ou ao perfil; não depende de um papel fixo como `ProjectAdmin`.
- Criar, editar, reordenar, classificar ou remover colunas usa a mesma permissão **Administrar quadros**.
- Movimentar cartões depende da permissão operacional aplicável à tarefa/quadro.
- Restaurar um quadro arquivado e suas tarefas é operação exclusiva de administrador.
- A API é a autoridade final das permissões; esconder controles na interface não substitui autorização.

## Regras funcionais

1. Um quadro pertence à organização e pode exibir tarefas de mais de um projeto autorizado.
2. Um quadro não pertence funcionalmente a um único projeto no modelo desejado.
3. Cada tarefa ativa no fluxo possui exatamente uma posição operacional atual: um `BoardId`, um `StageId` e
   uma posição dentro da coluna.
4. A mesma tarefa não aparece simultaneamente em dois quadros.
5. Mover uma tarefa para outro quadro é uma transferência de sua posição atual, não a criação de uma projeção;
   preserva integralmente conteúdo, vínculos e histórico.
6. Na transferência, o usuário escolhe uma coluna de destino ou usa automaticamente a primeira coluna aberta.
   Sem coluna aberta e sem escolha válida, a operação é recusada integralmente.
7. Toda coluna pertence a exatamente um quadro e possui nome, posição e classificação aberta/concluída.
8. Nomes e ordem são livres. Nenhum nome ou conjunto de colunas é obrigatório.
9. Um quadro pode ter qualquer quantidade de colunas abertas ou concluídas, inclusive várias de cada tipo.
10. Mover uma tarefa para coluna concluída conclui a tarefa automaticamente.
11. Mover uma tarefa de coluna concluída para coluna aberta reabre a tarefa automaticamente.
12. As regras 10 e 11 tratam do movimento individual da tarefa. Reclassificar a própria coluna segue uma
    operação coletiva distinta, descrita nas regras seguintes.
13. Reclassificar uma coluna vazia pode ser confirmado e persistido diretamente nos dois sentidos.
14. Se a coluna possui tarefas, a interface exige confirmação tanto para aberta -> concluída quanto para
    concluída -> aberta.
15. A confirmação informa, no mínimo, nome da coluna, direção da mudança, total de tarefas nela, quantas terão
    o estado alterado e, na conclusão, quantos descendentes abertos fora da coluna também serão afetados.
16. Ao confirmar aberta -> concluída, todas as tarefas abertas posicionadas na coluna são concluídas na mesma
    transação da classificação. Tarefas já concluídas não recebem evento duplicado.
17. Se uma tarefa-pai afetada possui subtarefas abertas em qualquer outro nível ou coluna, o diálogo pergunta
    explicitamente se o usuário aceita concluir também toda a descendência recursiva.
18. Aceitar conclui todos os descendentes abertos, em todos os níveis, sem movê-los de suas colunas atuais.
19. Recusar ou fechar essa confirmação cancela integralmente a reclassificação; nem coluna nem tarefa muda.
20. Ao confirmar concluída -> aberta, todas as tarefas concluídas posicionadas na coluna são reabertas na mesma
    transação da classificação. Tarefas já abertas não recebem evento duplicado.
21. Não existe exceção operacional em que a classificação atual da coluna e o estado aberto/concluído da tarefa
    permaneçam divergentes depois de reclassificação ou movimento confirmados.
22. Reclassificação, conclusão/reabertura das tarefas e eventual conclusão recursiva de descendentes formam uma
    única transação.
23. Cada tarefa efetivamente concluída registra evento próprio de histórico com ator, data/hora e motivo ligado
    à reclassificação da coluna; a alteração da coluna também permanece auditável.
24. Se o conteúdo da coluna ou a descendência mudar entre a confirmação e a persistência, a API rejeita a
    fotografia obsoleta e a interface recarrega o impacto para nova confirmação; nenhum efeito parcial persiste.
25. Reordenar colunas não move cartões nem altera o nome ou a classificação das colunas.
26. O limite WIP, quando configurado, é inteiro positivo e validado no backend. Sem valor significa sem limite.
27. Criar um quadro não cria automaticamente uma coluna chamada `Backlog` nem qualquer template fixo.
28. Criar, editar ou arquivar um quadro e administrar suas colunas exige a permissão configurável
    **Administrar quadros**.
29. Arquivar um quadro com tarefas arquiva também todas as tarefas nele posicionadas, preservando dados,
    anexos, horas e histórico. Elas deixam de aparecer nas telas normais.
30. A ação visual `Excluir quadro` executa esse arquivamento recuperável; não realiza exclusão física imediata.
31. O diálogo de arquivamento informa nome do quadro, quantidade de tarefas afetadas e que todas ficarão
    invisíveis nas visões comuns.
32. Arquivamento é transacional: quadro, colunas e tarefas são arquivados juntos ou nenhuma alteração persiste.
33. Não há transferência obrigatória de tarefas ao arquivar o quadro.
34. Somente administrador pode restaurar o quadro; a restauração reativa, na mesma operação, as tarefas que
    foram arquivadas junto com ele.
35. Tarefas que já estavam arquivadas antes do arquivamento do quadro não podem ser reativadas indevidamente
    pela restauração; a origem do arquivamento precisa ser distinguível.
36. Movimentação, conclusão, reabertura, arquivamento e restauração devem produzir histórico auditável.
37. Operações concorrentes devem falhar ou reconciliar sem posições duplicadas, perda de tarefa ou estado
    parcialmente aplicado.

## Experiência do Kanban

- Após o login, a rota inicial abre o Kanban no último quadro autorizado usado pelo usuário.
- No primeiro acesso, ou quando o último quadro não estiver disponível, nenhum quadro é escolhido
  automaticamente: a tela exibe `Nenhum quadro selecionado`, com `Selecionar quadro` e, para quem possuir
  **Administrar quadros**, `Criar quadro`.
- O topo contém seletor de quadros pesquisável e deixa claro qual quadro está ativo.
- Alternar quadro usa navegação SPA, atualiza a URL e respeita voltar/avançar.
- Ações administrativas de quadro e coluna aparecem somente com **Administrar quadros**.
- Ao trocar a classificação de uma coluna com tarefas em qualquer sentido, o editor mostra confirmação de
  impacto antes da ação final; na conclusão com descendentes abertos, o consentimento recursivo fica explícito.
- O painel de filtros é lateral recolhível ou overlay, contextual ao Kanban; não reserva uma coluna fixa da
  aplicação e não reintroduz sidebar global.
- Cartões podem ser movidos por drag-and-drop e por alternativa acessível de teclado.
- Colunas podem ser reordenadas por drag-and-drop e por controles acessíveis.
- Erro na movimentação restaura a última posição confirmada pelo servidor e apresenta a causa.

## Persistência e migração futura

- O modelo desejado requer substituir a associação `Project -> Boards` como autoridade funcional por vínculo
  de `Board` com `Organization` e escopo de acesso próprio.
- `WorkItemBoardPlacement` N:N deve ser convertido para uma única posição operacional por tarefa.
- Os vínculos legados precisam de regra de seleção do quadro/posição canônica, relatório de ambiguidades e
  rollback verificável.
- `WorkflowStatusId` e templates por projeto permanecem no código atual somente como legado técnico. Conforme
  D65, o nome da coluna é o status textual canônico e sua classificação aberta/concluída determina o estado da
  tarefa; a estratégia de compatibilidade deve impedir divergência antes da migration.
- O acesso derivado Quadro -> Projetos exige modelo de autorização que preserve o nível da permissão do quadro
  sem invalidar concessões independentes já existentes.
- Arquivamento de quadro precisa preservar quais tarefas foram arquivadas pela operação para restauração segura.
- Nenhuma dessas mudanças está autorizada por esta revisão. Exigem `G-MIGRATION`, `G-WORKFLOW` e plano de dados.

## API desejada

- Listar quadros vinculados ao usuário ou às suas equipes no tenant ativo.
- Consultar quadro com todas as colunas e tarefas autorizadas.
- Criar, editar e arquivar quadro com **Administrar quadros**.
- Restaurar quadro e tarefas com autorização administrativa.
- Vincular/desvincular usuários e equipes e manter acesso derivado aos projetos representados.
- Criar, editar, reordenar, classificar e remover coluna com **Administrar quadros**.
- Pré-visualizar o impacto da reclassificação nos dois sentidos e devolver fotografia/versão para confirmação.
- Confirmar atomicamente a reclassificação, conclusão ou reabertura das tarefas da coluna e, quando autorizado
  na conclusão, de todos os descendentes abertos recursivos, gerando histórico por tarefa.
- Transferir tarefa entre quadro/coluna e reordenar dentro da coluna.
- Validar tenant, permissão, WIP, classificação e concorrência no backend.
- Controllers apenas orquestram MediatR; operações de banco são assíncronas e transacionais quando compostas.

## Critérios de aceite

1. A aplicação autenticada abre no Kanban e permite escolher outro quadro pelo seletor superior.
2. Um quadro autorizado mostra tarefas de projetos diferentes sem misturar organizações.
3. Usuário não vinculado direta ou indiretamente por equipe não visualiza nem enumera o quadro.
4. Quem acessa o quadro vê todas as tarefas nele contidas e recebe nos projetos representados o mesmo nível de
   acesso que possui no quadro.
5. Cada tarefa possui uma única posição atual e não aparece simultaneamente em dois quadros.
6. Colunas podem receber qualquer nome e ordem, sem etapa `Backlog` obrigatória.
7. Cada coluna é classificada como aberta ou concluída; entrada em concluída conclui e saída reabre a tarefa.
8. Reclassificar uma coluna aberta com tarefas exige confirmação com contagens e conclui atomicamente todas as
   tarefas abertas nela.
9. Havendo pai afetado com descendentes abertos, a confirmação exige consentimento para concluir toda a árvore
   recursivamente; sem consentimento, nenhuma mudança persiste.
10. Cada tarefa concluída pela operação recebe seu próprio evento histórico.
11. Reclassificar coluna concluída como aberta exige confirmação e reabre atomicamente todas as tarefas nela.
12. Reclassificação e movimento individual mantêm coerentes classificação da coluna e estado da tarefa.
13. Usuário sem **Administrar quadros** não cria, edita, arquiva nem administra colunas, mesmo chamando a API.
14. Usuário/perfil com a permissão consegue administrar quadro e colunas sem depender de papel fixo.
15. Arquivar quadro com tarefas confirma o impacto e torna quadro e tarefas invisíveis nas telas normais.
16. Somente administrador restaura o quadro e as tarefas arquivadas por ele, sem reativar tarefas já arquivadas.
17. Ordem e movimentos sobrevivem ao reload e falhas não deixam estado parcial.
18. O filtro abre em painel contextual recolhível/overlay sem recriar sidebar global persistente.

## Testes futuros

- .NET: tenant, vínculo usuário/equipe, acesso derivado ao projeto, permissão configurável, posição única,
  transferência, WIP, conclusão/reabertura e concorrência.
- .NET: prévia de impacto, conclusão/reabertura em lote, conclusão recursiva, transação e rollback, fotografia
  obsoleta e um histórico por tarefa nos dois sentidos da reclassificação.
- .NET/SQL Server: migration de N:N para posição única, resolução de ambiguidades e rollback.
- .NET: arquivamento transacional de quadro/tarefas e restauração exclusiva de administrador.
- React: seletor de quadro, ausência de colunas obrigatórias, gestão condicionada por permissão, confirmação com
  contagens, consentimento recursivo, conclusão/reabertura coerentes e rollback visual.
- Playwright desktop/mobile: login -> Kanban, troca de quadro, painel de filtros, movimentos, reclassificação de
  coluna nos dois sentidos, recusa/aceite da conclusão recursiva e arquivamento.

## Gaps conhecidos

- O código atual ainda restringe quadros a projetos.
- O banco atual permite várias posições por tarefa.
- A criação atual exige/gera `Backlog` e quadro padrão por projeto.
- O workflow atual usa status por projeto e sincronização entre projeções.
- A home autenticada atual não é o Kanban.
- O seletor superior transversal e o painel de filtros no perfil Runrun.it ainda não existem integralmente.
- A autorização atual precisa ser auditada/migrada para vínculos de usuários/equipes, acesso derivado aos projetos
  e permissão configurável **Administrar quadros**.
- O arquivamento atual realoca/remove projeções; não arquiva o quadro junto com todas as tarefas.
- Não existe endpoint moderno para editar a classificação aberta/concluída de uma coluna transversal. O update
  legado altera vínculo com `WorkflowStatus` dentro do projeto, sem executar o contrato coletivo da D62.
- Não existe prévia nem diálogo de confirmação com contagem de tarefas e descendentes afetados.
- A mudança de categoria da coluna não conclui tarefas em lote, não percorre descendência recursiva, não grava
  histórico individual e não reúne toda a operação numa transação com controle de concorrência.
- Não existe reclassificação bidirecional que conclua ou reabra todas as tarefas com confirmação, atomicidade e
  histórico individual; o código atual atualiza conclusão principalmente durante movimento individual.

## Human Gates

- `G-SPEC`: já aprovado pelo PO para este contrato documental.
- `G-SCOPE`: decisão registrada na D52.
- `G-WORKFLOW`: obrigatório antes de alterar classificação, conclusão, reabertura ou transições.
- `G-MIGRATION`: obrigatório antes de alterar relações, posições, autorização derivada ou arquivamento persistente.
- `G-HISTORY`: obrigatório somente se a estrutura imutável do histórico precisar mudar.

## Referências

- D51, D52, D53, D62 e D65 em `DECISIONS.md`.
- `SPEC-TOP-NAVIGATION-SHELL`.
- `SPEC-SEARCH-SAVED-FILTERS`.
- `SPEC-USER-ACCESS-PERMISSIONS`.
- `SPEC-WORK-ITEM-MANAGEMENT`.
- `SPEC-MULTI-BOARD-VIEWS` (histórico superseded).
