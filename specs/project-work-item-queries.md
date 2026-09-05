# SPEC-PROJECT-WORK-ITEM-QUERIES: Visões segmentadas e consultas por projeto

**Status:** approved

**Aprovação humana:** PO aprovou G-SCOPE e G-SPEC em 2026-09-03 e delegou a definição do formato da primeira entrega.
O recorte aprovado é a opção A: compositor visual, consultas pessoais salvas e estado não sensível serializado na URL.

## Objective

Definir uma superfície por projeto para segmentar itens de trabalho por tipo e compor consultas reutilizáveis,
preservando hierarquia, autorização, terminologia canônica e a política de filtros pessoais já aprovada.

## Context

O pedido humano inclui visões específicas para `Epic`, `Bug`, `Feature`, `Product Backlog Item` e `Task`, com suas
características, e a geração de queries por projeto. O produto já possui tipos de `WorkItem`, Product Backlog em árvore
e filtros salvos pessoais, mas não há contrato aprovado para uma central de consultas por projeto nem decisão sobre
uma linguagem textual de consulta.

Como referência de interação, o ClickUp combina troca de visão, agrupamento, filtros e propriedades próximas do
conteúdo. O Prisma deve aproveitar esses padrões de usabilidade sem copiar trade dress ou introduzir opções sem regra.

## Scope aprovado

- Entrada `Itens` ou equivalente dentro do contexto de um projeto.
- Segmentos `Todos`, `Épicos`, `Bugs`, `Features`, `Product Backlog Items` e `Tarefas`, com contagem autorizada.
- Colunas/propriedades configuradas por tipo, usando apenas campos existentes e aprovados.
- Compositor visual com busca, condições, grupos lógicos, ordenação, agrupamento e prévia da quantidade de resultados.
- Execução sempre limitada ao projeto atual e aos itens que a pessoa pode visualizar.
- Reutilização da propriedade exclusivamente pessoal de filtros definida em `SPEC-SEARCH-SAVED-FILTERS`.
- URL serializável para estado não sensível e navegação SPA com voltar/avançar.
- Estados vazio, carregando, erro, critério incompatível e resultado sem acesso.

## Fora do escopo

- SQL livre ou acesso direto ao banco.
- Consultas que atravessem organizações ou ampliem permissões.
- Filtros compartilhados, conforme decisão vigente de `SPEC-SEARCH-SAVED-FILTERS`.
- Criar novos tipos ou alterar a hierarquia D36 antes de decisão específica.
- Implementar uma linguagem textual, API pública ou exportação antes da decisão de escopo abaixo.

## Regras

1. Todo resultado pertence ao projeto atual e passa pela autorização normal do `WorkItem`.
2. Selecionar um segmento aplica o tipo como critério explícito e mantém os demais critérios compatíveis.
3. Propriedades indisponíveis para um tipo devem ser identificadas e ignoradas ou bloqueadas de forma explicável.
4. A hierarquia continua obedecendo à D36; uma consulta não cria nem reclassifica itens.
5. Salvar uma consulta cria uma definição pessoal, nunca um artefato compartilhado.
6. Alterações de rascunho só mudam resultados após `Aplicar`, conforme a spec de filtros.
7. A interface deve priorizar controles compactos e progressivos, preservando uso por teclado e mobile.
8. Cards de segmento clicáveis devem manter borda, indicador lateral, foco e conteúdo dentro do próprio raio,
   sem sobreposição visual ou recorte indevido no estado selecionado.

## Decisão humana — G-SCOPE aprovado

Na primeira entrega, “geração de query” significa um compositor visual que produz uma definição JSON interna,
executada somente pela interface, salva como filtro pessoal e representada na URL. Não haverá SQL livre, linguagem
textual editável, endpoint público ou exportação. Para evitar migration, a persistência reutiliza `SavedFilter` no
quadro padrão — ou primeiro quadro — do projeto como âncora técnica, com `scope: project-items-v1`; a interface de
Kanban não apresenta definições desse escopo.

## Acceptance Criteria

- **Given** um projeto com itens de todos os tipos suportados
  **When** a pessoa alterna o segmento
  **Then** a lista e a contagem exibem somente o tipo escolhido sem perder o projeto atual

- **Given** uma combinação válida de critérios
  **When** a pessoa aplica a consulta
  **Then** os resultados autorizados são filtrados, ordenados e agrupados conforme a definição

- **Given** um critério incompatível com o segmento selecionado
  **When** a consulta é validada
  **Then** a interface explica o conflito e não executa uma interpretação silenciosa

- **Given** uma consulta pessoal salva
  **When** outro usuário acessa o mesmo projeto
  **Then** não visualiza nem administra essa consulta

## Data/API Impact

Sem mudança de schema e sem endpoint novo. A entrega reutiliza os endpoints pessoais de `SavedFilter` já existentes,
ancorados em um quadro do projeto. Projetos sem quadro permitem executar e compartilhar a URL, mas explicam que salvar
a consulta exige ao menos um quadro. Linguagem textual ou uso externo exigirão specs e contratos próprios.

## Dependencies

- D36, D52, D65 e D67.
- `SPEC-WORK-ITEM-MANAGEMENT`, `SPEC-B-001` e `SPEC-SEARCH-SAVED-FILTERS`.
- `SPEC-PRISMA-VISUAL-SYSTEM` apenas para padrões de composição e interação.

## Human Gates

- `G-SCOPE`: aprovado pelo PO em 2026-09-03 para a opção A.
- `G-SPEC`: aprovado pelo PO em 2026-09-03.
- `G-MIGRATION`: não aplicável; não há persistência nova.

## Traceability

US-PROJECT-QUERIES-001 → SPEC-PROJECT-WORK-ITEM-QUERIES → TASK-038 → implementação publicada em 2026-09-03;
ajuste visual do estado selecionado solicitado pelo PO em 2026-09-04.
