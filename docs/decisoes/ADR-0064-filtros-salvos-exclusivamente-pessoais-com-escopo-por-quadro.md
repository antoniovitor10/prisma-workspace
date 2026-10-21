---
id: ADR-0064
titulo: "Filtros salvos exclusivamente pessoais com escopo por quadro ou global"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D64"
substitui: ""
---

# ADR-0064 · Filtros salvos exclusivamente pessoais com escopo por quadro ou global

## Decisão

- **D64** — Filtros salvos são exclusivamente pessoais: somente o proprietário pode visualizar, criar, aplicar,
  editar, renomear, duplicar ou excluir, sem opção de compartilhar com quadro, equipe, projeto, organização ou
  outra pessoa. Ao salvar, o usuário escolhe escopo `Quadro atual` ou `Qualquer quadro acessível`. No escopo
  global pessoal, cada critério é revalidado no quadro corrente; critérios específicos incompatíveis são
  ignorados somente naquele quadro, sem impedir os demais, permanecem na definição e voltam a valer em quadros
  compatíveis. A interface mostra aviso e chip identificando cada critério ignorado. Permanecem válidos o botão
  `Aplicar`, a preferência pessoal aberto/fechado do painel e a D60: ordem vertical filtrada é temporária e não
  integra o filtro salvo, enquanto mover tarefa entre colunas continua real. Esta decisão sucede a opção de
  filtro compartilhado antes admitida na D52 e em `SPEC-SEARCH-SAVED-FILTERS`. (2026-08-24)
