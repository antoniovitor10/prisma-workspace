# DESIGN.md · <projeto>

<!-- Gerado pela skill direcao-visual no passo 7. Regras da rota APROVADA.
     Toda tarefa de interface lê este arquivo antes (skill de projeto design-system).
     Só muda com um novo ADR de direção visual. -->

- **Rota aprovada:** <nome> · <data> · ADR-XXXX
- **Tokens:** `<caminho>/tokens.css`. Na página e nos componentes, só `var(--*)`; nenhum hex, fonte ou tamanho solto.
- **Capturas aprovadas:** `docs/design/aprovado/` (1440×900 e 390×844)

## Atributos

1. … mas …
2. … mas …
3. … mas …

## Tese

<A ideia central em uma frase. O hero prova esta frase.>

## Cor

| Papel | Token | Hex | Contraste | Uso | Nunca |
|---|---|---|---|---|---|
| fundo | `--c-fundo` | | — | página | |
| texto | `--c-texto` | | x:1 sobre o fundo | corpo e títulos | |
| ação | `--c-acao` | | x:1 sobre o fundo | botão principal, links | em texto corrido longo |
| texto sobre ação | `--c-acao-texto` | | x:1 | | |
| acento | `--c-acento` | | | só na assinatura | em texto |

## Tipografia

| Papel | Família | Pesos | Token | Onde |
|---|---|---|---|---|
| display | | | `--f-display` | H1, H2 |
| texto | | | `--f-texto` | corpo, UI |
| dado | | | `--f-dado` | horários, preços, números |

Escala: `--t-h1`, `--t-h2`, `--t-h3`, `--t-corpo`, `--t-pequeno` (fluida, em `clamp()`). Texto corrido com no máximo `--medida`.

## Espaço, forma e profundidade

- Raio: `--raio` (<valor>). Sombra: `--sombra` (<uma camada ou nenhuma>).
- Espaço entre seções: `--espaco-secao`. Margem lateral: `--margem-pagina`.

## Imagem

- O que fotografar: …
- Tratamento (um preset só): …
- Proibido: foto de banco, imagem gerada por IA como identidade, antes/depois fora das regras do conselho.
- Proporções: mobile 4:5, desktop 3:2 ou 16:9, via `<picture>` com `width` e `height`.

## Assinatura

- O que é: …
- Onde aparece: …
- Onde **não** aparece: …

## Composição no desktop

```
(wireframe ASCII aprovado)
```

## Composição no mobile (obrigatória)

```
(wireframe ASCII aprovado; não é o desktop empilhado)
```

- Ordem das seções no mobile: …
- Hero: H1 em ≤ 3 linhas a 390 px, CTA visível sem rolar.
- Barra fixa inferior: <WhatsApp + Ligar>, altura `--barra-mobile`, com safe area; a página reserva esse espaço no fim.
- Menu: 3 ou 4 destinos + ação; alvos de `--alvo-min`.

## Componentes

- Botão: estados repouso, hover (só em `@media (hover: hover)`), foco visível, ativo, desabilitado.
- Card de serviço: …
- Formulário: rótulo visível, `inputmode` e `autocomplete` certos, fonte ≥ 16 px nos campos.

## Movimento

- Um momento orquestrado: … · Duração `--dur`, curva `--curva` · `prefers-reduced-motion` zera `--dur`.

## Voz e texto

- Quem fala: <o profissional, na 1ª pessoa / a equipe>.
- Faça: fatos concretos, verbo exato nos botões ("Agendar pelo WhatsApp").
- Palavras proibidas: transforme, eleve, solução completa, sua jornada, excelência, humanizado, de ponta, mais que um sorriso, … (lista completa na skill anti-cara-de-ia).

## Faça / não faça

| Faça | Não faça |
|---|---|
| | |

## Exceções à varredura

<!-- Padrões que a varredura da skill anti-cara-de-ia acusaria, mas que esta rota usa de propósito, com o motivo.
     Formato obrigatório (o script lê): - `padrão`: motivo -->
- (nenhuma)
