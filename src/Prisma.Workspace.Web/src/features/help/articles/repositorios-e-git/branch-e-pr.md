---
titulo: Branch, PR e o que move o card
resumo: Como o Prisma liga branch e pull request à task, como vincular à mão e quais eventos do GitHub movem o card.
ordem: 2
atualizado_em: 2026-11-27
revisar_ate: 2027-02-27
relacionados:
  - repositorios-e-git/github-app
  - integracoes/integracoes
---

## Como o PR encontra a task {#como-o-pr-encontra-a-task}

Quando chega um evento de pull request, o Prisma procura a task nesta ordem:

1. um **vínculo explícito** (feito na tela, em **PRs não vinculados**, ou pelo MCP com `vincular_pr`);
2. o **nome da branch** no formato `task/T-0001-descricao`;
3. um único `[T-0001]` no **título do PR**.

PRs vindos de um fork sempre precisam de vínculo explícito. As mensagens de commit não são lidas pelo Prisma; a convenção `tipo(T-0001): resumo` serve só para o histórico do repositório.

## Convenção de nomes {#convencao}

- Branch: `task/T-0001-descricao-curta`.
- Título do PR: `[T-0001] título da entrega`.
- Commits: `feat(T-0001): resumo` (recomendado, não obrigatório para o Prisma).

## Vincular branch {#vincular-branch}

Pelo MCP, `vincular_branch` liga uma branch a uma task. O ID precisa ter o formato `T-0001`, o repositório precisa estar conectado e ativo no projeto e a branch não pode citar outra task. O vínculo começa como local e vira publicado quando o GitHub avisa que a branch existe. Vincular branch **não move o card**.

## PRs não vinculados {#prs-nao-vinculados}

A aba **PRs não vinculados** (Configurações do projeto > Integrações) lista os PRs que chegaram sem uma task identificável, 25 por página. Cada linha mostra o número e o título, a branch de origem e de destino e o motivo, por exemplo: "A branch precisa seguir o formato task/T-0001-descricao, ou o PR precisa de vínculo explícito."

Quem pode editar tasks vê os campos **Task do PR** e **Motivo do vínculo** e o botão **Vincular PR**. Depois do vínculo, a mensagem é "PR vinculado." Sem pendências: "Nenhum PR aguardando vínculo."

Pelo MCP, `vincular_pr` aceita só a URL canônica `https://github.com/dono/repositorio/pull/123`, sem parâmetros. O token de papel sistema não vincula PRs.

## O que move o card {#o-que-move-o-card}

Só tasks **já autorizadas** por uma pessoa (card movido para Em desenvolvimento no quadro) são movidas pelo GitHub. Para as demais, os eventos só ficam registrados.

| Evento no GitHub | De | Para |
|---|---|---|
| branch criada | qualquer | não move (só vincula) |
| PR aberto como rascunho | qualquer | não move |
| PR aberto, pronto para revisão ou reaberto | Em desenvolvimento | Em revisão |
| PR fechado sem merge | Em revisão | Em desenvolvimento |
| revisão pedindo mudanças no commit atual | Em revisão | Em desenvolvimento |
| merge na branch principal, sem outro PR aberto da task e sem pedido de mudança pendente | Em revisão | Em teste |
| merge em outra branch | qualquer | não move |

Aprovação sozinha não move o card. Nada leva o card para **Concluída**: essa etapa é de uma pessoa. Eventos antigos, ou anteriores ao último movimento feito à mão, são ignorados.

## O card não andou? {#o-card-nao-andou}

1. A task foi autorizada (está ou passou por Em desenvolvimento por ação de uma pessoa)?
2. A integração do projeto e o repositório estão ativos?
3. O PR aparece em **PRs não vinculados**? Então falta o vínculo: renomeie a branch, ponha `[T-0001]` no título ou vincule à mão.
4. O merge foi na **Branch principal** cadastrada no repositório?
5. No GitHub, em **Settings** > **Webhooks** > **Recent Deliveries**, as entregas aparecem com resposta 202 (aceita) ou 200 (repetida)?
