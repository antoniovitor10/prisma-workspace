---
id: ADR-0088
titulo: "Navegação global no cabeçalho e painel lateral restrito ao contexto do projeto"
status: aceita
data: 2026-09-10
origem: "DECISIONS.md D88"
substitui: "ADR-0087"
---

# ADR-0088 · Navegação global no cabeçalho e painel lateral restrito ao contexto do projeto

## Decisão

- **D88** — Navegação global volta ao cabeçalho; lateral vira contexto do projeto. O PO pediu em 2026-09-10
  mover os itens do trilho (Início, Meu trabalho, Projetos, etc.) para o header e colocar no lugar o bloco do
  projeto (nome, chave, descrição e abas Itens/Backlog/Sprints/Kanban/Relatórios/Wiki/Configurações), visível
  **somente** dentro de `/projects/:projectId`. Isso **sucede a D87** quanto ao destino da navegação global:
  o trilho de ícones deixa de ser a casa da navegação principal. A barra superior recupera os links globais
  (desktop) e o hambúrguer continua sendo a superfície mobile. A lateral deixa de ser navegação global
  persistente e passa a ser **painel contextual do projeto**; fora do projeto ela não aparece. Em telas
  estreitas (≤768px) o painel do projeto não aparece — as abas do projeto ficam em faixa horizontal no
  workspace, para não competir com o hambúrguer. `layout/navigation.ts` permanece a fonte única dos itens
  globais e dos destinos do projeto. Identidade visual Prisma (D68 / SPEC-PRISMA-VISUAL-SYSTEM) intacta;
  não copiar visual de outro produto. (2026-09-10)
