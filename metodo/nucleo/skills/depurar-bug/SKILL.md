---
name: depurar-bug
description: Investiga um bug até a causa raiz, com reprodução e evidência, antes de qualquer correção. Use sempre que o desenvolvedor relatar erro, falha de teste ou comportamento inesperado, e sempre que um teste quebrar durante a execução de uma task.
metadata:
  origem: metodo
  versao: "0.4.0"
---

# Depurar um bug

Trilha **bug**: 1 task com seção "Diagnóstico". Corrigir sem diagnóstico é chute; chute que passa esconde o problema.

## Fase 1 · Entender (perguntas curtas, 1 rodada)

O que acontece · o que deveria acontecer · como reproduzir (passos) · onde (página, ambiente, navegador/dispositivo) · desde quando · quem é afetado · há print, log ou mensagem de erro.
Leia o código relacionado e as skills do projeto antes de perguntar: muita resposta está lá.

## Fase 2 · Reproduzir

Reproduza o bug **antes** de mexer em qualquer coisa: teste automatizado que falha, comando, ou passos no navegador. Se não conseguir reproduzir, diga isso e pare: não corrija o que não vê.

## Fase 3 · Isolar e explicar

- Siga o dado ou a chamada do sintoma até a origem. Anote o caminho (arquivo → função → linha).
- Formule a hipótese da causa raiz em uma frase. Teste a hipótese (log temporário, teste, inspeção). Se não confirmar, nova hipótese; não acumule "correções" preventivas.
- Pergunte "por que isso era possível?" pelo menos uma vez: a causa raiz costuma estar uma camada acima do sintoma.

## Fase 4 · Registrar e corrigir

1. Crie a task avulsa (`fluxo.md`, "Como criar uma task avulsa") com `trilha: bug` e a seção **Diagnóstico** preenchida: reprodução, causa raiz, evidência, impacto, por que não foi pego antes.
2. Critérios de aceite: o teste de regressão passa; o sintoma não reproduz; nada ao redor quebrou. Bug de interface tem teste de regressão **de interação** (refaz os cliques que reproduziam o bug); a tabela "Testes" da task diz qual.
3. Peça autorização e execute pela skill `executar-task`. A correção é a **menor** que resolve a causa raiz; melhorias ao redor viram sugestão no relatório.
4. Se a causa raiz revelar uma regra que o projeto deveria ter (validação, princípio), proponha para `principios.md` ou para a skill do projeto.

## Regras

- Um bug em produção com usuários afetados agora é **incidente**: use `hotfix-producao` primeiro.
- Não "aproveite" para refatorar. Uma task, uma causa.
- Remova todo log temporário antes de concluir (`verificar-antes-de-concluir` checa).
