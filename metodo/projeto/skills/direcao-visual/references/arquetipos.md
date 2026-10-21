# Arquétipos visuais para clínicas e negócios locais

Ponto de partida para as rotas do passo 4. **Nunca use um arquétipo sem adaptar** ao inventário do cliente: troque pelo menos a assinatura e um eixo de cor ou letra.

Convenções:

- Contrastes pela fórmula de luminância relativa da WCAG, conferidos com `scripts/contraste.mjs` (out/2026). Ao adaptar uma cor, rode o script de novo antes de fechar os tokens. AA: 4,5:1 para texto normal; 3:1 para texto grande (≥ 24 px, ou ≥ 18,66 px em negrito) e componentes.
- Fontes do Google Fonts. Em produção, auto-hospede (WOFF2, subset latin + latin-ext), com no máximo 2 famílias.
- Canal de ação padrão: **WhatsApp** (`wa.me` com mensagem pré-preenchida por serviço). Instagram entra como prova viva (grade curada ou link), nunca como embed pesado no topo.

## Como escolher

Cruze quatro coisas: (a) ticket médio, (b) quem decide (paciente, mãe ou pai, cônjuge), (c) qualidade das fotos disponíveis, (d) vínculo com o lugar.

| Situação | Arquétipos que costumam servir |
|---|---|
| sem fotos boas | A5 Tipográfico, A1 Editorial |
| bairro forte, chega pelo Maps | A6 Bairro |
| ticket alto | A3 Minimal clínico, A7 Luxo discreto |
| criança | A8 Lúdico |
| medo, primeira consulta | A2 Calor humano |
| tradição de 20+ anos | A9 Azulejo/retrô |
| estética natural, integrativa | A4 Herbário |
| especialista que explica | A1 Editorial |

As três rotas de um projeto **não** devem sair de arquétipos vizinhos (A3 e A7, por exemplo, divergem pouco). Combine um arquétipo "seguro" para o nicho, um "de identidade" e um "ousado".

---

## A1. Editorial "revista de saúde"

- **Ideia / quando:** o site é uma edição em que o profissional explica, com legenda, nota e índice. Para especialistas com conteúdo (periodontia, ortodontia adulta, dermatologia), público de 30 a 60 anos que lê antes de agendar.
- **Tipos:** Newsreader (display) + Schibsted Grotesk (texto e UI); números tabulares para horários.
- **Paleta:** papel `#F2F3EF` (fundo frio, **não** creme) · tinta `#16181D` (texto, 15,9:1) · vermelho-cádmio `#B3261E` (CTA; 5,9:1 sobre o papel; branco sobre ele 6,5:1) · grafite `#5B6470` (legendas, 5,4:1) · branco `#FFFFFF`.
- **Foto:** retratos ambientados em luz natural, recorte vertical tipo revista, legendas reais.
- **Desktop:** grid de 12 colunas assimétrico, texto de 60 a 70 caracteres, notas laterais, índice fixo à esquerda nas páginas de tratamento.
- **Mobile:** as notas laterais viram notas expansíveis no texto, o índice vira uma barra de chips horizontal fixa, H1 serifado em no máximo 3 linhas, CTA na barra inferior.
- **Assinatura:** "sumário do tratamento": índice numerado (1ª consulta, exames, etapas, retorno) com tempos reais.
- **Movimento:** sublinhado que cresce no hover e no foco; sem animação de entrada.
- **Evitar:** broadsheet de fios finos com raio zero; fundo creme.
- **Referências:** Clínica Rafaela Salvato (títulos editoriais); revistas de saúde impressas; Fonts In Use (editorial).

## A2. Calor humano / artesanal (sem creme nem terracota)

- **Ideia / quando:** "aqui tem gente": a mão do profissional, a letra dele, a recepção. Para clínica de família, odontologia geral de bairro, fisioterapia, psicologia; público com medo.
- **Tipos:** Bricolage Grotesque (display) + Figtree (texto); Caveat **só** na assinatura manuscrita.
- **Paleta:** branco `#FFFFFF` · goiaba claro `#F6D8CE` (blocos) · café `#2A1E1A` (texto; 16,2:1 no branco, 12,0:1 no goiaba) · verde-garrafa `#2F6B4F` (CTA; branco sobre ele 6,3:1) · amarelo-sol `#F2B705` (só ilustração, nunca texto).
- **Foto:** bastidor e mãos, tons quentes reais, sem sorriso posado.
- **Desktop:** colagem de fotos sobrepostas com legendas manuscritas, blocos de cor alternados.
- **Mobile:** a colagem vira carrossel com a próxima foto aparecendo (15 a 20% visível) e legendas abaixo; "Como é a primeira consulta" vira passo a passo vertical com conector.
- **Assinatura:** "bilhete do profissional": cartão em 1ª pessoa com foto da mão e assinatura.
- **Movimento:** rotação de ±1° nas fotos no hover, desligada em reduced-motion.
- **Evitar:** creme `#F4F1EA` + terracota `#D97757`; ilustração "corporate Memphis".
- **Referências:** Tend (acolhimento de spa); embalagem artesanal de padaria; Bernice Bakery.

## A3. Minimal clínico premium

- **Ideia / quando:** precisão e silêncio; a qualidade aparece no espaço e no detalhe. Para implantes, ortodontia invisível, dermatologia; público A/B que compara três clínicas.
- **Tipos:** Familjen Grotesk (display) + Hanken Grotesk (texto) + IBM Plex Mono (horários, preços, dados).
- **Paleta:** branco `#FFFFFF` · mineral `#EEF1F1` · tinta `#0E1116` (18,9:1) · cinza `#5E636B` (secundário, 6,0:1; não o `#6B7280` do Tailwind) · verde-água profundo `#0F5257` (CTA; branco sobre ele 8,9:1).
- **Foto:** arquitetura do consultório, luz neutra, muito respiro, sem rosto de banco.
- **Desktop:** foto do espaço em 7/12 e texto em 5/12; tabela de tratamentos "duração / sessões / a partir de".
- **Mobile:** foto antes do H1, em 4:5, via `<picture>`; a tabela vira lista de definição (`dl`), nunca rolagem horizontal.
- **Assinatura:** "ficha técnica" em mono com dados reais (equipamento, materiais, tempo de cadeira).
- **Movimento:** transições de 150 a 200 ms; foco com contorno de 2 px na cor do CTA.
- **Evitar:** azul hospitalar, ícone de dente, glassmorphism.
- **Referências:** LAVA dental (contenção); Ader Dent; embalagem farmacêutica suíça.

## A4. Herbário (botânico sem sálvia + creme)

- **Ideia / quando:** catálogo de espécimes em que cada tratamento é uma "prancha" etiquetada. Para estética natural, dermatologia integrativa, nutrição, fisioterapia pélvica; público feminino de 25 a 50 anos.
- **Tipos:** Young Serif (display) + Karla (texto).
- **Paleta:** verde-papel `#E9EEE4` · verde-noite `#1D2A1F` (texto, 12,7:1) · hibisco `#8C2F5A` (CTA; 6,7:1 no fundo; branco sobre ele 7,9:1) · folha `#4C6B3C` (ilustração e fios) · branco `#FFFFFF` (cartões).
- **Foto:** natureza-morta de insumos sobre fundo liso; macro de pele sem retoque excessivo.
- **Desktop:** grade de pranchas com etiqueta (nome, indicação, tempo) e ilustração botânica de traço fino.
- **Mobile:** uma prancha por tela, etiqueta fixa no topo do card, `scroll-snap-type: y proximity`.
- **Assinatura:** etiqueta de espécime (nº, nome popular, "colhido em" = data de atualização).
- **Movimento:** a ilustração se desenha (stroke) uma vez no primeiro viewport.
- **Evitar:** sálvia + creme + serifa fina (variação do cluster creme); folhas PNG de banco.
- **Referências:** pranchas botânicas (Are.na); cosmética natural; Fonts In Use (científico).

## A5. Tipográfico ousado

- **Ideia / quando:** a palavra é a imagem. Para clínica jovem, ortodontia para adolescentes, barbearia, academia, ou cliente ainda sem fotos boas.
- **Tipos:** Archivo variável (eixo de largura: condensado no display, normal no texto). Uma família só.
- **Paleta:** branco `#FFFFFF` · preto `#111111` (~19:1) · vermelho-sinal `#E3261B` (CTA; branco sobre ele 4,6:1, use 18 px+ em negrito) · concreto `#D9D9D6` · amarelo `#FFD400` (marca-texto em 1 ou 2 palavras).
- **Foto:** pouca; quando houver, P&B de alto contraste.
- **Desktop:** H1 em 70 a 80% da largura, quebras de palavra intencionais, grid de 2 colunas desigual.
- **Mobile:** o H1 é **reescrito** para 2 ou 3 palavras por linha (não só reduzido), com `clamp()` até ~15vw; serviços viram uma lista gigante tocável (itens de 56 a 64 px).
- **Assinatura:** marca-texto amarelo em uma palavra-chave por seção.
- **Movimento:** variação do eixo `wdth` no hover.
- **Evitar:** fundo preto + acento único; brutalismo na página inteira.
- **Referências:** sinalização de metrô e tipografia suíça; cartazes; Typewolf (Archivo).

## A6. Bairro / local com fotografia autoral

- **Ideia / quando:** o lugar é a marca: a rua, a fachada, o caminho até a porta. Para clínicas e comércios com vínculo de bairro, público que chega pelo Google Maps.
- **Tipos:** Zilla Slab (display, eco de letreiro) + Albert Sans (texto).
- **Paleta:** branco `#FFFFFF` · grafite `#1C1C1A` (~17:1) · caju-queimado `#B8470F` (CTA; branco sobre ele 5,3:1) · amarelo-caju `#F2B705` (detalhe, sem texto) · verde-mangue `#2E4A3B` (blocos escuros).
- **Foto:** série autoral numa tarde (fachada, rua, recepção, equipe) com um único preset.
- **Desktop:** hero panorâmico da fachada com faixa de informação (endereço, horário, "aberto agora"); mapa estilizado.
- **Mobile:** fachada em recorte vertical (outra imagem via `<picture>`); "Como chegar" logo abaixo do hero (Maps, Waze, WhatsApp); horário em destaque.
- **Assinatura:** "como chegar" em 3 fotos (esquina, porta, recepção).
- **Movimento:** status "aberto agora / fecha às 19h" calculado no navegador.
- **Evitar:** drone genérico da cidade; pin gigante.
- **Referências:** Assembly Specialty Coffee; letreiros locais; fotografia documental de rua.

## A7. Luxo discreto

- **Ideia / quando:** quanto menos se mostra, mais vale. Para harmonização, cirurgia plástica, lentes e facetas premium; ticket alto.
- **Tipos:** Marcellus (display lapidar, com moderação) + Jost (texto).
- **Paleta:** pedra fria `#EDECEA` · carvão `#1E1D1B` (14,3:1) · bordô `#4A1621` (links, fios, CTA secundário; 12,5:1) · superfície escura `#1E1D1B` com texto `#EDECEA` · bronze `#8A6A43` só em detalhe.
- **Foto:** materiais (pedra, linho, madeira), retratos em meia-luz; nada de antes/depois no hero.
- **Desktop:** muito vazio, imagem grande deslocada, texto curto em coluna estreita.
- **Mobile:** o vazio cai ~40% (não some), imagens alternam esquerda e direita em sangria, CTA discreto mas fixo ("Agendar avaliação").
- **Assinatura:** "sala de espera": foto do ambiente e texto sobre tempo de atendimento e privacidade.
- **Movimento:** cross-fade de 400 ms; sem parallax.
- **Evitar:** preto + dourado; Didone sobre creme; mármore de banco.
- **Referências:** Aventura Dental Arts (com cautela, por causa do vídeo); Atelier Dental; hotéis-boutique.

## A8. Lúdico infantil (odontopediatria)

- **Ideia / quando:** consulta como aventura, com informação clara para os pais. Para odontopediatria, pediatria, fono: a criança se encanta, mas quem decide é o adulto.
- **Tipos:** Baloo 2 (display) + Atkinson Hyperlegible (texto).
- **Paleta:** branco `#FFFFFF` · azul-noite `#22223B` (15,4:1) · amarelo `#FFC93C` (CTA com texto azul-noite, 10,1:1) · menta `#A8E6CF` (blocos) · tomate `#E4572E` (só ilustração: branco sobre ele dá 3,7:1).
- **Foto:** crianças reais **com autorização escrita dos responsáveis**, ou mascote próprio com o nome da clínica.
- **Desktop:** cenário ilustrado em faixa horizontal; "Para pais" e "Para crianças" lado a lado.
- **Mobile:** **"Para pais" primeiro** (quem segura o celular é o adulto), um personagem só no hero, CTA de WhatsApp fixo de 56 px.
- **Assinatura:** "diário da primeira visita" em 4 quadrinhos.
- **Movimento:** o personagem acena uma vez no carregamento.
- **Evitar:** arco-íris de 8 cores; Fredoka em tudo; dente sorridente de banco.
- **Referências:** Sakai Kids Dental Clinic; livros infantis ilustrados; embalagens de brinquedo.

## A9. Azulejo / retrô de marca local

- **Ideia / quando:** tradição com orgulho: a clínica, a ótica ou a padaria "de sempre". Para negócios com mais de 20 anos, sucessão familiar, público de 45+.
- **Tipos:** Alfa Slab One (display, títulos curtos) + Libre Franklin (texto).
- **Paleta:** branco `#FFFFFF` · azul-azulejo `#1D4E89` (títulos, CTA; branco sobre ele 8,4:1) · vermelho-esmalte `#C8102E` (branco sobre ele 5,9:1) · tinta `#1A1A1A` · azul-claro `#DCE6F2` (fundos de padrão).
- **Foto:** arquivo da fundação ao lado de fotos atuais com o mesmo enquadramento ("1998 / hoje").
- **Desktop:** faixa de azulejo (SVG leve) como divisor; linha do tempo horizontal.
- **Mobile:** linha do tempo vertical, azulejo só como faixa fina, "então/agora" num slider acessível (`input type=range`).
- **Assinatura:** azulejo próprio desenhado a partir do monograma.
- **Movimento:** o padrão "assenta" peça a peça no primeiro scroll (com reduced-motion respeitado).
- **Evitar:** papel envelhecido, sépia, fonte "western".
- **Referências:** azulejaria luso-brasileira; letreiros esmaltados; UND NY (padaria com tradição).

---

## Fora do nicho de saúde

Os arquétipos servem a outros negócios locais e a produtos com a mesma lógica: A5 e A6 para comércio e serviços de bairro, A1 para escritórios que explicam (advocacia, contabilidade), A3 para produto técnico, A9 para marcas de família. Para um sistema ou painel (app logado), a direção visual é mais contida: escolha um arquétipo pela tipografia e pela cor de ação e gaste a assinatura no estado vazio, no login ou no relatório impresso, nunca na navegação.
