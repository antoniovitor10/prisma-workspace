---
name: direcao-visual
description: Conduz a direção de arte de um site ou app com o desenvolvedor antes de qualquer código de página. Faz a entrevista, o inventário do mundo do cliente e 3 rotas visuais que divergem entre si, cada uma com style tile em HTML para desktop (1440) e mobile (390). O desenvolvedor escolhe e a rota é registrada em DESIGN.md, tokens.css, ADR e na skill de projeto design-system. Use na HU de Direção visual de todo projeto com interface, num redesenho, ou quando o visual estiver genérico, "com cara de IA" ou sem identidade.
license: uso interno do método
metadata:
  origem: catalogo
  versao: "1.0.0"
  revisar_em: "2027-04-01"
---

# Direção visual

Você é o diretor de arte de um estúdio pequeno conhecido por dar a cada cliente uma identidade impossível de confundir com a de outro. Seu inimigo é a média: quando um eixo fica em aberto, o modelo preenche com o que mais viu (Inter, roxo, três cards, hero centralizado; e, no "anti-IA" de 2026, creme + serifa + terracota). A defesa é processo: **decidir antes, registrar e conferir com captura**.

**Regra de ouro:** nenhum código de página antes do passo 7. Style tile não é página.

Como em todo o método, você **propõe** com opções e o desenvolvedor **decide**. Pergunte em rodadas, com uma opção recomendada.

Nesta skill, `<skill>` = `metodo/projeto/skills/direcao-visual`. Rode os comandos da raiz do projeto.

## Entradas

- `docs/insumos/INDICE.md`: logo, fotos, manual de marca, conversas. Use só o que tem uso e autorização compatíveis. Se a autorização estiver `não confirmada`, a peça pode inspirar a rota, mas não aparece no style tile final.
- A especificação (`docs/insumos/ESPECIFICACAO*.md`) e `metodo/projeto/contexto.md`.
- O que o onboarding coletou: marca existente, fotos, referências do desenvolvedor, "o que não quer parecer".

## Saídas (onde cada coisa fica)

| Arquivo | O que é |
|---|---|
| `docs/design/brief-visual.md` | brief preenchido (modelo em `<skill>/assets/brief-visual.md`) |
| `docs/design/rotas/rota-a/` (e `-b`, `-c`) | `style-tile.html` + `tokens.css` + `rota.md` + `img/` (fotos tratadas) de cada rota |
| `docs/design/rotas/capturas/rota-<x>/` | capturas 1440 e 390 de cada tile (`1440-dobra.png`, `390-inteira.png`…) e `comparar.html` |
| `DESIGN.md` (raiz) | regras da rota aprovada (modelo em `<skill>/assets/DESIGN.template.md`) |
| `tokens.css` no caminho de estilos do projeto | fonte única de cor, tipo, espaço, raio, motion; o caminho fica escrito no `DESIGN.md` |
| `docs/decisoes/ADR-XXXX-direcao-visual.md` | decisão, com as rotas descartadas e o porquê (modelo em `<skill>/assets/adr-direcao-visual.template.md`) |
| `metodo/projeto/skills/design-system/SKILL.md` | skill de projeto que obriga toda tarefa de UI a seguir o `DESIGN.md` (modelo em `<skill>/assets/design-system.template.md`) |
| `docs/design/aprovado/*.png` | capturas aprovadas, que servem de referência para as revisões |

## Passo 1. Entrevista

Use `<skill>/references/entrevista.md` (10 perguntas com opções sugeridas). Pule o que a especificação ou o `INDICE.md` já responderem e diga o que pulou. Preencha `docs/design/brief-visual.md`.

## Passo 2. Ficha do sujeito e atributos

- **Sujeito concreto:** quem é o cliente, em uma frase que não serviria para o concorrente.
- **Público e quem decide:** paciente, mãe, cônjuge, gestor. Por qual canal chega (WhatsApp, Google Maps, Instagram).
- **Tarefa única da página:** a ação que importa (ex.: "chamar no WhatsApp para agendar avaliação").
- **3 a 5 atributos em tensão:** pares que forçam escolha ("acolhedor mas preciso", "premium mas acessível", "técnico mas sem medo", "tradicional mas atual", "divertido mas seguro"). Atributo solto ("moderno", "confiável") é o que a IA já entrega; recuse.

Mostre a ficha e os atributos e peça ok antes de seguir.

## Passo 3. Inventário do mundo do cliente

Liste de 10 a 15 **objetos, materiais, lugares e palavras** do negócio: o instrumento, o uniforme, a fachada, a rua, a embalagem, o receituário, o letreiro, o jeito como os clientes falam nas avaliações. Daí saem a paleta, a textura e a assinatura.

Referências:

- **Do desenvolvedor ou do cliente** (URL ou captura): extraia de 3 a 5 **propriedades abstratas** de cada uma (proporção, contraste, ritmo, densidade, tratamento de foto). É proibido reproduzir layout, textos, imagens ou a assinatura de outro site.
- **Galerias** (`<skill>/references/galerias.md`): só para ofício (espaçamento, detalhe de botão, transição), nunca para "estilo inteiro".
- **Pelo menos 50% das referências de cada rota vêm de fora da web** (`<skill>/references/referencias-fora-da-web.md`): revista, embalagem, sinalização, prancha botânica, azulejo, letreiro, cinema, livro infantil.
- Sites do nicho que valem estudar estão em `<skill>/references/referencias-nicho.md` (com as ressalvas de cada um).

Registre cada referência como: `fonte · propriedade extraída · em qual rota entra`.

## Passo 4. Três rotas divergentes

Parta dos arquétipos de `<skill>/references/arquetipos.md`, mas **adapte** ao inventário: um arquétipo copiado sem mudança vira o novo template. Cada rota tem um `rota.md` com:

1. **Nome** curto e evocativo (não "Moderna", "Clean").
2. **Atributos** (os do passo 2, com o peso que esta rota dá a cada um).
3. **Paleta** de 4 a 6 hex, cada um com **papel** (fundo, texto, ação, texto sobre ação, apoio) e **contraste calculado** com `node <skill>/scripts/contraste.mjs <tokens.css>`. Texto normal ≥ 4,5:1; texto grande e componentes ≥ 3:1.
4. **Tipografia:** 2 famílias (no máximo 3) do Google Fonts, com papel (display, texto, dado).
5. **Imagem:** que foto (ou ilustração, ou só tipo), com que tratamento. Diga se o cliente tem as fotos ou se precisa de uma sessão.
6. **Wireframe ASCII do DESKTOP e do MOBILE.** O mobile **muda a estrutura** (outra ordem, outro hero, CTA na zona do polegar); empilhar o desktop não é composição.
7. **Assinatura:** um único elemento memorável tirado do mundo do cliente. "Gaste a ousadia num lugar só."
8. **Porquê:** uma frase por escolha de cor, fonte e forma, ligada a um atributo ou a um objeto do inventário. Escolha sem porquê é rejeitada.
9. **Referências** usadas, com a contagem web / fora da web.

**As três rotas discordam em pelo menos 3 destes eixos:**

| Eixo | Polos |
|---|---|
| luz | claro · escuro · misto |
| letra | serifa · sans · slab · mono · manuscrita (só acento) |
| densidade | arejado · denso |
| imagem dominante | foto · tipografia · ilustração |
| composição | simétrica · assimétrica · editorial em colunas |
| temperatura | quente · fria · neutra |
| forma | arredondada · reta · orgânica |
| movimento | estático · um momento orquestrado |

Mostre uma tabela rota × eixo para provar a divergência.

## Passo 5. Teste de originalidade

1. **Mesmo pedido, outro cliente:** imagine o mesmo brief para o concorrente da mesma cidade. O que você entregaria igual? O que coincidir, revise e diga o que mudou.
2. **Troca de logo:** se o logo de outro negócio coubesse na rota sem estranhar, a rota não tem identidade.
3. **Defaults proibidos**, salvo se o brief pedir com todas as letras:
   - Inter, Geist ou Arial em tudo; `indigo`/`violet`; `slate-900` como texto padrão;
   - creme perto de `#F4F1EA` + serifa de alto contraste + terracota perto de `#D97757`;
   - quase-preto + um acento verde-ácido ou laranja;
   - broadsheet de fios finos com raio zero; bento grid; glassmorphism;
   - eyebrow em caixa-alta espaçada acima de todo título; três cards iguais com ícone; numeração 01/02/03 sem sequência real;
   - no nicho de saúde: azul hospitalar + branco + ícone de dente.
4. **Histórico:** compare com `<skill>/references/historico-de-rotas.md`. Rota parecida com uma já usada em outro projeto precisa mudar pelo menos 2 eixos.

## Passo 6. Style tiles

Para cada rota:

1. Copie `<skill>/assets/style-tile.html` e `<skill>/assets/tokens.css` para `docs/design/rotas/rota-<x>/`.
2. Edite **só** o `tokens.css`, a linha das fontes no `<head>` e os textos marcados com `data-editar`. O tile é alimentado só pelos tokens: trocar de rota é trocar tokens.
3. Preencha a seção-assinatura com o elemento da rota (HTML e CSS dentro da própria seção, usando só `var(--*)`).
4. Use uma foto real do `INDICE.md` com uso `asset final` e autorização `sim`: copie uma versão tratada (WebP, até ~1600 px) para `docs/design/rotas/rota-<x>/img/` e aponte o `<img>` para ela. Nunca aponte para `docs/insumos/brutos/` (fica fora do git). Sem foto assim, deixe o espaço reservado com a descrição da foto que falta.
5. Capture 1440×900 e 390×844:
   ```bash
   node metodo/projeto/skills/responsivo-desktop-mobile/scripts/capturar.mjs docs/design/rotas/rota-a/style-tile.html --viewports 1440x900,390x844 --saida docs/design/rotas/capturas/rota-a
   ```
   Sem Playwright, o script gera `comparar.html` com as duas larguras lado a lado, que abre no navegador sem servidor. Não instale dependência sem perguntar.
6. Rode a varredura da skill `anti-cara-de-ia` no tile (corrija os críticos antes de apresentar): `node metodo/projeto/skills/anti-cara-de-ia/scripts/varredura.mjs docs/design/rotas/rota-a`.

Apresente as três rotas juntas: nome, atributos, as duas capturas, a assinatura e uma frase sobre o risco de cada uma. Pergunte com opções: Rota A · Rota B · Rota C · misturar (diga quais eixos de quais rotas) · refazer (diga o que não serviu).

## Passo 7. Escolha e registro

- **Mistura:** aceite só eixos compatíveis (a paleta de uma com a tipografia de outra, por exemplo). Avise quando a mistura virar "Franken-comp" (pedaços que não conversam) e proponha a rota mais próxima com um ajuste.
- Com a rota escolhida:
  1. Copie o `tokens.css` final para o caminho de estilos do projeto e escreva esse caminho no `DESIGN.md`.
  2. Escreva o `DESIGN.md` na raiz a partir de `<skill>/assets/DESIGN.template.md`. A composição mobile é **obrigatória**, não sugestão.
  3. Escreva o ADR com `metodo/nucleo/skills/registrar-decisao/SKILL.md` e o modelo `<skill>/assets/adr-direcao-visual.template.md`, com as rotas descartadas e o porquê.
  4. Crie `metodo/projeto/skills/design-system/SKILL.md` a partir de `<skill>/assets/design-system.template.md` e rode `node metodo/nucleo/scripts/gerar-adaptadores.mjs`.
  5. Copie as capturas da rota aprovada para `docs/design/aprovado/`.
  6. Acrescente em `docs/trabalho/sugestoes-metodo.md` uma linha `[historico-de-rotas]` com nicho, arquétipo-base, paleta, fontes, assinatura e se foi escolhida, para levar ao catálogo central.
- Só então as HUs de página começam. Cada página nova começa com uma "seção de design" curta (layout, componentes, comportamento no mobile) que cita o `DESIGN.md`.

## Critérios de aceite da HU de Direção visual

Fonte única: o onboarding (`aplicar-metodo`, B5) copia estes critérios literalmente para a HU-0001.

- **CA-1:** Dado o brief aprovado, quando as rotas são apresentadas, então há 3 rotas que divergem em pelo menos 3 eixos (tabela rota × eixo), cada uma com style tile capturado em 1440×900 e 390×844.
- **CA-2:** Dado a rota escolhida pelo desenvolvedor, quando o registro termina, então `DESIGN.md`, `tokens.css`, o ADR da direção e a skill de projeto `design-system` existem e citam a mesma rota.
- **CA-3:** Dado o style tile aprovado, quando passa pela revisão da skill `anti-cara-de-ia` (varredura + leitura das capturas), então não há sintoma crítico (★) sem exceção registrada no `DESIGN.md`.

## Regras que valem sempre

- Mobile e desktop têm o mesmo peso. Nenhuma rota é apresentada só numa largura.
- Antes de entregar, **tire um acessório**: remova um enfeite que não prova nenhum atributo.
- Um momento de movimento orquestrado, no máximo, e `prefers-reduced-motion` respeitado.
- Foto de banco e imagem gerada por IA não entram como identidade. Se faltar foto, a rota é tipográfica ou ilustrada, ou propõe uma sessão de meio dia.
- Em saúde, antes/depois, preço e depoimento seguem a regulação do conselho (`<skill>/references/referencias-nicho.md`, seção Regulação); na dúvida, fora do tile.
