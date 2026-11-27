---
id: ADR-0106
titulo: "GitHub App com escrita só em Pull requests, para abrir PR pelo card por ação humana na UI"
status: aceita
data: 2026-11-12
origem: "HU-0007, PRD D1 (decisão do desenvolvedor de 2026-11-12); registrado na T-0027"
substitui: "emenda a ADR-0101 (D101), item 2 (permissão mínima da App)"
---

# ADR-0106 · GitHub App com escrita só em Pull requests, para abrir PR pelo card por ação humana na UI

## Contexto

A ADR-0101 (D101) limita a GitHub App do método a leitura de PRs, reviews e deployments, com escrita de checks/status só na fase 3. A referência `metodo-git-integration.md` (CA-2A-05 e a seção de credenciais) repete essa regra. A HU-0007 pede que o desenvolvedor abra o PR da task com um clique no card do Prisma, com o texto que o agente preparou (`preparar_pr`), e veja o card andar na hora. Abrir PR pela API do GitHub exige `Pull requests: write` na App. O ADR-0001 do espaço continua: o agente prepara e para ali; quem abre o PR é uma pessoa.

## Decisão

A App tem leitura de PRs, reviews e deployments e escrita **só em Pull requests**, usada só quando uma pessoa logada confirma Abrir PR na tela. Token do método não abre PR. Sem escrita de conteúdo, merge ou administração de proteção.

Como isso fica no código (T-0027):

1. A sessão de leitura do `GitHubProvider` continua pedindo e exigindo `{ pull_requests: read, metadata: read }`; resposta com qualquer outra permissão é recusada (`git_permission_mismatch`). Webhook, consulta e vínculo não mudam.
2. Abrir o PR usa um token de instalação de escrita separado, pedido no clique, para um único repositório (`repository_ids: [id]`), com exatamente `{ pull_requests: write, metadata: read }`, conferido na resposta (a mais ou a menos é recusa), nunca guardado em cache nem reaproveitado por leitura, e descartado depois do uso. O token nunca vai para log, tela, erro, histórico ou resposta.
3. O endpoint `POST /api/projects/{projectId}/method/tasks/{id}/pull-requests/open` só aceita sessão da UI de uma pessoa com Editar, Comentar e Mudar status na task. Token do método, de qualquer papel, recebe 403 `interactive_pr_required`.
4. Contents continua leitura. Criar branch, fazer merge, editar ou fechar PR pelo Prisma continuam fora.

## Alternativas descartadas

- **Manter só leitura e usar a página de comparação do GitHub em todos os repositórios:** nenhuma permissão nova, mas o card só anda quando o webhook chega, o vínculo depende do nome da branch e há uma ida ao GitHub a mais. Continua valendo para repositório sem App (modo consulta, T-0029).
- **PR em nome da pessoa (token OAuth do usuário):** autoria humana no GitHub, mas exige fluxo OAuth novo e guarda de token de usuário.
- **Agente abre o PR com `gh`:** contraria o ADR-0001 e o princípio 1.

## Consequências

### Positivas

- Um clique abre o PR com o texto do agente, vincula o PR à task e move o card de `em-desenvolvimento` para `em-revisao` como a pessoa que clicou; o webhook que chega depois não duplica nada.
- O token de escrita vive segundos e só existe dentro do clique.

### Negativas / custos

- Permissão nova na App: cada instalação precisa aceitar a mudança no GitHub. Até o aceite, o botão mostra `git_pr_permission_missing` naquele repositório (as leituras seguem normais).
- O corpo do PR fica público nos repositórios públicos (o modal mostra o texto inteiro antes de confirmar).

## Publicação

Primeiro publicar o código (que só pede escrita no clique); depois o desenvolvedor muda a App `prisma-metodo-vitor` para `Pull requests: Read and write` (Contents continua Read) e aceita a permissão nas instalações (prisma-workspace, distribuidora, cooperarpay). A troca fica registrada no relatório da T-0027 (CA-11).

## Como reverter

Voltar a App para `Pull requests: Read only`. O botão passa a mostrar `git_pr_permission_missing` e nada mais muda; para remover a função, retirar o endpoint `open` e o botão (T-0028).
