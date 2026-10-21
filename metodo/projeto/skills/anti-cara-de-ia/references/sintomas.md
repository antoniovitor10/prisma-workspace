# Sintomas de "cara de IA" (2026)

★ = **crítico**: sozinho já denuncia, ou tem risco legal. Revisão com ★ não aprovada vira "aprovado com correções" no máximo.
Onde: **D** desktop · **M** mobile · **D/M** ambos.

## De onde vem

Quando o pedido deixa um eixo em aberto, o modelo preenche com o que mais aparece nos dados de treino: escolhas "seguras", que funcionam em qualquer lugar e por isso são reconhecidas e descartadas na hora. O roxo, por exemplo, não vem da biblioteca de componentes (o primário padrão do shadcn/ui é quase preto); vem do modelo preenchendo o vazio com `bg-indigo-500`.

Pior: mandar o modelo "evitar o roxo e ser diferente" o leva para o próximo agrupamento mais denso. A própria skill `frontend-design` da Anthropic descreve três looks onde a geração "anti-IA" se concentra: (1) creme perto de `#F4F1EA` + serifa display de alto contraste + terracota; (2) quase-preto + um acento verde-ácido ou vermelhão; (3) broadsheet de fios finos, raio zero, colunas densas. **Você se moveu, não escapou.**

Medições públicas (números de autores de ferramentas, não auditoria independente): Adrian Krebs avaliou 1.590 sites do Show HN contra 16 padrões (abr/2026) e encontrou 22% com 4 ou mais; o Impeccable diz que 74% das páginas geradas pelo GPT usavam fundo creme e 76% exageravam no espaçamento entre letras.

## Tabela

| # | Sintoma | Onde | Por que denuncia | O que fazer |
|---|---|---|---|---|
| **Cor e superfície** |||||
| 1 ★ | Gradiente roxo→azul, glow, sombra colorida | D/M | preenchimento padrão com `indigo`/`violet` | paleta derivada de um objeto do cliente (fachada, uniforme, material) |
| 2 ★ | Creme + serifa + terracota | D/M | primeiro agrupamento "anti-IA", hoje também default | se o cliente for quente, ponha o calor em outro eixo (foto, tipo humanista), com fundo que não seja creme |
| 3 ★ | Quase-preto + verde-ácido ou laranja único | D/M | segundo agrupamento | escuro só se o negócio for noturno ou técnico de verdade |
| 4 | Cinza sobre fundo colorido | D/M | contraste ruim, textura lavada | texto num tom escuro da própria matiz, ≥ 4,5:1 |
| 5 | Azul hospitalar + branco + ícone de dente | D/M | clichê do nicho que a IA reforça | cor do ambiente real da clínica ou de um material (madeira, pedra, tecido) |
| **Tipografia** |||||
| 6 ★ | Inter, Geist ou Arial em tudo | D/M | o sinal nº 1 nos inventários | par deliberado: display com personalidade + texto legível |
| 7 | Eyebrow em caixa-alta espaçada acima de todo título | D/M | rótulo decorativo que não informa nada | rótulo só quando informa ("Seg–Sáb, 8h–19h") |
| 8 | Espaçamento entre letras extremo nos títulos | D/M | vício de geração | tracking ajustado; negativo leve só em display grande |
| **Estrutura** |||||
| 9 | Hero centralizado + selo em pílula + 2 botões | D | template de SaaS | hero-tese: a coisa mais característica do cliente (foto autoral, rosto do profissional, mapa do bairro) |
| 10 | Hero → 3 features → depoimentos → FAQ → CTA | D/M | a mesma ordem em qualquer negócio | ordem pelas perguntas do cliente: "dói?", "quanto custa?", "onde fica?", "quem me atende?" |
| 11 ★ | Três cards iguais com ícone centralizado | D/M | `p-6 rounded-2xl shadow-lg` × 3 | lista editorial, tabela de tratamentos, linha do tempo real, foto + legenda |
| 12 | Card dentro de card, glassmorphism | D/M | profundidade sem hierarquia | uma camada de profundidade; fio ou cor de fundo para agrupar |
| 13 | Numeração 01/02/03 sem sequência real | D/M | enfeite que finge ordem | numerar só etapas reais (1ª consulta → plano → tratamento) |
| 14 | Bento grid em tudo | D | virou padrão de seção de features | só com 6 a 9 itens de pesos diferentes, e reordenação pensada no mobile |
| 15 | Brutalismo de fachada (borda grossa, sombra dura) | D/M | estilo emprestado sem motivo; falha em categorias de confiança | no máximo um acento (um CTA), nunca a página inteira em saúde |
| 16 | Seta → em todo botão, emoji como ícone | D/M | muleta de "ação" | verbo exato: "Agendar pelo WhatsApp", "Ver como chegar" |
| **Texto em português** |||||
| 17 ★ | "Transforme seu sorriso", "Eleve sua autoestima" | D/M | verbo abstrato + promessa genérica | fato concreto: "Clareamento em 2 sessões, com moldeira para casa" |
| 18 ★ | "Solução completa", "cuidado humanizado", "tecnologia de ponta", "excelência" | D/M | troque o logo e o texto continua valendo | prova: equipamento, número de atendimentos, especialidade, registro (CRO, CRM) |
| 19 | "Sua jornada", "mais que um sorriso", "venha nos conhecer" | D/M | tom de agência, não de pessoa | voz do profissional na 1ª pessoa, frases curtas |
| 20 ★ | Depoimento inventado com avatar genérico | D/M | sinal clássico de site gerado; além disso, é falso | avaliações reais do Google com link, ou nenhuma |
| **Fotografia** |||||
| 21 ★ | Banco de imagem: sorriso perfeito, jaleco imaculado | D/M | ninguém reconhece o lugar | sessão autoral de meio dia: mãos, instrumentos, recepção, fachada, luz natural |
| 22 ★ | Imagem gerada por IA "cinematográfica" | D/M | o "quente cinematográfico" já é assinatura de IA | foto real com um único preset de cor |
| 23 ★ | Antes/depois sem contexto | D/M | desconfiança e risco regulatório (CFO-196/2019 em odontologia) | só pelo profissional que fez o procedimento, com autorização escrita do paciente; na dúvida, fora |
| **Mobile** |||||
| 24 ★ | Hero que vira parede de texto | M | desktop espremido | hero mobile próprio: H1 ≤ 3 linhas a 390 px, imagem recortada via `<picture>`, CTA visível sem rolar |
| 25 | Cards empilhados sem ritmo | M | grid de 3 colunas que virou 1 | alternar formatos: carrossel com a próxima aparecendo, lista compacta, bloco de destaque, acordeão |
| 26 ★ | CTA perdido no fim ou no menu | M | o desktop tinha CTA no cabeçalho | barra fixa inferior com WhatsApp + telefone, com safe area e sem cobrir o foco |
| 27 | Menu hambúrguer com 8 itens | M | navegação do desktop transplantada | 3 ou 4 destinos + botão de ação |
| 28 | Botão flutuante de WhatsApp verde-padrão cobrindo conteúdo | M | plugin padrão | WhatsApp na barra de ação, com a cor da marca e ícone oficial pequeno |
| 29 | Fade-up em toda seção | D/M | animação demais é sinal de geração | um momento orquestrado só, e `prefers-reduced-motion` respeitado |

## Como separar DESKTOP e MOBILE na revisão

- Olhe a captura de 1440 e anote só o que aparece nela. Depois a de 390 e a de 360, e anote só o que é do mobile (mesmo que o sintoma seja "D/M", diga em qual largura viu).
- O mesmo sintoma nas duas larguras conta uma vez, com as duas larguras indicadas.
- 768 (tablet) e 1366 servem para pegar quebras: hero cortado, menu que não cabe, foto distorcida.
