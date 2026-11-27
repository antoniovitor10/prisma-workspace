---
titulo: Tokens de agente, humano e sistema
resumo: Para que serve cada papel, como emitir, guardar, reemitir e revogar os tokens do projeto.
ordem: 2
atualizado_em: 2026-11-27
revisar_ate: 2027-02-27
relacionados:
  - integracoes/integracoes
  - integracoes/mcp
---

Os tokens do projeto são as credenciais que o servidor MCP e o GitHub usam para falar com o Prisma. Cada token pertence a um projeto e tem um papel. O login do navegador (e-mail e senha) nunca serve no lugar de um token.

## Os três papéis {#papeis}

| Papel | Na tela | Para que serve |
|---|---|---|
| agente | Agente | um agente de IA trabalhando pelo MCP |
| humano | Humano pessoal | uma pessoa usando o MCP com as próprias permissões |
| sistema | Sistema de integração | o repositório do GitHub registrando branches e PRs |

### Agente {#papel-agente}

- Cria tasks só no Backlog (ou já concluídas, para registro).
- Bloqueia uma task (menos as concluídas) e manda uma task autorizada de Em desenvolvimento para Em revisão.
- Retoma o próprio bloqueio só depois que uma pessoa liberou em **Retomadas**.
- Todo movimento leva um motivo (até 1000 caracteres).
- **Nunca autoriza**: tirar uma task de Backlog ou Pronta para Em desenvolvimento é ação de uma pessoa na tela.
- Nunca tem mais permissão do que um Membro do projeto.

### Humano {#papel-humano}

Usa as permissões da pessoa que emitiu, mas também não leva uma task para Em desenvolvimento (a única exceção é devolver de Em revisão para Em desenvolvimento uma task já autorizada). Só a própria pessoa reemite o token humano dela.

### Sistema {#papel-sistema}

Só lê, edita e muda status por eventos do GitHub conferidos pelo servidor. Não cria tasks nem vincula PRs pelo MCP. É obrigatório para conectar um repositório.

## Emitir um token {#emitir-um-token}

1. Projeto > **Configurações** > **Integrações** > aba **Tokens**.
2. Preencha **Nome do token** (até 120 caracteres), escolha o **Papel** e a **Validade em dias** (de 1 a 90; o padrão é 30).
3. Clique em **Emitir token**.

Só administradores do projeto, numa sessão do navegador, emitem, listam e revogam tokens. Antes de configurar as Integrações no projeto, a emissão é recusada: "Provisione o método neste projeto antes de emitir tokens."

### Guarde o valor agora {#guarde-o-valor}

O valor completo aparece **uma única vez**, na caixa "Salve o token agora". Use **Copiar token** e guarde num cofre de senhas ou numa variável de ambiente; depois clique em **Já salvei o token**. O Prisma guarda só uma impressão do valor e não consegue mostrá-lo de novo.

Os tokens começam com `pmt_`, seguido de 43 caracteres. Nunca cole um token em card, comentário, wiki, commit ou arquivo versionado.

## Lista de tokens {#lista-de-tokens}

Cada linha mostra o nome, o papel, quem é o titular, o estado (Ativo, Expirado ou Revogado), o começo do valor, a validade e o último uso ("ainda não utilizado" se nunca foi usado). O último uso é atualizado no máximo a cada 5 minutos.

## Reemitir {#reemitir}

**Reemitir** existe para tokens de agente e de sistema. A identidade continua a mesma, com a mesma validade escolhida na emissão, e as permissões só podem ficar iguais ou menores. O valor novo também aparece uma única vez.

## Revogar {#revogar}

**Revogar** pede a confirmação "A integração que usa este token perderá o acesso." Depois de **Confirmar revogação**, o token para de funcionar na próxima chamada.
