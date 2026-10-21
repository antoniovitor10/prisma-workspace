# Checklist desktop + mobile

Cada item diz o que conferir e como. "Parece ok" não é resultado: cite a captura, o número ou o seletor.

## 1. Metas numéricas

| Item | Meta | Fonte |
|---|---|---|
| LCP | ≤ 2,5 s no p75 | web.dev, Web Vitals |
| INP | ≤ 200 ms no p75 (no laboratório, use o TBT ≤ 200 ms como aproximação) | web.dev |
| CLS | ≤ 0,1 no p75 | web.dev |
| Avaliação | passa se cumprir as três; mobile e desktop são avaliados **separadamente** | web.dev |
| Alvo de toque mínimo | 24×24 CSS px ou espaçamento equivalente | WCAG 2.2, 2.5.8 (AA) |
| Alvo da casa | **48 px no mobile, 44 px no desktop**, 8 px entre alvos | padrão do método (Apple 44 pt, Material 48 dp) |
| Contraste | 4,5:1 texto normal; 3:1 texto grande e componentes | WCAG 1.4.3 / 1.4.11 |
| Reflow | sem rolagem horizontal a 320 CSS px | WCAG 1.4.10 |
| Foco não encoberto | o elemento focado não fica totalmente escondido por barra fixa | WCAG 2.2, 2.4.11 (AA) |
| Linha de texto | 45 a 75 caracteres (`max-width: 65ch`) | tipografia clássica |

## 2. Larguras de teste

Padrão: **1440×900, 1366×768, 768×1024, 390×844, 360×800**.

Referência do Brasil (StatCounter, set/2026; os números mudam todo mês, confira antes de fixar breakpoints): no mobile, 414×896, 384×832, 412×915, 390×844, 393×873 e 360×800; no desktop, 1920×1080 (~34%), 1366×768 (~13%) e 1536×864 (~10%). Se o público do projeto for muito desktop, acrescente 1920×1080 e 1536×864 em `--viewports`.

## 3. Composição por largura (não empilhar)

- [ ] O mobile segue a composição do `DESIGN.md`, não o desktop empilhado: outra ordem, outro hero, CTA na zona do polegar.
- [ ] `grid-template-areas` diferentes por breakpoint quando a estrutura muda. A ordem visual segue o DOM: reordene no HTML, não só no CSS (leitor de tela e Tab seguem o DOM).
- [ ] Componentes que mudam conforme o espaço usam container queries (`container-type: inline-size; @container (min-width: 40rem)`).
- [ ] Imagens com direção de arte por largura via `<picture>`: desktop 16:9 ou 3:2, mobile 4:5, sempre com `width` e `height`.
- [ ] Tipografia fluida: H1 em `clamp(2rem, 1.2rem + 4vw, 4.5rem)`, corpo em `clamp(1rem, 0.95rem + 0.25vw, 1.125rem)`. `rem` + `vw` mantém o zoom funcionando.
- [ ] Primeira tela em 1440×900, 1366×768 e 390×844: H1 + uma frase + CTA visíveis sem rolar. No mobile, `min-height: 100svh` (não `100vh`).
- [ ] H1 em no máximo 3 linhas a 390 px. Se não couber, **reescreva** o título do mobile; não só diminua a fonte.
- [ ] 768 e 1366: nada quebrado (menu que não cabe, hero cortado, foto distorcida).

## 4. Polegar, barra de ação e navegação

- [ ] Ação principal embaixo, numa barra fixa (ex.: WhatsApp + Ligar) com `padding-bottom: env(safe-area-inset-bottom)` e `<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">`.
- [ ] A página reserva no fim um espaço igual à altura da barra (`--barra-mobile`), para a barra não cobrir conteúdo nem o foco (2.4.11).
- [ ] WhatsApp por link `wa.me` com mensagem pré-preenchida por serviço; nada de plugin flutuante.
- [ ] Desktop: 4 a 6 itens de menu + CTA. Mobile: 3 ou 4 destinos, painel em tela cheia com links de 48 px, botão rotulado "Menu".
- [ ] Hover só em `@media (hover: hover) and (pointer: fine)`, sempre com `:focus-visible` equivalente.
- [ ] Marque o CTA principal com `data-cta` para a checagem automática achar.

## 5. Desempenho real (4G, Android médio)

- [ ] Imagem do hero (LCP): AVIF ou WebP, `fetchpriority="high"`, **sem** `loading="lazy"`, dimensões explícitas. As demais com `loading="lazy"`.
- [ ] WordPress/LiteSpeed: excluir o hero do lazy-load do LSCache, gerar WebP/AVIF, conferir que o CSS crítico não esconde o LCP.
- [ ] Fontes: no máximo 2 famílias e 3 ou 4 arquivos; WOFF2 auto-hospedado com subset latin + latin-ext (acentos); `font-display: swap` (ou `optional` no display); `<link rel="preload" as="font" type="font/woff2" crossorigin>` só para a fonte do H1; `size-adjust`/`ascent-override` na fonte de reserva para reduzir CLS.
- [ ] Vídeo no hero: evitar no mobile; se houver, o poster é o LCP.
- [ ] INP: sem slider pesado nem chat de terceiros no carregamento.
- [ ] `@media (prefers-reduced-motion: reduce)` desliga parallax, autoplay e animações de entrada.

## 6. Formulários

- [ ] Rótulo visível sempre (placeholder não é rótulo); erro específico ("Informe o DDD").
- [ ] `inputmode="tel"` + `autocomplete="tel"`; `autocomplete="name"`, `email`, `postal-code` (CEP com `inputmode="numeric"`).
- [ ] Fonte dos campos ≥ 16 px no mobile (abaixo disso o iOS dá zoom).
- [ ] Em negócio local, o formulário costuma perder para o WhatsApp: no máximo 3 campos, como alternativa ("prefere que a gente ligue?").
- [ ] Formulário com dado pessoal: task de risco alto (revisor de segurança), consentimento e finalidade claros.

## 7. Acessibilidade mínima

- [ ] Um `h1` por página, títulos em ordem.
- [ ] Todo controle alcançável por Tab, com foco visível.
- [ ] `alt` descritivo nas imagens que informam; `alt=""` nas decorativas.
- [ ] `lang="pt-BR"` no `<html>`.
- [ ] Zoom de 200% sem perder conteúdo; sem rolagem horizontal a 320 px.

## 8. Comandos

```bash
# capturas + checagens (arquivo local ou URL)
node metodo/projeto/skills/responsivo-desktop-mobile/scripts/capturar.mjs http://localhost:4173/ --saida qa/capturas/home

# Lighthouse mobile e desktop (precisa de http)
npx -y http-server -p 4173 -c-1          # em outro terminal, se o projeto não tiver servidor próprio
node metodo/projeto/skills/responsivo-desktop-mobile/scripts/lighthouse.mjs http://localhost:4173/
```

Os comandos com `npx -y` baixam o pacote na primeira vez: pergunte ao desenvolvedor antes.

Complementos: PageSpeed Insights (campo + laboratório) quando o site estiver no ar; WebPageTest com perfil Android médio em 4G para ver a sequência de quadros e o LCP real; axe para acessibilidade.

**Servidor local leve:** se o servidor de desenvolvimento for pesado ou cair por falta de memória, gere o build e sirva a pasta estática com `http-server` (ou `python -m http.server`). Para capturar um arquivo HTML estático, nem precisa de servidor.
