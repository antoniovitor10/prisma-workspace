# SPEC-PRISMA-VISUAL-SYSTEM: Renovação visual sistêmica do Prisma WorkSpace

**Status:** approved

**Aprovação humana:** PO aprovou o G-SPEC em 2026-09-03 e definiu o mockup enviado em
`WhatsApp Image 2026-08-31 at 08.31.02.jpeg` como referência compositiva principal da tela inicial/login.

## Objective

Transformar o Prisma WorkSpace em uma aplicação mais clara, densa e visualmente memorável, começando pelo login e
estendendo um sistema de composição consistente ao shell e às principais superfícies operacionais, sem alterar regras
de negócio, permissões ou contratos de API.

## Context

A D68 aplicou marca, paleta e tipografia, mas a inspeção em 1440x900 e 390x844 mostrou que o login ainda distribui
pouco conteúdo em áreas grandes, deixa a proposta de valor solta e dá ao formulário uma aparência estreita e pouco
integrada. Nas telas autenticadas, cada página define isoladamente largura, cabeçalho, toolbar, cards, estados vazios e
ritmo de espaçamento, produzindo sensação de produto fragmentado e pouco preenchido.

O benchmark privilegia padrões operacionais, não cópia visual:

- Linear: alternância consistente entre lista/quadro, propriedades configuráveis, agrupamento, busca e comandos rápidos
  ([Display options](https://linear.app/docs/display-options), [Search](https://linear.app/docs/search)).
- Jira: uma mesma base de trabalho representada por quadro, lista, timeline, calendário e backlog, com WIP e contexto
  preservados ([Boards overview](https://www.atlassian.com/software/jira/guides/boards/overview)).
- ClickUp: dashboards acionáveis e composição por cards que combinam trabalho, tempo, carga e indicadores
  ([Dashboards](https://clickup.com/features/dashboards)); sua usabilidade também orienta a barra contextual,
  alternância entre visões, agrupamento, filtros progressivos e ações rápidas, sem reproduzir a identidade do produto
  ([Views](https://clickup.com/features/views), [Features](https://clickup.com/features)).
- monday.com: workspace como contêiner para quadros, dashboards e documentos, com visão executiva agregada sem perder
  o detalhe operacional ([Work management](https://support.monday.com/hc/en-us/articles/115005305649-Get-started-with-monday-work-management)).
- Asana: múltiplas visões e dashboards voltados a revelar bloqueios e progresso
  ([Project dashboards](https://help.asana.com/s/article/project-dashboards?language=en_US)).

## Scope

- Evolução dos tokens visuais D68: superfícies, elevação, foco, tipografia, espaçamento, densidade e estados semânticos.
- Novo login responsivo, com composição de marca e contexto real de produto no desktop e formulário dominante no mobile.
- Refinamento do shell superior aprovado em `SPEC-TOP-NAVIGATION-SHELL`, sem reintroduzir sidebar global.
- Primitivas reutilizáveis de página: contêiner, cabeçalho, toolbar, surface/card, métrica, chip, estado vazio e skeleton.
- Aplicação das primitivas às rotas de maior frequência: Kanban, Meu trabalho, Projetos, Relatórios e Configurações.
- Estados claro/escuro, hover, foco, loading, vazio, erro e responsividade.
- Padrões de usabilidade inspirados no ClickUp: controles contextuais compactos, troca de visão previsível,
  agrupamento/ordenação próximos do conteúdo e divulgação progressiva de opções avançadas.

## Out of Scope

- Alterar regras de negócio, RBAC, endpoints, DTOs, schema SQL Server ou navegação aprovada.
- Adicionar módulos, IA, chat, objetivos ou integrações observados nos benchmarks.
- Copiar trade dress, ilustrações ou componentes proprietários de Linear, Jira, ClickUp, monday.com ou Asana.
- Reintroduzir sidebar global; menus laterais contextuais de Configurações continuam dependentes de spec própria.
- Transformar toda tela em dashboard ou preencher espaço com métricas decorativas sem ação associada.

## Functional Requirements

1. O sistema visual deve manter Inter, marca prismática e espectro D68, usando gradiente como assinatura e ação principal,
   não como preenchimento indiscriminado de todas as superfícies.
2. Cada rota principal deve usar a mesma grade, largura adaptativa, cabeçalho, ritmo vertical e hierarquia entre título,
   contexto, ação primária e ações secundárias.
3. Superfícies devem distinguir plano de fundo, painel, card interativo e destaque por contraste e borda; sombra forte fica
   restrita a menus, diálogos e itens elevados.
4. O login desktop deve reproduzir a leitura compositiva do mockup aprovado: marca Prisma discreta no topo esquerdo,
   prisma facetado central dominante, wordmark `PRISMA WorkSpace`, linha espectral, promessa curta e os cinco pilares
   Planeje/Colabore/Acompanhe/Decida/Entregue alinhados no rodapé do painel de marca. A marca antiga do Detran não aparece.
5. O formulário de login deve ocupar uma superfície própria entre 420 e 480 px, preservar fluxos de cadastro,
   confirmação e recuperação e manter a ação principal inequívoca.
6. SSO indisponível não pode competir visualmente com `Entrar`; deve aparecer como opção secundária identificada como
   Cloud/Enterprise ou ser divulgado progressivamente.
7. Em mobile, conteúdo decorativo deve ser reduzido; marca, título, formulário, recuperação e ação primária devem caber
   em uma leitura vertical direta, com touch targets de no mínimo 44 px.
8. A topbar deve preservar destinos e utilitários da spec aprovada, com busca/comando como ponto de entrada claro,
   navegação ativa inequívoca e redução progressiva por largura.
9. Toolbars de lista/quadro devem agrupar busca, filtros, visualização e ordenação numa única superfície contextual,
   mantendo a ação principal separada e visível.
10. Cards e linhas devem mostrar somente metadados úteis à decisão imediata; cor semântica reforça estado, prioridade,
    prazo ou risco, nunca substitui texto ou ícone acessível.
11. Páginas com dados devem priorizar densidade útil: resumo curto e acionável quando houver informação real, seguido
    pela área operacional; não criar KPIs fictícios nem duplicar números já presentes no corpo.
12. Estados vazios devem explicar o contexto e oferecer a próxima ação autorizada. Skeletons devem preservar a geometria
    aproximada da tela carregada e evitar saltos grandes.
13. Modo escuro deve ser uma tradução semântica dos mesmos níveis de superfície, não simples inversão; textos e controles
    precisam manter contraste WCAG AA.
14. Movimentos decorativos devem respeitar `prefers-reduced-motion`; nenhum efeito pode atrasar autenticação ou operação.
15. A fonte web deve ser declarada no `index.html`; o `@import` dentro de `createGlobalStyle`, que hoje gera aviso do
    Styled Components em desenvolvimento, deve ser removido sem mudar a família tipográfica D68.
16. A implementação deve ocorrer em fatias: fundação + login; shell + primitivas; páginas operacionais; QA responsivo.

## Invariants

- React SPA + Styled Components continuam conforme D7/D68.
- Topbar global permanece; sidebar global não retorna.
- Navegação, deep links, permissões, dados e comandos existentes permanecem funcionais.
- Nenhum benchmark autoriza expansão funcional fora das specs aprovadas.

## User Story References

- `US-UX-001` (`stories/US-UX-001.md`)

## Acceptance Criteria

- **Given** viewport desktop de 1440x900
  **When** o login é exibido
  **Then** marca, promessa, contexto visual e formulário compõem uma cena equilibrada sem grandes vazios acidentais

- **Given** viewport de 390x844
  **When** o login é exibido
  **Then** a ação `Entrar` e a recuperação permanecem claras, todos os controles têm ao menos 44 px e não há overflow

- **Given** uma rota autenticada principal
  **When** a tela termina de carregar
  **Then** cabeçalho, toolbar, superfície e estados usam primitivas consistentes e a próxima ação é reconhecível

- **Given** modo claro ou escuro
  **When** navego entre as rotas principais
  **Then** contraste, foco, estado ativo e semântica de cores permanecem equivalentes

- **Given** uma pessoa que prefere movimento reduzido
  **When** usa login, menus e cards
  **Then** animações não essenciais são removidas sem perda de contexto

- **Given** a suíte E2E ativa
  **When** os fluxos de autenticação e navegação forem executados em desktop e mobile
  **Then** todos passam e não há regressão funcional nas rotas modificadas

## Data Impact

Nenhum. Não há alteração de entidade, tabela, migration ou seed.

## Authorization Impact

Nenhum. Visibilidade de ações e destinos continua obedecendo às permissões atuais.

## Contracts

Nenhuma alteração de API ou DTO. Apenas contratos internos de componentes React poderão ser criados.

## Dependencies

- D7, D40, D66, D68, D69 e D70.
- `SPEC-TOP-NAVIGATION-SHELL`, `SPEC-AUTH-001` e specs funcionais das páginas afetadas.
- LP Prisma em `antoniovitordev.com.br/prisma` como referência de marca, não como layout literal do produto.

## Error Cases

- Fonte web indisponível deve cair em `system-ui` sem quebrar layout.
- Falha de autenticação, confirmação ou recuperação mantém mensagem associada ao formulário e foco gerenciável.
- Dados indisponíveis em páginas autenticadas exibem estado de erro existente, sem skeleton infinito.

## Human Gates

- `G-SPEC`: aprovado pelo PO em 2026-09-03.
- Nenhum gate de migration, workflow, histórico ou deploy é exigido por esta refatoração visual local.

## Test Gate Mapping

- Vitest/Testing Library: login em todos os fluxos, primitives, topbar, estados e responsividade lógica.
- Playwright desktop/mobile: login, navegação global, Kanban, Meu trabalho, Projetos, Relatórios e Configurações.
- `npm run build`, `npm run test`, `npm run lint` e `npm run e2e` com ambiente obrigatório da D40.
- Capturas visuais em 1440x900, 1280x720, 768x1024 e 390x844 para QA manual.

## Risks

- Escopo amplo produzir uma troca visual longa; mitigado por fatias verticais verificáveis.
- Aumento de densidade virar ruído; cada resumo precisa apoiar uma decisão ou ação real.
- Componentes legados manterem CSS local divergente; migração deve ser incremental e sem reescrita funcional.
- Testes E2E anteriores ainda não terem sido executados após D68; baseline deve ser estabelecido antes da primeira fatia.

## Rollback

Cada fatia deve ser isolada por componentes/tokens e reversível sem alteração de dados ou API. Se uma página regredir,
reverte-se somente sua adoção das novas primitivas, preservando a fundação já validada.

## Traceability

US-UX-001 → SPEC-PRISMA-VISUAL-SYSTEM → TASK-035/TASK-036/TASK-037 → Vitest/Playwright → homologação

## Implementation Result

Implementada em 2026-09-03. A entrega consolidou tokens e primitivas, refez o login a partir do mockup oficial,
refinou topbar e contexto e aplicou a mesma linguagem a Projetos, Meu trabalho, Kanban, Relatórios e Configurações.
Os fluxos e contratos funcionais foram preservados. A validação final registrou 43/43 testes React, build e lint sem
erros e 35/35 cenários Playwright em desktop/mobile. O runner E2E também passou a ignorar artefatos do Playwright no
watcher do Vite e a reduzir logs SQL, evitando recargas e cancelamentos causados pelo próprio ambiente de teste.
