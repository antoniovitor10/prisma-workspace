---
titulo: Conectar um repositório do GitHub
resumo: GitHub App, onde achar o App ID e o Installation ID, as referências da chave e do segredo, o webhook e o modo consulta com token de leitura, para repositórios sem acesso de administrador.
ordem: 1
atualizado_em: 2026-11-27
revisar_ate: 2027-02-27
relacionados:
  - repositorios-e-git/branch-e-pr
  - integracoes/tokens
  - administracao/instalacao-e-configuracao
---

Conectar um repositório faz o Prisma acompanhar as branches e os pull requests das tasks e mover o card sozinho nos momentos certos. Há dois jeitos: **GitHub App + webhook** (o GitHub avisa o Prisma na hora; exige administrador do repositório) ou **Consulta com token de leitura** (o Prisma lê o GitHub de hora em hora; não exige administrador, veja [Sem acesso de administrador](#sem-admin)).

## O que você precisa {#o-que-voce-precisa}

- As Integrações configuradas e ativas no projeto (veja [Integrações do projeto](/ajuda/integracoes/integracoes)).
- Ser administrador do projeto no Prisma.
- Um token com papel **Sistema de integração** já emitido na aba **Tokens**. Sem ele, o formulário fica desligado: "Emita primeiro um token com papel Sistema de integração na seção Tokens."
- Uma GitHub App instalada no repositório e alguém com acesso de administrador ao repositório para criar o webhook.
- Duas configurações cadastradas no servidor pelo administrador da instalação: a chave privada da App e o segredo do webhook (veja [Referências de segredo](#referencias-de-segredo)).

## Criar e instalar a GitHub App {#github-app}

1. No GitHub, crie a App em **Settings** > **Developer settings** > **GitHub Apps** > **New GitHub App** (na sua conta ou na organização dona do repositório).
2. Em permissões do repositório, marque só **Pull requests: Read-only** (o **Metadata: Read-only** vem junto). O Prisma pede ao GitHub um acesso limitado a esse repositório com essas duas permissões; qualquer outra configuração é recusada.
3. Gere uma chave privada em **Private keys** > **Generate a private key**. O arquivo baixado vai para o administrador da instalação cadastrar no servidor; não guarde esse arquivo no repositório nem cole o conteúdo no Prisma.
4. Instale a App só no repositório que será conectado (**Install App** > **Only select repositories**).

### Onde achar o App ID {#app-id}

Na página da App no GitHub (**Settings** > **Developer settings** > **GitHub Apps** > a sua App > **General**), o número aparece em **App ID**, logo no topo, na seção **About**.

### Onde achar o Installation ID {#installation-id}

Depois de instalar, abra a configuração da instalação (**Settings** > **Applications** > **Installed GitHub Apps** > **Configure**, ou na organização: **Settings** > **GitHub Apps** > **Configure**). O Installation ID é o número no fim do endereço da página, depois de `/installations/`.

### Onde achar o ID do repositório {#id-do-repositorio}

O ID numérico do repositório vem da API do GitHub, no campo `id` de `https://api.github.com/repos/<dono>/<repositório>` (com o GitHub CLI: `gh api repos/<dono>/<repositório> --jq .id`).

## Referências de segredo {#referencias-de-segredo}

A tela **não recebe** a chave privada nem o segredo do webhook. Os campos **Referência da chave privada** e **Referência do segredo do webhook** recebem só o **nome** de uma configuração que já existe no servidor, com letras, números, hífen ou sublinhado (até 128 caracteres), por exemplo `github-app-key` e `github-webhook-secret`.

O servidor procura o valor em `MethodGit:Secrets:<nome>` (como variável de ambiente: `MethodGit__Secrets__<nome>`). Detalhes em [Administração da instalação](/ajuda/administracao/instalacao-e-configuracao#integracoes-e-github).

O segredo do webhook precisa ter pelo menos 32 bytes aleatórios e ser exclusivo deste repositório. Se o nome não existir no servidor, a conexão falha com "Segredo da integração não configurado. Avise o administrador."

## Conectar o repositório {#conectar-o-repositorio}

Projeto > **Configurações** > **Integrações** > aba **Repositórios**. Preencha:

| Campo | O que informar |
|---|---|
| Repositório | `dono/repositório`, como `equipe/projeto` |
| ID do repositório | o número do repositório (veja acima) |
| Branch principal | a branch onde o merge leva o card para Em teste, por exemplo `main` |
| Sistema de integração | o token de papel Sistema de integração que registrará os movimentos |
| GitHub App ID | o App ID |
| Installation ID | o Installation ID |
| Servidor GitHub | `github.com` (o único aceito hoje) |
| Referência da chave privada | o nome da configuração da chave privada |
| Referência do segredo do webhook | o nome da configuração do segredo do webhook |

Clique em **Conectar repositório**. O Prisma confere a instalação e o repositório no GitHub na hora. Deu certo: "Repositório conectado. Configure o webhook no GitHub com a URL abaixo."

Erros comuns:

- "Cadastro GitHub inválido; confira IDs, host e nome do repositório."
- "Não foi possível validar a instalação e o repositório. Confira a configuração externa." (App não instalada nesse repositório, App ID ou Installation ID trocados, ou chave privada errada no servidor)
- "Nome do repositório diverge do ID autorizado pela instalação."

## Criar o webhook no GitHub {#webhook}

1. Na lista de repositórios conectados, use **Copiar URL do webhook**. O endereço segue o formato `<endereço do Prisma>/api/integrations/git/github/<id da integração>/webhook`.
2. No GitHub, no repositório: **Settings** > **Webhooks** > **Add webhook**.
3. **Payload URL**: cole a URL copiada.
4. **Content type**: `application/json`.
5. **Secret**: o mesmo valor guardado no servidor com o nome informado em **Referência do segredo do webhook**. Peça ao administrador da instalação; o Prisma nunca mostra esse valor.
6. Em eventos, escolha **Let me select individual events** e marque só **Pull requests**, **Pull request reviews** e **Branch or tag creation** (`pull_request`, `pull_request_review` e `create`).
7. Salve. O GitHub manda um `ping`, que o Prisma aceita e ignora.

O Prisma confere a assinatura de cada entrega com o segredo; entregas sem assinatura válida são recusadas.

## Pausar ou reativar o repositório {#pausar-repositorio}

Cada repositório conectado tem **Pausar integração** e **Reativar integração**. Pausado, o Prisma ignora os eventos desse repositório. Ao reativar, a conexão é conferida de novo no GitHub.

## Sem acesso de administrador no GitHub (modo consulta) {#sem-admin}

Quando você não é administrador do repositório (não dá para instalar a App nem criar o webhook), conecte no modo **Consulta com token de leitura**. O Prisma lê o GitHub com um token de leitura da organização, de hora em hora, e move o card pelas mesmas regras do webhook.

### Criar o token de leitura {#token-de-leitura}

1. No GitHub: **Settings** > **Developer settings** > **Personal access tokens** > **Fine-grained tokens** > **Generate new token**.
2. Em **Resource owner**, escolha a organização dona dos repositórios (ou o seu usuário).
3. Em **Repository access**, escolha só os repositórios que terão task.
4. Em permissões do repositório, marque só **Pull requests: Read-only** e **Contents: Read-only**.
5. A organização pode exigir que um administrador dela aprove o token. Sem a aprovação, o GitHub recusa o acesso e o Prisma mostra: "O GitHub recusou o acesso: falta a aprovação do token pela organização ou a leitura deste repositório."

### Cadastrar o token e conectar o repositório {#conectar-modo-consulta}

1. Projeto > **Configurações** > **Integrações** > aba **Repositórios**.
2. Em **Tokens de leitura por organização**, informe a **Organização GitHub** (a parte antes da barra, como `minha-organizacao`) e cole o **Token de leitura**. Clique em **Salvar token**. O token é guardado cifrado e nunca mais aparece: a tela mostra só o começo dele (`github_pat_` e mais cinco caracteres), quem cadastrou e quando. Um token por organização vale para todos os repositórios dela no projeto.
3. Em **Como o Prisma lê este repositório**, escolha **Consulta com token de leitura**. O formulário passa a pedir só **Repositório**, **Branch principal** e **Sistema de integração** (sem App ID, Installation ID nem webhook).
4. Clique em **Conectar repositório**. O Prisma lê o repositório com o token para confirmar o acesso. Deu certo: "Repositório conectado no modo consulta."

### Consultar agora e erros {#consultar-agora}

- O Prisma consulta cada repositório no modo consulta **de hora em hora**. Para não esperar, use **Consultar agora** no repositório (aba **Repositórios**) ou na aba **Git** da tarefa. É uma consulta por minuto por repositório; um segundo clique dentro de 1 minuto recebe "Uma consulta deste repositório acabou de rodar. Tente de novo em 1 minuto."
- A linha do repositório mostra a **última consulta com sucesso** e, se a última falhou, o motivo, sem o texto do GitHub: token inválido, vencido ou revogado; falta de aprovação da organização; repositório fora do token. Com erro, nenhum card se move.
- Para trocar o token, salve um novo para a mesma organização. **Revogar token** apaga o token no Prisma e os repositórios dela param de ser consultados até um novo token. Revogue também no GitHub se ele vazou.
- Nos repositórios sem ID da task no nome da branch, o agente vincula a branch pelo MCP (`vincular_branch`) ou você vincula pela aba **Git** da tarefa; o PR dessa branch é ligado à task pela consulta.
