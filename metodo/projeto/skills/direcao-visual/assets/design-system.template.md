---
name: design-system
description: Regras visuais aprovadas deste projeto (rota <nome>, ADR-XXXX). Leia SEMPRE antes de criar, alterar ou revisar qualquer interface, página, componente, cor, fonte, espaçamento, imagem ou texto de interface deste projeto, no desktop e no mobile.
metadata:
  origem: projeto
  versao: "1"
  gerada_por: direcao-visual
---

# Design system · <projeto>

<!-- Gerado pela skill direcao-visual (passo 7). Copie para metodo/projeto/skills/design-system/SKILL.md,
     preencha os <...> e rode node metodo/nucleo/scripts/gerar-adaptadores.mjs. -->

## Antes de qualquer interface

1. Leia `DESIGN.md` (raiz). Ele manda; esta skill só resume.
2. Abra as capturas aprovadas em `docs/design/aprovado/` para a largura em que vai trabalhar.
3. Toda página nova começa com uma "seção de design" na task: layout no desktop, layout no mobile, componentes usados, citando o `DESIGN.md`.

## Regras

- Rota **<nome>**, aprovada em <data> (ADR-XXXX).
- Só `var(--*)` de `<caminho>/tokens.css`. Nenhum hex, fonte, tamanho, raio ou sombra soltos. Token novo só com motivo no relatório da task.
- Fontes: `--f-display` = <família> (H1, H2); `--f-texto` = <família> (corpo, UI); `--f-dado` = <família> (horários, preços). Nenhuma outra família.
- Assinatura: <o que é>. Aparece em <onde>; nunca em <onde>.
- Mobile obrigatório: <ordem das seções>, hero com H1 em ≤ 3 linhas a 390 px e CTA visível sem rolar, barra fixa <WhatsApp + Ligar> com safe area, alvos ≥ `--alvo-min`.
- Hover só em `@media (hover: hover) and (pointer: fine)`, sempre com `:focus-visible` equivalente.
- Movimento: um momento orquestrado (<qual>); `prefers-reduced-motion` respeitado.
- Voz: <quem fala>. Palavras proibidas: as de `DESIGN.md` → Voz e texto.

## Como verificar

- Antes de concluir uma task de interface: skill `responsivo-desktop-mobile` (capturas nas 5 larguras + Lighthouse) e varredura da `anti-cara-de-ia`.
- Compare as capturas com `docs/design/aprovado/`. Desvio da direção aprovada é falha, não gosto.

## Mudar a direção

Não mude aqui. Rode a skill `direcao-visual` de novo, gere um ADR que substitua o ADR-XXXX e regenere esta skill.
