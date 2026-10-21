# SPEC-SEARCH-SAVED-FILTERS: Busca e Filtros Salvos

**Status:** approved

**Decisão humana:** PO definiu em 2026-08-24 que todo filtro salvo é exclusivamente pessoal e escolhe escopo entre o quadro atual e qualquer quadro acessível. Não existem filtros compartilhados.

## Objetivo

Oferecer busca e filtros reutilizáveis sem expor dados não autorizados e sem transformar o painel em uma grade permanente. Cada usuário administra somente os próprios filtros; nenhum filtro altera a experiência de outra pessoa.

## Estado atual comprovado

- Existe busca global autenticada em `GET /api/search?q={termo}&limit={n}`; não há busca dedicada de `WorkItem`.
- Kanban e Product Backlog possuem busca, critérios, barra compacta, contador e limpeza.
- `SavedFilter` persiste `Id`, `BoardId`, `UserId`, `Name`, `FilterJson` e `CreatedAt`.
- A consulta real filtra por `BoardId + UserId`; portanto, os filtros existentes já são privados e restritos a um quadro.
- A API oferece listar, criar e excluir no caminho `/api/boards/{boardId}/saved-filters`; não oferece obter por id nem atualizar/renomear.
- A UI salva pelo quadro atual e não oferece escopo global pessoal.
- O painel completo no perfil Runrun.it, chips, rascunho separado e preservação integral na URL continuam incompletos.

## Escopo

- Busca textual e estruturada somente sobre itens autorizados.
- Filtros por campos realmente suportados, como projeto, quadro, coluna, tipo, prioridade, responsável, criador, tags e datas.
- Painel contextual recolhível no desktop e overlay no mobile.
- Rascunho que só afeta resultados após `Aplicar`.
- Chips legíveis para critérios ativos, incompatíveis e ignorados.
- Filtros salvos exclusivamente pessoais.
- Escopo pessoal `Quadro atual` ou `Qualquer quadro acessível`.
- Criar, aplicar, renomear, atualizar, duplicar e excluir somente filtros próprios.
- Preservação segura em URL quando o critério for serializável e não sensível.
- Integração com Kanban, Product Backlog, listas e relatórios compatíveis.

## Fora do escopo

- Filtro compartilhado com quadro, equipe, projeto, organização ou outro usuário.
- Publicação, aprovação ou governança de filtros coletivos.
- Compartilhamento público anônimo.
- Pesquisa em conteúdo binário de anexos.
- SQL livre, busca semântica, vetorial ou por IA.
- Ações em massa, cobertas por `SPEC-BULK-ACTIONS-AUTOMATIONS`.
- Persistência da ordem vertical temporária de uma visão filtrada.

## Atores e autorização

1. Usuário autenticado busca somente recursos que já pode visualizar.
2. Cada filtro salvo possui exatamente um proprietário (`UserId`).
3. Somente o proprietário pode visualizar, aplicar, editar, renomear, duplicar ou excluir seu filtro.
4. Administrador, gestor, membro ou qualquer outro usuário não visualiza nem administra o filtro pessoal alheio por causa de seu papel.
5. A posse ou aplicação de filtro nunca concede acesso a quadro, projeto, pessoa ou tarefa.
6. Toda consulta revalida tenant e autorização antes de filtrar, contar ou paginar.

## Regras funcionais

### Painel e aplicação

1. A barra contextual prioriza busca, botão `Filtros (N)`, ordenação, quantidade de resultados e quadro ativo.
2. O painel reúne subtarefas, filtros salvos, critérios ativos e filtros avançados, com rodapé fixo `Aplicar`, `Salvar` e `Limpar`.
3. Alterar texto ou critério modifica somente o rascunho. O resultado permanece inalterado até o usuário acionar `Aplicar`.
4. `Salvar` persiste a definição pessoal, mas não substitui sozinho o conjunto já aplicado.
5. `Limpar` limpa o rascunho; o resultado muda somente depois de `Aplicar`.
6. Abrir e fechar o painel não descarta o rascunho. A preferência aberto/fechado é lembrada por usuário e respeita a viewport.
7. Critérios aplicados aparecem como chips removíveis com rótulo humano, contador e ação individual.
8. Alterar uma definição aplicada marca `Alterações não salvas` e nunca sobrescreve o filtro persistido automaticamente.
9. Estados vazio, carregando, erro e critério inválido são distintos e acessíveis.

### Propriedade e escopo

10. Todo filtro salvo é pessoal; a interface não oferece `Compartilhar`, `Publicar` ou `Compartilhado com o quadro`.
11. Ao salvar, o usuário escolhe um dos escopos:
    - `Quadro atual`: aparece e pode ser aplicado somente no quadro onde foi criado;
    - `Qualquer quadro acessível`: aparece em todos os quadros que o proprietário puder acessar.
12. Trocar o escopo, nome ou definição exige salvar explicitamente e somente o proprietário pode fazê-lo.
13. Filtro pessoal global não atravessa organização e não torna um quadro enumerável; ele só aparece depois que o acesso ao quadro atual foi comprovado.
14. Ao perder acesso a um quadro, o filtro global deixa de ser utilizável nele sem revelar metadados desse quadro.

### Compatibilidade entre quadros

15. Ao aplicar filtro global, cada critério é revalidado contra o quadro atual e suas permissões.
16. Critério compatível é aplicado normalmente.
17. Critério específico incompatível, como uma coluna inexistente no quadro atual, é ignorado somente naquele quadro; os demais critérios válidos continuam aplicados.
18. Critério ignorado não é removido nem reescrito na definição persistida.
19. A interface exibe aviso e chip claro com o nome do critério ignorado e a razão objetiva da incompatibilidade.
20. Ao entrar depois em quadro compatível, o mesmo critério volta a ser aplicado sem nova edição do filtro.
21. Se nenhum critério permanecer aplicável, a tela informa que o filtro não possui critérios compatíveis para o quadro atual e não simula resultado filtrado.

### Relação com ordem e navegação

22. Um filtro ativo cria apenas uma visão pessoal temporária sobre a ordem compartilhada do Kanban, conforme D60.
23. Reordenar verticalmente o subconjunto filtrado não integra `FilterJson`, não é salvo e é descartado ao alterar ou remover o filtro.
24. Mover uma tarefa entre colunas com filtro ativo continua sendo alteração real e persistente.
25. Critérios válidos permanecem ao alternar entre Product Backlog e Kanban; incompatíveis seguem a regra de aviso/ignorados.
26. No Product Backlog, o critério inicial é `Sem sprint`, com opção explícita de incluir tarefas em sprint conforme D54.

### Segurança e determinismo

27. A definição usa schema versionado e validado, nunca SQL ou expressão executável.
28. Valores removidos ou não autorizados não podem revelar dados por rótulos, sugestões, erros ou contagens.
29. Paginação e ordenação usam desempate determinístico.
30. URL, API e persistência usam representação compatível e revalidada no backend.
31. Limites de texto, quantidade de critérios, listas de valores e profundidade devem proteger a consulta.

## Experiência e acessibilidade

- No desktop, o painel é lateral contextual, recolhível e temporário; não recria sidebar global.
- No mobile, usa overlay com área útil, ações fixas e alvos de toque de pelo menos 44 px.
- Teclado e leitor de tela conseguem abrir o painel, navegar entre grupos, remover chips e devolver foco ao acionador.
- O contador de filtros possui nome acessível completo.
- Opções longas usam texto completo e tooltip acessível; estado não depende apenas de cor.
- Chips ignorados são visualmente diferentes dos aplicados e explicam por que não afetaram aquele quadro.

## Persistência e impacto de dados

- O modelo atual (`BoardId` obrigatório + `UserId`) atende somente filtros pessoais de quadro.
- O escopo pessoal global exige representar explicitamente o alcance do filtro sem remover seu proprietário nem o tenant. A solução física deve ser auditada antes da implementação.
- Se `BoardId` precisar ficar opcional ou houver novo campo/índice de escopo, a alteração exige `G-MIGRATION`.
- Filtros existentes devem migrar ou permanecer como `Quadro atual`; nunca podem virar globais ou compartilhados implicitamente.
- Não é necessário campo de visibilidade compartilhada, pois essa funcionalidade foi removida do contrato.
- A preferência aberto/fechado deve ser isolada por usuário; sua estratégia concreta não pode vazar entre contas no mesmo navegador.
- `FilterJson` não armazena ordem vertical pessoal temporária, credenciais, tokens ou valores não autorizados.

## Contratos esperados

### Busca

- `GET /api/search?q={termo}&limit={n}` permanece como busca global autenticada atual.
- Uma busca dedicada a tarefas só deve ser criada se a implementação comprovar necessidade e definir campos, normalização e paginação.

### Filtros salvos

- A API deve listar filtros do proprietário aplicáveis ao contexto atual: pessoais do quadro e pessoais globais.
- Criar/atualizar recebe nome, definição validada e escopo `Board` ou `Global`.
- Obter, editar, renomear e excluir valida `UserId` proprietário no backend.
- Aplicar filtro global retorna ou permite derivar quais critérios foram ignorados e a razão segura de cada um.
- Rotas exatas podem evoluir do caminho atual por quadro, mas nenhum endpoint lista ou modifica filtros de outro usuário.

## Critérios de aceite

- **Dado** um usuário com acesso parcial **quando** busca termo de recurso não autorizado **então** nenhum resultado, contagem, sugestão ou rótulo é revelado.
- **Dado** um filtro salvo **quando** outro usuário acessa o mesmo quadro **então** não vê, aplica, edita nem exclui esse filtro.
- **Dado** o diálogo de salvar **quando** é aberto **então** oferece somente `Quadro atual` e `Qualquer quadro acessível`, sem opção compartilhada.
- **Dado** um filtro de quadro **quando** o proprietário muda para outro quadro **então** esse filtro não aparece como aplicável.
- **Dado** um filtro global pessoal **quando** o proprietário abre outro quadro acessível **então** o filtro aparece para ele e para nenhum outro usuário.
- **Dado** um filtro global com critério de coluna incompatível **quando** é aplicado em outro quadro **então** esse critério é ignorado, os demais são aplicados e um chip/aviso identifica claramente o critério ignorado.
- **Dado** o mesmo filtro global **quando** volta a um quadro compatível **então** o critério preservado volta a ser aplicado.
- **Dado** um filtro sem critérios compatíveis **quando** é aplicado **então** a tela explica a incompatibilidade e não apresenta o conjunto integral como se estivesse filtrado.
- **Dado** critérios editados no painel **quando** `Aplicar` ainda não foi acionado **então** os resultados anteriores permanecem.
- **Dado** um filtro aplicado alterado **quando** a definição local diverge **então** a interface mostra `Alterações não salvas` e não sobrescreve automaticamente.
- **Dado** uma reordenação vertical com filtro ativo **quando** o filtro muda ou é removido **então** a ordem temporária é descartada e nunca foi salva no filtro.
- **Dado** filtro ativo **quando** uma tarefa é movida para outra coluna **então** o movimento real persiste.
- **Dado** viewport móvel **quando** o painel abre **então** ações e critérios permanecem acessíveis sem rolagem horizontal.

## Gaps entre código e contrato

### G-FILTER-001 — não existe escopo pessoal global

`SavedFilter.BoardId` é obrigatório, o repositório consulta por `BoardId + UserId` e as rotas exigem quadro. Não há representação nem listagem de filtros pessoais em qualquer quadro acessível.

### G-FILTER-002 — salvar não oferece escolha de escopo

`Kanban.tsx` pede apenas o nome por `window.prompt` e cria o filtro no quadro selecionado. Falta diálogo acessível com `Quadro atual`/`Qualquer quadro acessível`.

### G-FILTER-003 — aplicar não revalida critérios por quadro

A definição JSON é mesclada diretamente no estado do Kanban. Não existe classificação de critério compatível/ignorado, aviso seguro nem chip explicativo.

### G-FILTER-004 — edição e propriedade estão incompletas na experiência

A API atual lista, cria e exclui filtros próprios por quadro, mas não possui `GET` individual nem `PUT` para renomear, atualizar definição ou trocar escopo.

### G-FILTER-005 — painel ainda não implementa todo o rascunho aprovado

Chips completos, separação rigorosa rascunho/aplicado, estado alterado, critérios ignorados, preservação de URL e painel Runrun.it desktop/mobile ainda precisam de cobertura integral.

### G-FILTER-006 — contrato documental anterior oferecia compartilhamento

A versão anterior da spec e a D52 permitiam filtros compartilhados com o quadro. Essa opção foi removida pela D64 e não deve ser implementada. O código atual não persiste compartilhamento, portanto seu comportamento privado existente deve ser preservado durante a evolução para escopo global pessoal.

### G-FILTER-007 — ordem temporária filtrada ainda não existe

Conforme o gap registrado em `SPEC-KANBAN-VISUAL-ORDER`, a UI não implementa reordenação vertical pessoal isolada e não comprova que ela fica fora do filtro salvo.

## Testes futuros

- **.NET:** isolamento por proprietário/tenant; escopos Board/Global; acesso perdido; schema; critérios incompatíveis; paginação; autorização antes de contagem.
- **SQL Server:** migração segura de filtros existentes para escopo Board e índices do novo alcance, se necessários.
- **React:** diálogo de escopo; ausência de compartilhamento; rascunho/Aplicar; chips ativos e ignorados; edição própria; estado não salvo; ordem temporária fora do payload.
- **Playwright:** dois usuários no mesmo quadro sem filtros cruzados; filtro global entre quadros compatível/incompatível; aviso; mobile; teclado; mudança real de coluna com filtro ativo.

## Decisões ainda pendentes

- Campos e operadores pesquisáveis no piloto.
- Semântica de texto para acentos, termos parciais e relevância.
- Filtro favorito/padrão por tela.
- Política de nomes duplicados, exclusão lógica/física e concorrência de edição.
- Limites de complexidade, página, volume e latência.
- Estratégia de busca textual (`LIKE`, full-text ou outra comprovada).

## Human Gates

- `G-SPEC`: contrato aprovado explicitamente pelo PO.
- `G-MIGRATION`: obrigatório se o escopo global exigir alteração de schema ou índices.
- `G-SCOPE`: somente para reintroduzir compartilhamento, novos operadores ou semântica não definida.
- `G-COMPLETION`: conforme classificação da tarefa de implementação.

## Rastreabilidade

`D52` (parcialmente sucedida) + `D60` + `D64` → `SPEC-SEARCH-SAVED-FILTERS` → `TASK-031` → testes .NET, React e Playwright → homologação manual
