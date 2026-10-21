---
id: ADR-0087
titulo: "Navegação global em trilho lateral recolhível de ícones"
status: substituida # substituida por ADR-0088
data: 2026-09-08
origem: "DECISIONS.md D87"
substitui: ""
substituida_por: "ADR-0088"
---

# ADR-0087 · Navegação global em trilho lateral recolhível de ícones

## Decisão

- **D87** — Navegação lateral recolhível volta, ao lado da barra superior. O dev Sergio pediu em 2026-09-07,
  textualmente, *"ao invés de abas menu tipo sanduíche"*, e o PO decidiu em 2026-09-08: *"mantem o que o sergio
  decidiu mas algo que abre de tamanho como o ClickUp"*. Isso **sucede parcialmente** a
  `SPEC-TOP-NAVIGATION-SHELL`, que proibia sidebar: a navegação principal **sai das abas da barra superior** e
  passa a ser um trilho de ícones à esquerda que expande para mostrar rótulos — era literalmente o pedido do
  Sergio, *"ao invés de abas"*. A barra superior mantém busca, seletor de organização, criação rápida e conta.
  O que continua proibido é **copiar a identidade visual do ClickUp**: a
  `SPEC-PRISMA-VISUAL-SYSTEM` permanece intacta, e o que se adota é o *comportamento* de trilho expansível, não
  cores, tipografia ou ícones de outro produto. O estado recolhido ou expandido é preferência por pessoa,
  guardada no navegador. Em telas estreitas (até 768px) o trilho não aparece: quem navega ali é o menu
  hambúrguer que a barra superior já tem. Isso corrige a intenção registrada antes da implementação, de abrir o
  trilho sobreposto no mobile — seriam duas navegações sobrepostas concorrentes na mesma tela, e o hambúrguer já
  resolvia o caso com foco e Escape testados.
  Os itens e seus portões de permissão são definidos em um único lugar (`layout/navigation.ts`) e consumidos
  pelo trilho e pelo hambúrguer, para que não exista navegação divergente entre as duas superfícies. (2026-09-08, implementada em 2026-09-09)
