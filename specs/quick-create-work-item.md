# SPEC-QUICK-CREATE-WORK-ITEM: Criação rápida de tarefa

**Status:** approved
**Baseline:** contrato funcional aprovado por PO em 2026-08-24, alinhado às D52–D54 e comparado com a implementação atual.

## Propósito

Criar uma única tarefa já posicionada em um único quadro e uma única coluna, com projeto e responsável explícitos, partindo tanto de uma coluna do Kanban quanto da ação global `Novo item`.

## Modelo funcional

- A tarefa é o registro canônico.
- Cada tarefa possui um projeto.
- Cada tarefa possui uma única posição atual em um quadro e coluna conforme D52.
- Quadro é transversal à organização e pode conter tarefas de projetos diferentes.
- A relação entre projeto e quadro é derivada das tarefas posicionadas; não exige que o quadro seja filho ou vínculo fixo do projeto.
- Sprint é opcional e independente da posição no quadro conforme D54.
- Criação multiquadro e `WorkItemBoardPlacement` N:N pertencem ao modelo legado sucedido pela D52 e não são o alvo funcional.

## Campos obrigatórios

1. Título.
2. Projeto.
3. Quadro.
4. Coluna.
5. Responsável principal.

O responsável começa sempre vazio. O sistema não escolhe automaticamente o usuário atual, líder, gestor, primeiro membro ou responsável anterior.

## Formas de abertura

### Pelo botão de uma coluna

- O botão da coluna abre o mesmo formulário centralizado de criação.
- Quadro e coluna de origem vêm preenchidos.
- Projeto começa vazio e precisa ser escolhido.
- Responsável começa vazio e precisa ser escolhido.
- Quadro e coluna podem ser alterados, desde que o destino permaneça acessível e válido.

### Pelo botão global `Novo item`

- O formulário abre sem quadro e sem coluna selecionados.
- Projeto começa vazio, salvo contexto explícito de projeto já presente na rota; mesmo quando pré-preenchido, continua sujeito à validação de acesso.
- Responsável começa vazio.
- O usuário seleciona todos os campos obrigatórios antes de criar.

## Seleção de quadro e coluna

1. O seletor de quadro mostra todos os quadros ativos acessíveis ao usuário na organização corrente.
2. O seletor possui busca por nome.
3. O seletor não fica restrito aos quadros historicamente associados ao projeto escolhido, pois quadro é transversal.
4. Selecionar projeto não cria associação persistente com o quadro.
5. A relação projeto–quadro passa a existir funcionalmente quando a tarefa é criada naquele quadro.
6. O seletor de coluna depende do quadro selecionado e lista somente suas colunas ativas e acessíveis.
7. Ao trocar o quadro, a coluna anterior é limpa imediatamente.
8. Nenhuma coluna é selecionada automaticamente no fluxo global.
9. A criação nunca procura uma coluna chamada `Backlog` nem usa a primeira coluna como fallback.
10. Se não houver quadro acessível, o formulário apresenta somente um estado vazio/informativo.
11. Nesse estado não existe ação `Criar quadro` e nenhum quadro é criado automaticamente.
12. Se o quadro escolhido não possuir coluna disponível, o formulário explica o impedimento e não permite criar a tarefa.

## Projeto, acesso e responsável

1. Criador precisa possuir acesso suficiente tanto ao projeto quanto ao quadro selecionado.
2. O acesso ao quadro não permite capturar um projeto arbitrário sem acesso.
3. Quando o projeto já estiver representado por tarefas no quadro, aplica-se o acesso derivado definido pela D52 e pela D55.
4. Para introduzir no quadro a primeira tarefa de um projeto ainda não representado, o criador precisa possuir acesso independente ao projeto e acesso ao quadro.
5. O responsável precisa ser membro ativo da organização e estar disponível para atribuição no contexto combinado de projeto e quadro.
6. O seletor de responsável usa nome funcional; e-mail aparece somente como fallback permitido pela política de identidade.
7. Trocar projeto ou quadro limpa o responsável se ele deixar de ser elegível no novo contexto.
8. IDs de outro tenant, projeto ou quadro inacessível, coluna de outro quadro e responsável inelegível são rejeitados pelo backend.

## Envio e feedback

1. `Criar item` fica desabilitado até os cinco campos estarem válidos.
2. Ao enviar, o botão entra em estado carregando e o formulário impede clique ou submissão duplicada.
3. Uma única intenção do usuário não pode criar duas tarefas, mesmo com duplo clique ou repetição do evento na interface.
4. A criação é atômica: tarefa, posição, responsável e histórico inicial são persistidos juntos ou nada é criado.
5. Em falha:
   - o formulário permanece aberto;
   - todos os valores informados são preservados;
   - cada erro aparece junto ao campo correspondente;
   - erro sem campo identificável aparece em uma mensagem geral;
   - nenhuma tarefa ou posição parcial permanece salva.
6. Em sucesso:
   - o formulário de criação fecha;
   - o Kanban e demais consultas afetadas são atualizados sem reload;
   - o modal centralizado de detalhe da nova tarefa abre imediatamente;
   - a rota, quadro, filtros, posição de rolagem e demais contexto de origem são preservados para o fechamento do detalhe e para voltar/avançar no navegador.

## Regras de domínio

1. Título não pode ser vazio e respeita o limite do domínio.
2. Projeto, quadro, coluna e responsável são obrigatórios no backend, não apenas na interface.
3. Quadro pertence à organização ativa.
4. Coluna pertence ao quadro informado.
5. Projeto pertence à organização ativa.
6. Responsável pertence à organização ativa e é atribuível no contexto.
7. Uma tarefa recebe somente `BoardId` e `StageId` atuais; não recebe lista de quadros.
8. Limite WIP e demais regras ativas da coluna são validados antes da persistência.
9. Posição inicial é calculada de forma consistente dentro da coluna, sem colisão silenciosa.
10. A criação registra autoria, responsável e entrada inicial na coluna no histórico aplicável.

## Estado atual comprovado no código

- Existem duas experiências diferentes: `QuickCreateDialog` global e um modal legado dentro de `Kanban`.
- O modal global exige título e projeto, seleciona automaticamente um projeto/quadro e aceita `boardIds` com múltiplos quadros.
- O modal global não possui coluna nem responsável.
- O texto atual afirma que somente o título é necessário.
- O botão de uma coluna preenche internamente `selectedBoardId` e `stageId`, mas seu modal solicita apenas título e campos opcionais; projeto e responsável não são exigidos.
- O frontend envia `boardId`, `boardIds`, `projectId` e `stageId` opcionais conforme o fluxo.
- O backend resolve múltiplos quadros, cria placements N:N e procura automaticamente uma coluna `Backlog`/`Ready` quando `StageId` não é informado.
- `ResponsibleId` e `StageId` continuam opcionais no comando e no modelo atual.
- O handler valida existência de responsável/participantes quando informados, mas não comprova integralmente sua elegibilidade conjunta no projeto e quadro.
- O sucesso atual fecha a criação e mostra mensagem/atualiza consultas, mas não abre imediatamente o detalhe da tarefa criada.

## Gaps entre contrato e implementação

1. **Modelo N:N legado:** criação ainda aceita `BoardIds` e produz múltiplos placements.
2. **Dois formulários:** ação global e botão da coluna abrem experiências diferentes, com campos e validações divergentes.
3. **Obrigatoriedade incompleta:** projeto, coluna e responsável não são obrigatórios uniformemente no frontend e backend.
4. **Defaults indevidos:** fluxo global seleciona automaticamente primeiro projeto/quadro, contrariando quadro e coluna vazios e responsável vazio.
5. **Coluna automática:** backend procura `Backlog`/`Ready` quando a coluna não é enviada.
6. **Quadros por projeto:** modal global obtém quadros de `ProjectSummary.boards`, não todos os quadros transversais acessíveis da organização.
7. **Busca de quadro ausente:** lista atual não possui pesquisa por nome.
8. **Coluna ausente no global:** não existe seletor dependente do quadro.
9. **Responsável ausente:** nenhum dos dois formulários exige a escolha inicial.
10. **Autorização conjunta:** não foi comprovada validação atômica de acesso ao projeto, quadro, coluna e responsável.
11. **Estado vazio inadequado:** a experiência atual ainda se apoia em quadros do projeto/default e não implementa o estado informativo definido para ausência de quadro acessível.
12. **Erros pouco precisos:** fluxo da coluna usa alerta genérico e o global concentra os erros em uma única mensagem, sem associação consistente por campo.
13. **Duplo envio:** existe estado de mutation no global, mas a proteção completa e o comportamento do modal legado não estão cobertos por evidência.
14. **Pós-criação:** não abre automaticamente o modal centralizado da nova tarefa nem comprova preservação de rota/filtros/scroll.
15. **Atomicidade e idempotência:** não há evidência suficiente para repetição de requisição, concorrência e ausência de registros parciais no modelo-alvo singular.

## Interface esperada

- Um único componente de criação rápida reutilizado no topo global e nas colunas.
- Layout centralizado, responsivo e acessível por teclado.
- Ordem sugerida: título, projeto, quadro pesquisável, coluna dependente e responsável.
- Campos pré-preenchidos conforme a origem, sem defaults indevidos.
- Mensagens de validação junto aos campos.
- Estado vazio claro para nenhum quadro acessível ou nenhuma coluna disponível.
- Botão com estado carregando e prevenção de múltiplos envios.
- Sucesso encadeado diretamente ao modal de detalhe.

## API esperada

- Contrato de criação singular com `ProjectId`, `BoardId`, `StageId`, `ResponsibleId` e `Title` obrigatórios.
- Nenhum `BoardIds` funcional no contrato-alvo.
- Validação de tenant e autorização para todas as referências.
- Validação de pertencimento da coluna ao quadro.
- Validação de responsável ativo e atribuível.
- Resposta de sucesso contendo o identificador necessário para abrir o detalhe da nova tarefa.
- Erros estruturados por campo/regra para apresentação no formulário.
- Transação única para tarefa, posição, responsável e histórico inicial.

## Critérios de aceite

- **Dado** o botão de uma coluna, **quando** abrir a criação, **então** quadro e coluna estão preenchidos e projeto/responsável estão vazios.
- **Dado** o botão global, **quando** abrir a criação, **então** quadro, coluna e responsável estão vazios.
- **Dado** o seletor de quadro, **quando** pesquisar, **então** lista todos e somente os quadros ativos acessíveis da organização.
- **Dado** um quadro selecionado, **quando** abrir o seletor de coluna, **então** aparecem somente colunas daquele quadro.
- **Dado** uma troca de quadro, **quando** a seleção mudar, **então** a coluna anterior é removida.
- **Dado** um usuário sem quadros acessíveis, **quando** abrir a criação, **então** vê estado informativo sem ação de criar quadro.
- **Dado** qualquer campo obrigatório ausente, **quando** tentar enviar, **então** nada é criado e o campo é identificado.
- **Dado** projeto e quadro de contextos incompatíveis ou inacessíveis, **quando** enviar, **então** a operação é rejeitada sem efeito parcial.
- **Dado** um clique duplo em `Criar item`, **quando** a primeira submissão estiver em andamento, **então** somente uma criação é processada.
- **Dado** uma falha de validação ou API, **quando** retornar, **então** o modal continua aberto com valores e erros preservados.
- **Dado** uma criação válida, **quando** concluir, **então** existe um único `WorkItem` em um único quadro/coluna com responsável definido.
- **Dado** o sucesso, **quando** o formulário fechar, **então** o modal centralizado da nova tarefa abre sem perder o contexto do Kanban/rota.

## Testes e homologação

### Evidência automatizada necessária

- React: origem global vazia, origem de coluna pré-preenchida, dependência quadro→coluna e responsável vazio.
- React: busca de quadros, estado sem quadro, erros por campo, preservação dos valores e carregamento.
- React: duplo clique produz uma única chamada e sucesso abre o detalhe preservando o contexto.
- .NET: campos obrigatórios, tenant, acesso conjunto, responsável, coluna do quadro, WIP e posição singular.
- .NET integração: transação/rollback, concorrência e repetição da intenção sem registros parciais.
- Playwright desktop/mobile: criar pela coluna e pela ação global, validar erros, abrir detalhe e retornar ao mesmo contexto.

### Homologação manual pendente

1. Abrir pela coluna e conferir quadro/coluna preenchidos e projeto/responsável vazios.
2. Abrir globalmente e conferir quadro/coluna/responsável vazios.
3. Pesquisar e selecionar um quadro transversal acessível.
4. Trocar quadro e verificar limpeza/recarga das colunas.
5. Simular ausência de quadros e confirmar estado apenas informativo.
6. Provocar erro e confirmar modal, valores e mensagens por campo.
7. Clicar duas vezes em criar e confirmar apenas uma tarefa.
8. Criar com sucesso e confirmar abertura imediata do detalhe e retorno ao mesmo contexto.

## Human Gates futuros

- `G-MIGRATION`: conversão segura do placement N:N legado para posição singular da D52.
- `G-WORKFLOW`: adequação das regras de coluna/posição e remoção do fallback `Backlog`.
- `G-HISTORY`: somente se a estrutura imutável dos eventos de criação precisar mudar.

## Referências

- `DECISIONS.md` — D49, D52, D53, D54 e D55
- `specs/work-item-management.md`
- `specs/boards-stages-wip.md`
- `specs/task-history.md`
- `specs/top-navigation-shell.md`
- `src/Detran.Kanban.Web/src/components/GlobalActions.tsx`
- `src/Detran.Kanban.Web/src/pages/Kanban.tsx`
- `src/Detran.Kanban.Application/Features/WorkItems/Commands/CreateWorkItemCommand.cs`
- `src/Detran.Kanban.Application/Features/WorkItems/Commands/CreateWorkItemCommandHandler.cs`
- `src/Detran.Kanban.Api/Controllers/WorkItemsController.cs`

## Rollback

Esta revisão altera somente documentação. Não muda formulário, endpoint, banco ou dados existentes.

## Rastreabilidade

D52–D55 → `SPEC-QUICK-CREATE-WORK-ITEM` → gap do modelo N:N/UX atual → futura implementação singular → testes → homologação manual.
