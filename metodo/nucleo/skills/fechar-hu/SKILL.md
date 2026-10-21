---
name: fechar-hu
description: Fecha uma HU quando a última task dela conclui, conferindo se o código entregue bate com a HU (convergir) e extraindo aprendizados para as skills do projeto (retrospectiva). Use quando o desenvolvedor disser que uma HU terminou ou quando mover a última task dela para concluida.
metadata:
  origem: metodo
  versao: "0.5.0"
---

# Fechar a HU

Duas partes, na mesma sessão. Nenhuma edita código.

**Pré-requisito:** todas as tasks da HU estão `concluida` e com a seção Execução preenchida. Se não estiverem, liste quais faltam e pare. Se o trabalho foi feito fora do método, o desenvolvedor move o status (ou pede explicitamente) e descreve o que mudou antes de você fechar.

## Parte 1 · Convergir

1. Leia a HU (CAs, decisões, esclarecimentos, fora do escopo) e os relatórios de execução de todas as tasks.
2. Para cada CA, aponte onde no código ou no comportamento ele está cumprido e qual task o cumpriu. Confira no código, não só no relatório.
3. Liste o que a HU pede e **não está** entregue, e o que foi entregue **além** da HU.
4. O que falta vira task nova em `backlog` (só acrescenta; nunca edita task concluída nem código). O que sobrou vira pergunta: manter, remover ou registrar no fora do escopo?
5. Se tudo bate: HU `status: concluida`.

## Parte 2 · Retrospectiva (10 linhas, não mais)

Rode `node metodo/nucleo/scripts/tasks.mjs metricas HU-XXXX` e use os números. Responda na seção **Fechamento** da HU:

- O que a entrevista e a clarificação **não** pegaram e apareceu como bloqueio?
- Quantas voltas de revisão houve e por quê? Quantas tasks o desenvolvedor aprovou como vieram (`revisao_humana: aprovado`)? Nas outras, o que o agente deixou passar?
- Alguma task estava grande ou pequena demais?
- Que regra do projeto foi descoberta (ex.: "o cliente não aceita X", "este módulo exige Y")?
- Que comando ou atalho faltou nas skills do projeto?
- **Que regra escrita ficou velha?** O que esta HU mudou que torna falsa uma linha de `principios.md`, de uma skill do projeto ou de um ADR? Skill do projeto descreve o que é verdade **hoje**, não o histórico.

Para cada regra descoberta **ou que ficou velha**, **proponha** o texto exato: o que entra, o que muda e o que sai (skill do projeto, `principios.md` ou ADR substituto). Remover regra obsoleta vale tanto quanto acrescentar: é o que impede as skills de inchar com o tempo. Se uma skill passou de ~300 linhas, proponha quebrá-la por assunto. O desenvolvedor aprova; só então aplique (skill `criar-skill-projeto`). Se a lição é sobre o **método** (não o projeto), registre em `docs/trabalho/sugestoes-metodo.md` para levar ao repositório central.

## Regras

- Convergir sem retrospectiva deixa o projeto igual; retrospectiva sem convergir fecha HU incompleta. As duas.
- Não reabra discussão de escopo: o que a HU não pedia não é "faltou".
