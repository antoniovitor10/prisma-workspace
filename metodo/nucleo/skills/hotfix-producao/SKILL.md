---
name: hotfix-producao
description: Trata um incidente em produção: estabiliza primeiro, com a mudança mínima, e registra depois. Use quando o desenvolvedor disser que produção está fora, quebrada ou com usuários afetados agora.
metadata:
  origem: metodo
  versao: "0.2.0"
---

# Hotfix em produção

Trilha **incidente**. A ordem muda: **estabilizar → registrar → corrigir de verdade**. É a única trilha em que o agente pode agir antes de a task existir, e mesmo assim só com o desenvolvedor presente na conversa.

## 1. Estabilizar (com o desenvolvedor na conversa)

1. Confirme o sintoma e o alcance em 2 perguntas no máximo: o que está quebrado, desde quando, quantos afetados.
2. Proponha a saída **mais rápida e reversível**, nesta ordem de preferência:
   - desfazer: rollback do último deploy, reverter o último merge (`git revert`), desligar a feature por configuração
   - contornar: desativar a parte quebrada, mensagem temporária
   - corrigir: só se for óbvio, pequeno e testável em minutos
3. Diga exatamente o que vai fazer e **espere o "vai"**. Em produção, nada sem o ok explícito, independentemente da autonomia configurada.
4. Faça, verifique em produção (saída real: curl, log, página aberta) e diga o resultado.

## 2. Registrar (logo depois)

Crie uma task avulsa (`fluxo.md`, "Como criar uma task avulsa") com `trilha: incidente`, já `concluida`, com: sintoma, horário, o que foi feito, evidência, quem aprovou. Commit com `fix(T-XXXX): hotfix …`. O "vai" do desenvolvedor na conversa autoriza o push e o deploy desse commit, acima da autonomia configurada, pela branch que o projeto usa para hotfix (ou a principal).

## 3. Corrigir de verdade (depois)

Se a estabilização foi um contorno ou rollback, crie a task da correção definitiva pela trilha **bug** (`depurar-bug`), com link para a task do incidente, e peça autorização normalmente. Um contorno que fica para sempre vira dívida.

## 4. Pós-incidente (curto)

Na task do incidente, 5 linhas: causa, por que chegou em produção, o que evitaria (teste, verificação, princípio). Se der regra nova, proponha para `principios.md`.

## Regras

- Nunca "aproveite" o hotfix para outra mudança.
- Segredos, banco de produção e dados de usuário: só com o desenvolvedor dizendo o quê e vendo o resultado.
- Se o desenvolvedor não está na conversa, não há hotfix: registre o que você encontrou e pare.
