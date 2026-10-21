---
name: clarificar
description: Fecha as lacunas de uma HU já escrita com até 5 perguntas objetivas, gravando as respostas na própria HU. Use depois de escrever ou alterar uma HU e antes de quebrá-la em tasks.
metadata:
  origem: metodo
  versao: "0.4.1"
---

# Clarificar a HU

A entrevista fecha o escopo. A clarificação acha o que **ainda** ficou ambíguo depois que a HU existe no papel. É um portão: sem passar por aqui, não se quebra em tasks.

## Passos

1. Leia a HU inteira e as skills do projeto que ela toca.
2. Passe pela taxonomia abaixo e anote só o que a HU **não responde** e que **mudaria a implementação**:

   | Área | Perguntas típicas |
   |---|---|
   | Dados | de onde vem, formato, obrigatório ou não, o que fazer com vazio ou inválido |
   | Permissões | quem pode ver, criar, editar, apagar; o que o resto vê |
   | Erros e limites | o que acontece na falha, timeout, duplicidade, volume alto |
   | Estados | vazio, carregando, parcial, desabilitado |
   | LGPD | dado pessoal novo? finalidade? retenção? |
   | Integrações | o que fazer quando o serviço externo cai |
   | Aceite | algum CA não é verificável por alguém que não estava na conversa? |

   Inclua sempre a pergunta "este CA dá para testar com um teste de interação? com que dado?" para cada CA com interface que não deixa isso claro.
3. Faça rodadas de até **5 perguntas**, as mais importantes primeiro, com opções específicas do projeto e uma recomendada (com o porquê). Use perguntas interativas se a ferramenta tiver. A rodada seguinte nasce das respostas: se uma resposta abriu outra dúvida que muda a implementação, pergunte. **Zero perguntas** só quando a HU responde toda a taxonomia; diga isso explicitamente, área por área.
4. Com as respostas, grave na HU, seção **Esclarecimentos**, no formato `E1 · pergunta → resposta`. Se uma resposta muda um CA, atualize o CA.
5. Se restarem dúvidas que só o cliente responde, registre em "Dúvidas do cliente" com a premissa provisória adotada.
6. Diga: **"HU clarificada. Posso quebrar em tasks?"** (ou "escrever o PRD", na trilha épico).

## Regras

- Não repita o que a entrevista já perguntou. Se a HU responde, não pergunte.
- Nenhuma pergunta de gosto ("prefere azul ou verde?") se isso já foi decidido; a clarificação é sobre comportamento, não sobre preferência.
- Pode rodar mais de uma vez, cada vez com um foco (ex.: "clarifica a parte de permissões").
