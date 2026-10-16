# prisma-mcp

Adaptador MCP (stdio) que liga o método de trabalho ao Prisma WorkSpace: o repositório
continua sendo a verdade do **conteúdo** (HU, task, critérios) e o Prisma é a verdade do
**status** e do histórico. Decisões D100 (fase 1) e D101/D102 (fase 2a).

Na fase 2a o adaptador fala com a API do método usando um **token opaco do método**, emitido
pelo administrador do projeto na UI do Prisma. A API é a autoridade: ela confere o papel do
token, o escopo do projeto e a matriz de transições em toda operação. As regras locais do
adaptador só repetem a recusa para dar uma mensagem rápida; tirá-las não dá mais poder ao token.

O pacote tem dois executáveis:

- `prisma-mcp` (`dist/server.js`): servidor MCP com as 8 operações do contrato.
- `prisma-metodo-import` (`dist/import.js`): importa `docs/trabalho/HU-*` para o projeto.

## Operações do MCP

| Ferramenta | Parâmetros | Para quê |
|---|---|---|
| `regras_do_projeto` | nenhum | papel, escopo, validade do token, matriz efetiva e convenções, vindos da API; chamar no início da sessão |
| `obter_task` | `id` | status, título, arquivo, HU e PR de uma task |
| `listar_tasks` | `status?` | tasks do projeto, opcionalmente filtradas por status |
| `proximas_tasks` | nenhum | tasks autorizadas em `em-desenvolvimento` com todas as dependências concluídas |
| `criar_task` | `id, titulo, hu, arquivo, cobre, risco, status?` | cria a task no quadro, marcada com a HU |
| `mover_status` | `id, novo_status, motivo` | muda a coluna, se a matriz da API permitir ao papel do token |
| `comentar` | `id, texto` | relatório de execução, bloqueio, handoff |
| `vincular_pr` | `id, url` | registra o vínculo explícito do PR com a task (também aparece no campo **PR**) |

Status (uma coluna para cada, nesta ordem): `backlog` (Backlog), `pronta` (Pronta),
`em-desenvolvimento` (Em desenvolvimento), `em-revisao` (Em revisão), `em-teste` (Em teste),
`concluida` (Concluída) e `bloqueada` (Bloqueada).

No Prisma, cada task é um card de topo no quadro. A HU não vira card: ela fica no campo
personalizado **HU** (por exemplo `HU-0001`) e numa tag com o mesmo nome; o status da HU continua
no `hu.md`. `depende_de` vira vínculo de dependência entre as tasks. A chave de cada task é o campo
**ID do metodo** (`T-0001`). Os outros campos do projeto são **Arquivo**, **Cobre**, **Risco** e **PR**.

## Papéis e tokens

O administrador do projeto provisiona o método (quadro, sete colunas e campos) e emite os tokens
na tela de configurações do método do projeto. O valor do token (`pmt_` seguido de 43 caracteres)
aparece **uma única vez**, na emissão; a lista mostra só o prefixo e as datas.

| Papel do token | Pode | Não pode |
|---|---|---|
| `agente` | ler; criar task em `backlog` (ou já `concluida`, atalho/incidente); comentar; vincular PR; bloquear (exceto `concluida`); `em-desenvolvimento` → `em-revisao` no nível 0; retomar o próprio bloqueio depois que um humano liberar a retomada na UI | autorizar (`backlog`/`pronta` → `em-desenvolvimento`) por qualquer caminho; mover o que o Git prova; emitir tokens |
| `humano` | o que o próprio usuário pode, dentro do projeto do token | autorizar o início ou liberar retomada: isso só vale pela sessão interativa da pessoa na UI |
| `sistema` | uso interno da integração Git (fatos verificados do webhook) | mover pelo MCP |

A autorização de início e a liberação de uma retomada são **ações de uma pessoa na UI**. Nenhum
token, de nenhum papel, faz isso, nem com um motivo dizendo que "um humano pediu".

O histórico do card registra cada mudança com antes e depois, quem fez, o papel e o canal
(`ui`, `mcp` ou `webhook`).

## Variáveis de ambiente

A configuração vem só de variáveis de ambiente. Não grave o token em arquivo versionado.

| Variável | Obrigatória | Descrição |
|---|---|---|
| `PRISMA_URL` | sim | URL do Prisma, por exemplo `http://127.0.0.1:13100` |
| `PRISMA_TOKEN` | sim | token opaco do método (`pmt_...`), emitido na UI. JWT de login e e-mail/senha não são aceitos |
| `PRISMA_PROJECT` | sim | nome ou ID do projeto; precisa ser o mesmo do escopo do token |
| `PRISMA_BOARD` | não | nome ou ID do quadro, só para conferir com o escopo do token |
| `PRISMA_ROLE` | não | papel esperado (`agente`, `humano` ou `sistema`); se diferir do papel do token, o adaptador para |

O adaptador não escreve o token nem cabeçalho de autorização em log ou em mensagem de erro.
Com 401 (token inválido, expirado ou revogado) ou 403 (sem permissão), ele responde
"pare e avise" e não tenta outra credencial.

### Revogar e trocar o token

O administrador revoga na mesma tela; a revogação vale na requisição seguinte. Para trocar
(rotação), emita um token novo para o mesmo agente, atualize a variável `PRISMA_TOKEN` e revogue o
antigo. A autoria do agente no histórico continua a mesma depois da troca.

## Compilar

Requer Node 22.

```
cd tools/prisma-mcp
npm ci
npm run build
```

O resultado fica em `dist/`.

## Importar as tasks do repositório

```
node dist/import.js <pasta docs/trabalho> [--dry-run] [--sincronizar-status]
```

- Usa o mesmo `PRISMA_TOKEN` e as mesmas regras da API. Não cria projeto, quadro, colunas nem
  campos: o projeto precisa estar provisionado pelo administrador antes.
- Cria ou atualiza cada task pela chave **ID do metodo**, com o campo **HU** e a tag da HU, e cria os
  vínculos `depende_de`. Rodar duas vezes não duplica nada.
- Confere o lote inteiro antes de gravar: se alguma task não puder ser criada no status do
  frontmatter com o papel do token, nada é gravado.
- Nenhum token cria task já autorizada (`em-desenvolvimento`). Com token de agente, só `backlog`
  ou `concluida`. Para levar ao Prisma tasks que já estavam em andamento, importe em `backlog` e um
  humano autoriza na UI.
- `--sincronizar-status` reaplica o status do frontmatter em tasks existentes, dentro da matriz do
  papel; também não autoriza nada.
- `--dry-run` só lê os arquivos e não faz nenhuma requisição.
- No fim imprime um resumo: criados, atualizados, sem mudança, vínculos criados.

Depois da importação, tire o `status:` do frontmatter das tasks: em modo prisma ele não existe.

### Onde ver no Prisma

- **Kanban** do projeto: cada task é um card na coluna do seu status; a tag mostra a HU.
- **Itens** do projeto: lista as tasks com a coluna **Etapa**.
- Detalhe do card: abas **Comentários** (relatórios e bloqueios) e **Histórico** (quem moveu,
  quando, papel e canal).

## Configurar num PC Windows

O Prisma de desenvolvimento roda no servidor e só escuta em `127.0.0.1`. No PC, abra um túnel SSH
que leva a porta local 13100 até ele e deixe o terminal aberto:

```powershell
ssh -N -L 13100:127.0.0.1:3100 <usuario>@<servidor-dev>
```

Confira com `curl.exe http://127.0.0.1:13100/health`.

Compile o pacote no PC (seção [Compilar](#compilar)). Nos exemplos abaixo o caminho é
`C:\dev\prisma-workspace\tools\prisma-mcp\dist\server.js`; troque pelo seu.

### Token fora dos arquivos

Peça ao administrador o token de agente do projeto (ou emita, se você administra). Guarde-o como
variável de ambiente do usuário do Windows e abra um terminal novo depois:

```powershell
[Environment]::SetEnvironmentVariable("PRISMA_TOKEN", (Read-Host "Token do método" -MaskInput), "User")
```

Os clientes MCP abaixo repassam essa variável ao servidor sem que ela apareça em nenhum arquivo
do repositório.

### Claude Code

Opção 1, por comando, com escopo local. Rode na pasta do repositório do projeto; a configuração
vale só para ele e fica no seu `%USERPROFILE%\.claude.json`, fora do repositório:

```powershell
claude mcp add --scope local `
  --env PRISMA_URL=http://127.0.0.1:13100 `
  --env PRISMA_PROJECT=canais-mobile `
  --env PRISMA_ROLE=agente `
  prisma -- node C:\dev\prisma-workspace\tools\prisma-mcp\dist\server.js
```

`PRISMA_TOKEN` não entra no comando nem em arquivo nenhum: o servidor herda o ambiente do
terminal em que o Claude Code foi aberto, onde a variável do usuário já está.

Opção 2, `.mcp.json` na raiz do repositório do projeto (pode ser versionado, porque só referencia
a variável com `${...}`; o Claude Code a expande ao subir o servidor):

```json
{
  "mcpServers": {
    "prisma": {
      "command": "node",
      "args": ["C:\\dev\\prisma-workspace\\tools\\prisma-mcp\\dist\\server.js"],
      "env": {
        "PRISMA_URL": "http://127.0.0.1:13100",
        "PRISMA_PROJECT": "canais-mobile",
        "PRISMA_ROLE": "agente",
        "PRISMA_TOKEN": "${PRISMA_TOKEN}"
      }
    }
  }
}
```

Confira com `claude mcp list` (deve mostrar `prisma` conectado) e, dentro de uma sessão, `/mcp`.

### Codex

Em `%USERPROFILE%\.codex\config.toml`:

```toml
[mcp_servers.prisma]
command = "node"
args = ["C:\\dev\\prisma-workspace\\tools\\prisma-mcp\\dist\\server.js"]
# repassa a variável do usuário do Windows, sem gravar o valor aqui
env_vars = ["PRISMA_TOKEN"]

[mcp_servers.prisma.env]
PRISMA_URL = "http://127.0.0.1:13100"
PRISMA_PROJECT = "canais-mobile"
PRISMA_ROLE = "agente"
```

Confira com `codex mcp list`. Se a sua versão do Codex não tiver `env_vars`, coloque
`PRISMA_TOKEN` dentro de `[mcp_servers.prisma.env]`: esse arquivo é seu e fica fora do
repositório, mas guarda o token em texto puro; nesse caso, peça um token de validade curta.

### Primeiro uso

Numa sessão nova do agente: "chame regras_do_projeto e depois listar_tasks". A resposta mostra o
papel e o projeto do token. Se o MCP não responder ou devolver 401/403, o agente para e avisa; ele
não volta para o modo local nem pede senha.

## Testes

```
npm test
```

Roda, sem precisar de um Prisma de verdade:

- unitários: mapa de status, regras locais (guard redundante), configuração e leitura dos arquivos do
  método (com os dados reais de `canais-mobile`, quando a pasta existir);
- `token-mode`: sobe o servidor MCP por stdio contra uma API falsa e confere que não há login por
  senha, que o papel vem da API, que leituras não gravam nada, que 401 manda parar sem nova tentativa
  e que o token não vaza em erro;
- `import-token`: a importação valida o lote antes de gravar, não cria estrutura, não autoriza nada e
  é idempotente.

O teste de integração (`test/integration.test.ts`) fala com um Prisma de verdade, num projeto
próprio criado para o teste (nunca um projeto real), e só roda com `PRISMA_IT=1`. Ver o cabeçalho
do arquivo para as variáveis.

`test/ensaio-demo.mjs` repete o roteiro da demonstração num projeto que você informa.

## Limites desta fase

- Fase 2a: tokens com papel, matriz na API e webhook do GitHub. Sem reconciliação periódica (2b) e
  sem gate de merge nem deploy observado (fase 3). GitLab fora deste ciclo.
- Em produção a integração fica desligada até o administrador provisionar o projeto.
