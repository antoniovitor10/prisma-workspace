---
name: responsivo-desktop-mobile
description: Garante qualidade igual no desktop e no mobile ao implementar ou revisar qualquer página. Cobre composição própria por largura, primeira tela em 1440×900, 1366×768 e 390×844, alvos de toque, barra de ação com WhatsApp e safe area, tipografia fluida, Core Web Vitals, fontes, formulários e reduced motion. Inclui scripts de captura em 5 larguras (com checagens automáticas) e Lighthouse mobile e desktop. Use em toda task de interface antes de concluir, antes de publicar e para capturar style tiles.
license: uso interno do método
metadata:
  origem: catalogo
  versao: "1.0.0"
  revisar_em: "2027-04-01"
---

# Responsivo desktop + mobile

Mobile e desktop têm o mesmo peso. O mobile não é o desktop empilhado: tem composição própria, aprovada no `DESIGN.md`. E nenhuma página é dada como pronta sem evidência nas duas pontas: capturas, números e uma tabela de problemas.

## Quando usar

- Toda task de interface, no passo de verificação (junto com `verificar-antes-de-concluir`).
- Para capturar os style tiles da skill `direcao-visual` (1440 e 390).
- Antes de publicar, e depois de mudar layout, fontes ou imagens do hero.

## Passos

1. **Leia o `DESIGN.md`** (se existir). A composição mobile aprovada é obrigatória; desvio é falha.
2. **Confira o código** contra `metodo/projeto/skills/responsivo-desktop-mobile/references/checklist.md`, seções 3 a 7. Corrija o que for óbvio antes de capturar.
3. **Capture e cheque** nas 5 larguras:
   ```bash
   node metodo/projeto/skills/responsivo-desktop-mobile/scripts/capturar.mjs <url-ou-arquivo> --saida qa/capturas/<nome>
   ```
   Gera a primeira tela e a página inteira de cada largura, `relatorio.md` (rolagem horizontal, alvos < 24 px e abaixo do padrão da casa, campos com fonte < 16 px, H1 na primeira tela e com no máximo 3 linhas no mobile, CTA visível, barra fixa, imagens sem dimensão, lazy-load na primeira tela) e `comparar.html`, que abre no navegador sem servidor.
4. **Olhe as capturas** você mesmo, largura por largura, e compare com `docs/design/aprovado/`. O script não vê composição.
5. **Lighthouse** mobile e desktop (precisa de URL http):
   ```bash
   node metodo/projeto/skills/responsivo-desktop-mobile/scripts/lighthouse.mjs http://localhost:4173/
   ```
   Reporte desempenho, LCP, CLS, TBT e **o elemento LCP** de cada um.
6. **Entregue a tabela** (abaixo) no relatório da task. Nunca "parece ok".

## Formato da entrega

```markdown
### Responsivo · <página> · <data>

Capturas: qa/capturas/<nome>/comparar.html

| Viewport | Problema | Onde | Correção |
|---|---|---|---|
| 390×844 | CTA fora da primeira tela | hero | mover "Agendar pelo WhatsApp" para cima do parágrafo |

| | Desempenho | LCP | CLS | TBT | Elemento LCP |
|---|---|---|---|---|---|
| mobile | | | | | |
| desktop | | | | | |

Metas: LCP ≤ 2,5 s · CLS ≤ 0,1 · TBT ≤ 200 ms · alvos ≥ 24 px (casa: 48 mobile / 44 desktop) · sem rolagem horizontal.
```

## Regras

- Não instale nem baixe dependência sem perguntar. O `capturar.mjs` usa o Playwright do projeto ou o global e, sem ele, gera um `comparar.html` ao vivo (iframes) e avisa. O `lighthouse.mjs` e o `http-server` usam `npx -y`, que baixa o pacote na primeira vez: **pergunte antes do primeiro `npx -y`** da sessão.
- Captura frágil não trava a task: se o navegador ou o servidor falhar, registre o erro, use o `comparar.html` e siga com o que der para conferir. Não tente contornar segurança de navegador ou de portal.
- `qa/` fica fora do git (está no `.gitignore` do método). Capturas que viram referência vão para `docs/design/aprovado/`.
- Servidor de desenvolvimento pesado ou caindo: gere o build e sirva a pasta estática (`npx -y http-server -p 4173 -c-1`).
- Lighthouse é laboratório. Quando o site estiver no ar com tráfego, o dado de campo (PageSpeed Insights) manda.
