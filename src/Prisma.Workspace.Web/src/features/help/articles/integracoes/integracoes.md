---
titulo: Integrações do projeto
resumo: Configurar, ativar ou pausar as Integrações, por que a integração pausada recusa todo token e quem move o card.
ordem: 1
atualizado_em: 2026-11-27
revisar_ate: 2027-02-27
paginas:
  - /projects/:projectId/settings?secao=metodo#ativar-ou-pausar
relacionados:
  - integracoes/tokens
  - integracoes/mcp
  - repositorios-e-git/github-app
---

As Integrações ligam um projeto a agentes de IA (pelo servidor MCP) e ao GitHub. Os agentes leem e registram o andamento das tasks; o GitHub informa branches e pull requests. As pessoas continuam movimentando os cards pelo quadro.

## Onde ficam {#onde-ficam}

Abra o projeto > **Configurações** > item **Integrações** do menu da esquerda. O item só aparece quando a instalação tem as Integrações habilitadas (configuração `MethodGit:Enabled` no servidor; veja [Administração](/ajuda/administracao/instalacao-e-configuracao#integracoes-e-github)).

O que cada pessoa vê depende da permissão no projeto:

- **Tokens** e **Repositórios**: administradores do projeto.
- **PRs não vinculados**: todos que veem o projeto.
- **Retomadas**: quem pode mudar o status das tasks.

## Configurar no projeto {#configurar}

Na primeira vez, a seção mostra "Método ainda não configurado".

1. Em **Quadro do método**, deixe **Criar quadro Método** ou escolha um quadro existente. Um quadro existente recebe as colunas que faltarem desta lista: Backlog, Pronta, Em desenvolvimento, Em revisão, Em teste, Concluída e Bloqueada. Colunas e cards existentes são preservados.
2. Clique em **Configurar método neste projeto**.
3. O Prisma cria (ou confere) os campos dos cards: ID do metodo, HU, Arquivo, Cobre, Risco e PR.

A configuração termina com a integração **pausada**. Ative em seguida (próxima seção). Só um administrador do projeto, numa sessão do navegador, configura e ativa; quem não tem permissão vê "Peça a um administrador com permissão de gerenciar o projeto para configurar o método."

## Ativar ou pausar {#ativar-ou-pausar}

O topo da seção mostra o quadro usado e o estado: ativo ou pausado.

- **Reativar método** liga a integração. Mensagem: "Método reativado."
- **Pausar método** desliga para agentes e para o GitHub. Mensagem: "Método pausado para integrações e agentes."

A reativação pode ser recusada quando as sete colunas ou os campos do card foram apagados ou renomeados. A mensagem diz o que revisar (por exemplo "O campo HU está ausente, inativo, duplicado ou incompatível.").

### Por que a integração pausada recusa todo token {#pausada-recusa-token}

Cada chamada de um agente, de uma pessoa pelo MCP ou de um sistema traz um token do projeto. A cada chamada o servidor confere se o projeto tem a integração **ativa**. Pausada, nenhum token passa, mesmo que esteja válido: a resposta é **401** com a mensagem "Token do método inválido, expirado ou revogado. Pare e avise."

A mesma resposta aparece quando o token venceu, foi revogado ou o projeto não está com status **Ativo**. Então, ao receber esse 401, confira nesta ordem:

1. a integração do projeto está ativa?
2. o status do projeto (Configurações > Geral) está "Ativo"?
3. o token aparece como "Ativo" na lista de tokens, e não "Expirado" ou "Revogado"?

Se a integração está ativa mas a configuração ficou inválida, o token entra, mas as consultas respondem **403** "A configuração do método está indisponível. Peça a um administrador para verificá-la."

## Quem move o card {#quem-move-o-card}

- **Autorizar** uma task é sempre ação de uma pessoa: mover o card para **Em desenvolvimento** no quadro do navegador. Nenhum token autoriza.
- O **agente** registra o andamento: pode bloquear a task, mandá-la de Em desenvolvimento para Em revisão (depois de autorizada) e criar tasks novas no Backlog. Todo movimento do agente exige um motivo.
- O **GitHub** move o card pelos pull requests (veja [o que move o card](/ajuda/repositorios-e-git/branch-e-pr#o-que-move-o-card)).
- Nada move o card para **Concluída** sozinho: essa etapa é de uma pessoa.

## Retomadas {#retomadas}

Quando o agente bloqueia uma task autorizada durante o desenvolvimento, só ele pode retomá-la, e só depois que uma pessoa liberar. Na aba **Retomadas**:

1. Preencha o motivo em "Motivo da liberação" da task (até 1000 caracteres).
2. Clique em **Liberar retomada**. Mensagem: "Retomada liberada. O agente pode retomar seu próprio bloqueio."

A task continua bloqueada até o agente retomar, e a liberação vale uma vez: qualquer outro movimento a cancela. Com a integração pausada, a aba pede para reativá-la antes.
