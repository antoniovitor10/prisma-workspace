# SPEC-TOP-NAVIGATION-SHELL: Navegação Superior sem Barra Lateral

**Status:** approved

## Objective

Remover a barra lateral persistente e reorganizar a aplicação em um shell limpo de largura total, com uma
barra superior para navegação global. A primeira tela autenticada é o Kanban; o topo contém o seletor do
quadro ativo e os controles contextuais, enquanto filtros avançados abrem em painel lateral recolhível/overlay.

## Context

### Atual comprovado
- O shell superior sem sidebar persistente foi implantado na TASK-032 e está em produção no commit histórico
  `f264792`; o bundle validado não contém a antiga `Sidebar` como navegação ativa.
- A topbar contém destinos globais, busca, organização ativa, timer, `Novo item`, notificações e conta.
- Barras contextuais por rota existem, mas a aplicação ainda abre na listagem de projetos, não no Kanban.
- O seletor superior de quadros transversais e o estado `Nenhum quadro selecionado` ainda não existem.
- Filtros atuais são parciais e não reproduzem integralmente o painel Runrun.it da spec de filtros.

### Desejado
- A sidebar deixa de existir em desktop e mobile.
- A área de conteúdo passa a usar toda a largura disponível.
- Recursos globais da sidebar migram para a navegação superior.
- O login direciona ao Kanban e o seletor superior permite passear somente pelos quadros vinculados ao usuário
  ou às suas equipes.
- Breadcrumbs, quadro ativo e ações específicas ocupam o topo contextual. Filtros avançados usam painel
  contextual recolhível/overlay, sem recriar uma coluna global persistente.
- A densidade diminui sem esconder funções, permissões ou estado atual.

## Scope

- Novo shell de duas faixas superiores, sticky quando houver espaço.
- Migração integral da navegação global da sidebar.
- Slot/contrato de barra contextual por rota.
- Integração inicial com Project Workspace, Product Backlog e Kanban.
- Kanban como rota inicial autenticada e seletor superior de quadros.
- Comportamento responsivo, teclado, leitor de tela e overflow.
- Remoção do estado/localStorage de recolhimento da sidebar.

## Out of Scope

- Renomear módulos ou alterar permissões/RBAC.
- Alterar contratos de busca, filtros ou criação de tarefas além das specs próprias.
- Criar uma nova sidebar recolhível como substituição.
- Manter duas navegações globais duplicadas.

## Functional Requirements

1. `AppShell` deve abandonar a coluna lateral e usar `topbar`, `contextbar` opcional e `content` em largura total.
2. A navegação global deve preservar, conforme autorização, os destinos atuais:
   - Meu trabalho;
   - Projetos;
   - Solicitações;
   - Relatórios;
   - Equipes;
   - Configurações.
3. O destino ativo deve ser identificado por texto, estado visual e `aria-current="page"`, sem depender somente
   de cor.
4. Busca global, organização ativa, timer, `Novo item`, notificações, perfil e sair devem continuar disponíveis,
   reorganizados por prioridade e sem competição visual com a navegação.
5. No desktop, destinos globais frequentes ficam visíveis como links. Itens menos frequentes podem entrar em
   `Mais`, desde que o destino ativo nunca fique invisível sem indicação.
6. Configurações e sair devem ficar no menu da conta ou em overflow claramente identificado, respeitando as
   permissões atuais.
7. A barra contextual deve aceitar por rota:
   - breadcrumbs;
   - título/visão atual quando necessário;
   - troca Product Backlog/Quadro/Sprints/Relatórios;
   - busca e acionador do painel de filtros da tela;
   - ordenação, agrupamento e ações contextuais.
8. No Kanban, a barra contextual hospeda busca compacta, seletor do quadro ativo, resumo dos filtros e o
   acionador do painel definido em `SPEC-SEARCH-SAVED-FILTERS`. Os critérios avançados não ficam expostos como
   uma grade permanente no topo.
9. Controles movidos para a barra contextual não podem permanecer duplicados no corpo da página.
10. O conteúdo principal deve começar alinhado ao shell, com espaçamento consistente e sem o vazio reservado
    pela sidebar antiga.
11. As duas faixas devem ter hierarquia visual discreta: navegação global com identidade do produto e barra
    contextual mais leve, separada por borda/sombra mínima, sem grandes blocos decorativos.
12. Em telas estreitas, a navegação global usa botão `Menu` que abre popover ou painel sobreposto a partir da
    barra superior; não recria uma coluna lateral permanente e não reduz a largura do conteúdo.
13. No mobile, ações menos prioritárias entram em overflow; busca, contexto atual e ação principal permanecem
    alcançáveis. Nenhuma faixa pode exigir rolagem horizontal da página.
14. Menus devem fechar por `Escape`, clique externo e seleção, conter foco enquanto abertos e devolver o foco
    ao acionador.
15. Troca de organização continua navegando para `/projects`, invalida dados do tenant anterior e não mantém
    menus/filtros inválidos abertos.
16. A mudança deve preservar deep links, rotas, botões voltar/avançar e os atalhos existentes, incluindo
    `Ctrl/Cmd + K` para busca.
17. `Sidebar.tsx`, o botão de recolher e `sidebar-collapsed` deixam de participar da experiência; dados legados
    no localStorage podem ser ignorados ou removidos defensivamente.
18. Após autenticação, a rota inicial deve abrir o Kanban no último quadro autorizado usado. No primeiro acesso
    ou sem quadro anterior válido, não escolhe automaticamente: exibe `Nenhum quadro selecionado`, ação
    `Selecionar quadro` e, somente com **Administrar quadros**, `Criar quadro`.
19. O seletor superior lista somente quadros vinculados diretamente ao usuário ou às suas equipes, preserva o
    quadro ativo na URL e permite alternância SPA com voltar/avançar.
20. Acesso ao quadro concede acesso derivado aos projetos representados no mesmo nível de permissão do quadro,
    conforme D52; o shell não deve mostrar projetos ou quadros fora desse conjunto.

## Information Architecture

### Faixa global
- Marca/atalho inicial.
- Links globais autorizados.
- Busca global.
- Organização ativa.
- Timer e `Novo item`.
- Notificações e conta.

### Faixa contextual
- Quadro ativo e seletor de quadros no Kanban.
- Breadcrumbs e contexto de projeto quando a rota exigir.
- Navegação entre áreas relacionadas.
- Busca, resumo de filtros, ordenação, agrupamento e acionador do painel de filtros.
- Ações específicas como criar quadro, sprint ou relatório quando autorizadas.

## Interface States

- Carregamento de permissões não deve causar salto excessivo ou mostrar links não autorizados transitoriamente.
- Overflow aberto/fechado.
- Menu mobile aberto/fechado.
- Barra contextual presente/ausente conforme rota.
- Conteúdo ativo e breadcrumbs atualizados após navegação SPA.

## Acceptance Criteria

- **Given** um usuário autorizado no desktop
  **When** a aplicação carrega
  **Then** não existe coluna lateral, o conteúdo ocupa a largura total e todos os destinos antes disponíveis na
  sidebar permanecem alcançáveis pela barra superior.
- **Given** um usuário autenticado com quadros vinculados
  **When** a aplicação termina o login
  **Then** abre o Kanban e o seletor superior mostra apenas seus quadros autorizados.
- **Given** um usuário sem permissão de relatórios/configurações
  **When** a navegação é exibida
  **Then** esses destinos não aparecem no menu principal nem nos overflows.
- **Given** o Kanban aberto
  **When** a barra contextual é renderizada
  **Then** quadro ativo, seletor de quadros, busca, resumo dos filtros, ordenação e ações ficam organizados sem
  duplicação; o painel avançado permanece fechado até ser acionado.
- **Given** uma viewport móvel
  **When** o usuário abre `Menu`
  **Then** encontra os mesmos destinos autorizados numa superfície sobreposta, fecha com `Escape` e o conteúdo
  continua usando toda a largura após fechar.
- **Given** timer ativo e notificações disponíveis
  **When** a largura diminui
  **Then** os recursos permanecem acessíveis por rótulo, ícone com nome acessível ou overflow, sem corte.
- **Given** navegação entre Backlog e Quadro
  **When** a rota muda
  **Then** a barra contextual troca seus controles sem reload e preserva contexto/filtros válidos.

## Test Gate Mapping

- React: renderização por permissões, rota ativa, ausência de sidebar, slots contextuais e overflow.
- React: menu mobile, foco, `Escape`, retorno do foco e ausência de controles duplicados.
- Playwright desktop: navegar por todos os destinos autorizados e validar conteúdo em largura total.
- Playwright mobile: menu superior, busca, criação rápida, organização, notificações e barra contextual.
- Playwright visual/funcional: Kanban e Backlog com filtros na faixa contextual e sem regressão de drag-and-drop.

## Dependencies

- D9/D13 para identidade e frontend.
- D49 para o modal de tarefa.
- D52 para o contexto transversal, acesso e seleção de quadros.
- `SPEC-USER-ACCESS-PERMISSIONS` para perfis, capacidades e visibilidade dos destinos.
- `SPEC-B-001`, `SPEC-SEARCH-SAVED-FILTERS` e `SPEC-BOARDS-STAGES-WIP`.

## Risks

- Sobrecarga da topbar por excesso de links e utilitários.
- Itens autorizados ficarem escondidos somente em determinadas larguras.
- Barra sticky consumir altura demais em notebooks e mobile.
- Duplicação temporária entre controles antigos das páginas e o novo slot contextual.

## Rollback

- Reverter o shell e restaurar `Sidebar` sem alterar rotas ou permissões.
- Manter os componentes de navegação desacoplados durante a transição até os testes E2E aprovarem o novo shell.

## Human Gates

- `G-SPEC`: aprovação desta especificação.
- `G-SCOPE`: aprovação da substituição estrutural da navegação persistente.

## Traceability

CAND-TOP-NAVIGATION-SHELL -> SPEC-TOP-NAVIGATION-SHELL -> TASK-032 -> tests React/Playwright -> commit
