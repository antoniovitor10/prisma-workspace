---
id: ADR-0052
titulo: "Quadro operacional transversal por organização com posição única de tarefa"
status: substituida # substituida por ADR-0064, ADR-0072, ADR-0083, ADR-0089
data: 2026-08-24
origem: "DECISIONS.md D52"
substitui: "ADR-0010, ADR-0050"
substituida_por: "ADR-0064, ADR-0072, ADR-0083, ADR-0089"
---

# ADR-0052 · Quadro operacional transversal por organização com posição única de tarefa

## Decisão

- **D52** — Quadro passa a seguir o conceito operacional transversal aprovado a partir da referência oficial do
  Runrun.it: pertence à organização, pode reunir tarefas de projetos diferentes e não é filho funcional de um
  projeto. Cada tarefa mantém uma única posição atual em um quadro/coluna; transferi-la para outro quadro
  preserva integralmente conteúdo e histórico e permite escolher a coluna de destino ou usar a primeira coluna
  aberta. Colunas têm nome e ordem livres, sem nomes/templates obrigatórios, e são classificadas internamente
  como abertas ou concluídas; entrar em concluída conclui a tarefa e voltar para aberta a reabre. O Kanban é a
  tela inicial autenticada, abrindo o último quadro acessado; no primeiro acesso ou se ele não estiver disponível,
  exibe `Nenhum quadro selecionado` com `Selecionar quadro` e, mediante permissão, `Criar quadro`. O seletor
  superior lista os quadros acessíveis. Somente usuários/equipes vinculados visualizam um quadro e veem todas as
  suas tarefas; esse acesso concede aos projetos representados o mesmo nível de permissão do quadro. Criar,
  editar, arquivar quadros e administrar colunas usa a permissão configurável **Administrar quadros**, não papel
  fixo. Arquivar um quadro arquiva também suas tarefas e as oculta das telas normais; somente administradores
  restauram o conjunto. Filtros usam painel lateral contextual recolhível/overlay (sem sidebar global), lembram
  aberto/fechado por usuário e só afetam resultados após `Aplicar`. Após D64, filtros salvos são exclusivamente
  pessoais, com escopo no quadro atual ou em qualquer quadro acessível. A D52 sucede D10/D50 e a cláusula de
  quadros da D36 no que conflita, restringe D22/D45 ao
  modelo legado/compatibilidade quando
  relacionam coluna a workflow de projeto e preserva a D51 quanto à ausência de sidebar global persistente.
  (2026-08-24)
