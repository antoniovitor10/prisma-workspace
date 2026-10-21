---
name: passar-bastao
description: Deixa uma task incompleta em estado retomável por outra sessão ou outro agente, preenchendo a seção Handoff da task. Use ao encerrar uma sessão com trabalho pela metade, ao trocar de ferramenta ou modelo, ou quando o desenvolvedor pedir para parar.
metadata:
  origem: metodo
  versao: "0.2.0"
---

# Passar o bastão

O estado do trabalho vive **na task**, nunca na conversa nem em arquivo global. Quem retomar lê a task e continua sem perguntar o que já foi respondido.

## Passos

1. Commit do que está feito, mesmo parcial, com `wip(T-XXXX): <o que está pronto>`. Nada fica só na árvore de trabalho.
2. Preencha a seção **Handoff** da task:

   ```
   ### Handoff · <data e hora> · <ferramenta/modelo>
   **Feito:** <lista objetiva, com arquivos>
   **Falta:** <lista objetiva, na ordem sugerida>
   **Branch e último commit:** task/T-XXXX-slug · <sha curto>
   **Comandos que rodei e resultado:** <testes, build; o que passa e o que não passa>
   **Decisões tomadas nesta sessão:** <e por quê>
   **Dúvidas em aberto:** <o que precisa do desenvolvedor; se houver, status bloqueada>
   **Armadilhas:** <o que parece fácil e não é>
   ```

3. Status: continua `em-desenvolvimento` (ou `bloqueada`, se há dúvida). Registre no histórico: `handoff`.
4. Se o rastreador é o Prisma, comente lá o resumo do Handoff.
5. Diga ao desenvolvedor em 3 linhas: onde parou, o que falta, se há dúvida.

## Ao retomar

Quem pega a task lê o Handoff, confere `git log` e `git status` da branch, roda os comandos listados para confirmar o estado, e **apaga o Handoff** quando a task concluir (o relatório de execução substitui). Nunca confie no Handoff sem conferir o git.
