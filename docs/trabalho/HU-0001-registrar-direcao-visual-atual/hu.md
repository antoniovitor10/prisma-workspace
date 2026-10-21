---
id: HU-0001
titulo: "Registrar a direção visual atual (D68)"
trilha: funcionalidade
status: aberta
prd: nao
criado_em: 2026-10-21
atualizado_em: 2026-10-21
---

# HU-0001 · Registrar a direção visual atual (D68)

## História

**Como** mantenedor e time de engenharia do Prisma WorkSpace
**Quero** consolidar a direção visual vigente (D68/D70) no formato do método 0.5.1, gerando o `DESIGN.md` na raiz do repositório e a skill de projeto `metodo/projeto/skills/design-system/SKILL.md` a partir dos tokens reais da aplicação web
**Para que** qualquer nova tela ou componente da aplicação autenticada siga os tokens, tipografia, superfícies, estados e regras de acessibilidade e responsividade sem deriva visual nem "cara de IA".

## Critérios de aceite

### CA-01 · Documento canônico `DESIGN.md` na raiz alinhado à D68 e D70
- **Dado que** a identidade visual oficial do Prisma WorkSpace está definida em `ADR-0068` (D68), `ADR-0070` (D70), `src/Prisma.Workspace.Web/src/styles/theme.ts` e `src/Prisma.Workspace.Web/DESIGN.md`
- **Quando** a direção visual atual for registrada no formato da skill `metodo/projeto/skills/direcao-visual/SKILL.md`
- **Então** o arquivo `DESIGN.md` na raiz do repositório documenta direção estética, referências reais, anti-referências, paleta light-first e modo escuro em tokens semânticos, tipografia (`Inter` e mono), escala de espaçamento, densidade, raio de borda, sombra, movimento e regras de componente das telas da aplicação web.

### CA-02 · Skill de projeto `design-system` criada com tokens reais
- **Dado que** o frontend React 19 utiliza Styled Components com `AppTheme` em `src/Prisma.Workspace.Web/src/styles/theme.ts` e `ThemeMode.tsx`
- **Quando** a skill `metodo/projeto/skills/design-system/SKILL.md` for criada seguindo `metodo/nucleo/skills/criar-skill-projeto/SKILL.md`
- **Então** a skill permanece abaixo de 500 linhas, mapeia os tokens reais de `theme.ts`, proíbe valores soltos (hexadecimais ou pixels fora do tema) nos componentes de produto e referencia os checklists de `anti-cara-de-ia` e `responsivo-desktop-mobile` (1440x900 e 390x844).

### CA-03 · Preservação dos tokens e componentes existentes sem regressão visual
- **Dado que** esta HU registra a direção visual vigente já aprovada pelo PO em D68 e D70
- **Quando** o registro do `DESIGN.md` da raiz e da skill `design-system` for concluído
- **Então** nenhuma cor, fonte ou espaçamento aprovado em `src/Prisma.Workspace.Web/src/styles/theme.ts` é descaracterizado e o gate de tipos (`npx tsc -b`) e testes do frontend seguem íntegros.

## Fora de escopo

- Alterar ou migrar o CSS próprio da landing page institucional em `apps/landing/` (landing explicitamente fora de escopo desta HU).
- Redesenhar telas existentes além da consolidação normativa em `DESIGN.md` na raiz e `metodo/projeto/skills/design-system/SKILL.md`.

## Notas e referências

- **Referência de domínio vigente:** `metodo/projeto/skills/dominio/referencias/prisma-visual-system.md`
- **Decisões relacionadas:** `docs/decisoes/ADR-0068-rebrand-para-prisma-workspace-e-nova-identidade-visual-ofici.md`, `docs/decisoes/ADR-0070-reabertura-da-evolucao-visual-sistemica-do-prisma-workspace.md`, `docs/decisoes/ADR-0103-adocao-do-metodo-de-trabalho.md`
- **Skills de apoio:** `metodo/projeto/skills/direcao-visual/SKILL.md`, `metodo/projeto/skills/anti-cara-de-ia/SKILL.md`, `metodo/projeto/skills/responsivo-desktop-mobile/SKILL.md`
- **Arquivos-fonte atuais:** `src/Prisma.Workspace.Web/src/styles/theme.ts`, `src/Prisma.Workspace.Web/src/styles/ThemeMode.tsx`, `src/Prisma.Workspace.Web/DESIGN.md`
