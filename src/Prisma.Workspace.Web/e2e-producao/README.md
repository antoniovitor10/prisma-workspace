# Suíte Playwright de produção

Suíte curta que o agente roda contra a produção (`https://prisma.nordevs.com.br`) logo depois de cada publicação. Ela é separada da suíte E2E (`../e2e/`), que continua proibida contra produção.

Ela só age no projeto **"Teste de produção (agente)"** da organização **"pessoal"**. Ela nunca cria organização nem projeto, nunca muda configuração e nunca toca outro projeto.

## Onde roda

Só no PC do desenvolvedor, pelo agente. Nunca no CI nem no servidor, porque o repositório é público e a senha da conta de teste fica só no PC. O comando e a configuração se recusam a rodar quando `CI` ou `GITHUB_ACTIONS` está definida.

## Preparação do desenvolvedor (uma vez)

1. Na org "pessoal", crie o projeto **Teste de produção (agente)**, com pelo menos um quadro de duas colunas ou mais.
2. Crie ou escolha uma conta de teste e adicione-a só a esse projeto, com permissão de edição. Se ela enxergar outro projeto não arquivado, a preparação para.
3. Crie `src/Prisma.Workspace.Web/.env.producao.local` (já ignorado pelo git) e preencha:

   ```
   PROD_E2E_EMAIL=
   PROD_E2E_PASSWORD=
   # opcional; padrão https://prisma.nordevs.com.br
   PROD_E2E_BASE_URL=
   ```

   Só o desenvolvedor abre e preenche esse arquivo. O agente nunca o abre nem cita valores.

   `PROD_E2E_BASE_URL` só aceita `https://prisma.nordevs.com.br`. Outra URL https só passa com `PROD_E2E_PERMITIR_OUTRO_HOST=1`, para que um valor trocado por engano não mande a senha a outro host.

## Como rodar

Na pasta `src/Prisma.Workspace.Web`:

```
# só a base, desktop e Pixel 5
PROD_E2E_EVIDENCIAS=<espaço>/docs/trabalho/<HU>/evidencias/<T-XXXX> npm run e2e:producao

# a base mais e2e-producao/tasks/T-XXXX.spec.ts
PROD_E2E_EVIDENCIAS=<espaço>/docs/trabalho/<HU>/evidencias/<T-XXXX> npm run e2e:producao -- T-XXXX
```

No PowerShell: `$env:PROD_E2E_EVIDENCIAS='<pasta>'; npm run e2e:producao -- T-XXXX`.

Opções do Playwright podem ir depois, no formato `--opcao` ou `--opcao=valor` (ex.: `--list`, `--headed`).

## O que acontece

1. **Credenciais** (`rodar.mjs`): se o arquivo não existe ou falta `PROD_E2E_EMAIL` ou `PROD_E2E_PASSWORD`, o comando para antes de abrir o navegador e diz só o nome da variável.
2. **Login** (`fixtures/auth.producao.setup.ts`): `POST /api/auth/login` com `fetch` do Node, sem navegador, sem trace, vídeo nem print. A org "pessoal" é achada em `GET /api/organizations` pelo nome ou slug. A sessão vai para `e2e-producao/.auth/sessao.json` (ignorado pelo git).
3. **Preparação** (`fixtures/preparar.producao.setup.ts`): confere a org ativa (`GET /api/organizations/current`), acha o projeto pelo nome exato (`GET /api/projects`, depois `GET /api/projects/{id}`) e para se a conta enxergar mais de um projeto não arquivado, se não achar, se houver mais de um com o nome ou se não houver quadro. Arquiva as sobras: tarefas abertas com `[e2e-prod]` no título (`GET /api/WorkItems/project/{id}`, `DELETE /api/WorkItems/{id}`), e informa quantas eram.
4. **Base** (`base/base.spec.ts`), no desktop e no Pixel 5: abre o projeto pelo cartão → quadro → backlog → cria a tarefa `[e2e-prod] <data e hora> <desktop|celular>` em "Novo item" → no quadro, clica em "Mover tarefa para a próxima coluna" (a coluna da direita da de criação, seja qual for o nome) → abre o detalhe → "Arquivar" na gaveta → confere pela API que não sobrou tarefa aberta com o prefixo.
5. **Spec da task** (`tasks/T-XXXX.spec.ts`): só roda quando o comando recebe `T-XXXX`. O arquivo fica no repositório como histórico. Specs de task usam a fixture `fixtures/projeto-teste.ts`.

### Trava de escrita

A fixture `fixtures/projeto-teste.ts` intercepta no navegador toda requisição à origem da produção. Leitura (GET, HEAD, OPTIONS) passa. Escrita fora de `/api/` só passa numa lista fechada, `ESCRITAS_FORA_DA_API`, que hoje tem só `POST /hubs/<hub>/negotiate` (SignalR). A lista é fechada de propósito: ampliá-la é mudança de segurança e passa por revisão. Em `/api/`, passam só: criar tarefa com o prefixo num quadro do projeto de teste, mover, editar ou arquivar tarefas criadas pela própria execução, e renovar o token. Qualquer outra escrita é abortada antes de sair do navegador e reprova o teste. Isso vale também para a org: escrita com outra organização no cabeçalho é bloqueada.

`PROD_E2E_PROJETO` troca o nome do projeto **só** para provar a parada: se existir um projeto com o nome informado, a suíte se recusa a rodar.

## Evidências e relatório

- Prints de cada etapa (e o de falha) e um `resumo.md` vão para `PROD_E2E_EVIDENCIAS/<carimbo da execução>/`, verde ou não: cada execução tem a sua subpasta. Sem a variável, vão para `e2e-producao/resultados/evidencias/<carimbo>/`, ignorada pelo git.
- O relatório HTML fica em `e2e-producao/playwright-report/` e os artefatos em `e2e-producao/test-results/`, ignorados pelo git. Trace e vídeo só existem em falha e nunca no login.
- A senha nunca aparece em log, trace, vídeo, print nem resumo.
- **O trace e o relatório HTML guardam o token Bearer** das requisições de uma falha. `e2e-producao/playwright-report/` e `e2e-producao/test-results/` nunca vão para anexo, relatório, PR nem chat: olhe no PC e apague as duas pastas depois. A pasta de evidências recebe só prints e o `resumo.md`.
- Ao fim de uma rodada verde, `rodar.mjs` apaga todo `trace.zip` de `e2e-producao/test-results/`.

## Checagem de tipos

`npm run typecheck:e2e-producao` checa os tipos da suíte (`e2e-producao/tsconfig.json`, que inclui `playwright.producao.config.ts`). Os tipos de `credenciais.mjs` ficam em `credenciais.d.mts`: mudou um, mude o outro.

## Se a base falhar depois de um deploy

O agente **para**, mostra ao desenvolvedor o erro e os prints e **propõe rollback**. Ele nunca faz rollback sozinho: quem decide é o desenvolvedor (skill `prisma-deploy`, ADR-0002).

Se a falha deixou uma tarefa `[e2e-prod]` aberta, a execução seguinte a arquiva no início.
