# Prisma WorkSpace — landing page

Landing page estática dark-first para o Prisma WorkSpace, construída com HTML5, CSS moderno e JavaScript puro, com animações orquestradas via GSAP + ScrollTrigger (CDN).

Conceito visual: refração. Um feixe de luz atravessa o prisma no hero e se decompõe em cinco fluxos coloridos — Planeja, Colabora, Acompanha, Decide, Entrega.

## Executar localmente

Inicie um servidor estático na pasta do projeto:

```powershell
python -m http.server 8000
```

Depois acesse `http://localhost:8000`.

## Estrutura

- `index.html` — conteúdo, semântica e SVG do prisma
- `css/styles.css` — tokens de design, tipografia (Bricolage Grotesque / Inter / JetBrains Mono), seções e responsividade
- `js/main.js` — sequência de abertura do hero, reveals por scroll, scrollspy do trilho de recursos, menu mobile e barra de progresso espectral
- `assets/favicon.svg` — favicon vetorial

## Pendências (aguardando definições do produto)

- Links reais de download, Cloud, GitHub e redes: `href="#"` marcados com `TODO` no HTML
- Logo oficial: o wordmark atual é uma recriação neutra em SVG, a refinar com a referência da marca
