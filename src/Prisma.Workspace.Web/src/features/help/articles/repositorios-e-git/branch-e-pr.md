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

1. um **vínculo explícito** (feito na tela, em **PRs não vinculados** ou na aba **Git** da tarefa, ou pelo MCP com `vincular_pr`);
2. um **vínculo de branch** (pelo MCP com `vincular_branch` ou na aba **Git**): o PR aberto dessa branch é da task, mesmo sem ID no nome;
3. o **nome da branch** no formato `task/T-0001-descricao`;
4. um único `[T-0001]` no **título do PR**.

PRs vindos de um fork sempre precisam de vínculo explícito. As mensagens de commit não são lidas pelo Prisma; a convenção `tipo(T-0001): resumo` serve só para o histórico do repositório.

## Convenção de nomes {#convencao}

- Branch: `task/T-0001-descricao-curta`.
- Título do PR: `[T-0001] título da entrega`.
- Commits: `feat(T-0001): resumo` (recomendado, não obrigatório para o Prisma).

## Vincular branch {#vincular-branch}

Pelo MCP, `vincular_branch` liga uma branch a uma task. O ID precisa ter o formato `T-0001`, o repositório precisa estar conectado e ativo no projeto e a branch não pode citar outra task. O vínculo começa como local ("não enviada") e vira enviado quando o GitHub avisa que a branch existe (ou quando a consulta a encontra, no modo sem administrador). Vincular branch **não move o card**.

## Git no card e na tarefa {#git-no-card}

- No quadro, o card mostra até três selos: o **repositório** (nome curto; o nome completo aparece ao passar o mouse), a **branch** (cortada se for longa; "não enviada" enquanto for local) e o **PR** com o número e o estado (aberto, rascunho, aprovado, mudanças pedidas, mergeado ou fechado). Com mais de um PR, o card mostra o mais recente que não foi fechado sem merge. Desligue os selos em **Filtros** > **Conteúdo do cartão** > **Git**.
- No detalhe da tarefa, a aba **Git** lista cada branch (com o link para o GitHub) e cada PR (título, estado, aprovações e mudanças pedidas), a **linha do tempo do Git** (branch vinculada, enviada, PR aberto, revisão, merge e fechamento, com data, autor e canal: tela, MCP, webhook ou consulta) e, para quem pode editar a tarefa, os campos **Nome da branch** e **URL do PR** para vincular à mão.

## Abrir o PR pelo Prisma {#abrir-pr}

Quando a branch da task já está no GitHub (push feito) e ainda não tem PR aberto, aparece o botão **Abrir PR**: no card do quadro (só no computador) e na aba **Git** do detalhe da tarefa (também no celular). Só quem pode editar, comentar e mudar o status da tarefa vê o botão.

1. O agente, ao terminar a task, grava o rascunho do PR pelo MCP com `preparar_pr` (o mesmo título e corpo do relatório). A aba **Git** mostra "Rascunho do PR: gravado em … por …".
2. Clique em **Abrir PR**. O modal traz o título e o corpo do rascunho (sem rascunho: "[T-0001] título da task" e o link do card), a branch de origem e a base (a **Branch principal** cadastrada no repositório). Edite o que quiser; o corpo fica público para quem vê o repositório.
3. O Prisma põe `[T-0001]` no início do título se ele não começar assim e acrescenta no fim do corpo a linha "Task no Prisma: <link do card>".
4. Marque **Abrir como rascunho** se quiser um PR em rascunho (ele não move o card até sair do rascunho).
5. Confirme. O PR é aberto pela GitHub App, fica vinculado à task e o card vai de **Em desenvolvimento** para **Em revisão** em seu nome. Em **Em revisão** o card só registra; nas outras etapas o PR é aberto e vinculado, e o modal avisa que o card ficou onde estava.

Se já existe um PR aberto para a branch (aberto à mão no GitHub), o Prisma não abre outro: vincula o existente e avisa "Já havia um PR, vinculei o #N". Se o GitHub recusar, o modal mostra o motivo (sem permissão na App, branch apagada, nada a comparar, rascunho não aceito no plano, limite de chamadas) e nada muda no card.

Abrir PR exige a GitHub App com **Pull requests: Read and write** (veja [Criar e instalar a GitHub App](/ajuda/repositorios-e-git/github-app#github-app)). O agente nunca abre PR: tokens do método recebem recusa; quem abre é sempre uma pessoa na tela.

### Repositório sem GitHub App: Abrir no GitHub {#abrir-no-github}

Num repositório conectado pelo **modo consulta** (token de leitura), o botão se chama **Abrir no GitHub**. O modal é o mesmo, sem a caixa de rascunho. Ao confirmar, abre numa aba nova a página de comparação do GitHub (`compare/<branch principal>...<branch>`) com o título e o corpo preenchidos, já com `[T-0001]` e a linha do link do card; você confirma o PR lá. O Prisma não grava nada nesse momento: o PR é vinculado na próxima consulta. Se o corpo for longo demais para o link, ele é cortado com a nota "(corpo completo na task do Prisma)" e o modal oferece **Copiar corpo**.

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
