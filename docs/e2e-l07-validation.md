# L07 — instância QA e tarefas

Tarefa 5 do Maestro em 2026-10-14. Branch `test/e2e-l07` a partir de `origin/test/e2e-expansion` / `c0cbe44`. Parte A preparada primeiro, no commit `3c84291`; parte B amplia somente testes/factory e documentação. Produto, specs, migrations e specs E2E anteriores intactos.

## Parte A: QA separada

Projeto `prisma-qa`: app/API em `0.0.0.0:3110`, SQL exclusivo em `127.0.0.1:3111`, banco `PrismaWorkspace_QA`, volumes próprios. Boot aplicou 28 migrations existentes e o seed oficial (um projeto antes do QA). Imagem `prisma-qa-app:2c3816c` pinada: os fontes runtime/Dockerfile da imagem própria existente foram comparados com main 2c3816c e não diferem; imagem recebeu essa tag sem acessar containers/configuração de produção.

```bash
scripts/run-local.sh up qa
scripts/run-local.sh down qa
```

Sem `qa`, os scripts continuam selecionando `prisma-dev` / 3100 / 3102 / `DetranKanban_E2E`. O perfil valida projeto, portas e banco; setup QA não modifica as credenciais/conexões do Playwright. Compose parametriza imagem, app, SQL, banco e URL da ponte. `down` preserva volumes. QA não tem ponte IA iniciada nem usa a ponte de produção; este recorte fornece o produto/seed para inspeção visual, sem inferência.

Conta do seed: `admin@prisma.example.invalid`. Login gerado em `/home/agentes/projetos/prisma/.env.qa.login`; senha também em `.env.qa.seedpassword`; configuração em `.env.server-qa`. Arquivos ignorados, modo 600. GET `/health` e POST `/api/auth/login` em `127.0.0.1:3110` retornaram 200; a presença do token foi conferida sem imprimi-lo. Dev 3100 também respondeu 200 nos mesmos checks. A suíte usa exclusivamente 3100. Prova adicional headless (3,70 s): a SPA QA abriu o projeto seed, GET /api/projects retornou 200 e todas as chamadas API partiram para 3110, sem mutação de projetos/tarefas.

## Casos e dados

Em `src/Prisma.Workspace.Web/e2e/tasks-l07.spec.ts`, desktop/mobile, seis jornadas normais e três regressões isoladas:

| Caso | Prova |
| --- | --- |
| 39 — Criar | UI grava título/descrição/prioridade/horas/responsável na coluna própria; reload, ID, GET e detalhe confirmam persistência. |
| 80 — Editar | Título, HTML da descrição, prioridade, duas datas e responsável elegível próprio persistem na UI e API após reload. |
| 120 — Duplicar/arquivar/reativar | Novo ID e conteúdo copiado; arquivar some da lista ativa, filtro mostra arquivada, reativar volta à lista; API confirma estado e original preservado. |
| 164 — Checklist/campos | Adiciona, marca, recarrega e remove item; campos Text/Boolean próprios são gravados, recarregados e limpos para null. |
| 216 — Hierarquia | Subtarefa UI abre detalhe completo com anexos/horas; vínculo e tipo persistem; Epic sob Task e filho de Subtask recebem 400 sem alterar filhos existentes. |
| 244 — Leitor | Leitura funciona; PUT/DELETE diretos e tentativas UI recebem 404 do guard; erro ao editar, reload/GET conservam título, versão e estado não arquivado. |

Factory `e2e/fixtures/boards.ts` ampliada com parâmetros de item, campos personalizados, membro elegível e prefixo do lote. Cada cenário usa projeto/quadro/coluna/tarefa com UUID e sessão administrativa capturada. Finally remove vínculos dos membros criados, desativa suas memberships e arquiva somente o projeto próprio; verifica saída da lista ativa. Conta Identity e auditoria arquivada permanecem porque não há exclusão de conta nesse fluxo. Erros do cenário e de limpeza são preservados juntos caso um timeout feche o contexto.

Sem sleep/waitForTimeout, asserts opcionais ou entidades do seed/outros specs. O login administrativo padrão é só a identidade autorizada, não uma entidade de negócio emprestada. Respostas registradas antes da ação; checkbox controlado aguarda PUT e estado marcado; selects usam papel/nome acessível. Datas perdem foco por click no título da seção, pois Tab percorre partes do input date sem obrigatoriamente disparar blur. Edição aguarda PUT e GET de hidratação antes do próximo campo. Navegação aguarda DOM e estado HTTP/UI, como a fixture, sem esperar recursos externos de fonte. Na criação, o teste aguarda Boards/Stages/WorkItems tanto na entrada quanto no reload, confere IDs das fixtures nas respostas e o ID/título gravado antes de verificar a UI.

## Defeitos preservados

Os três `test.fail` são aplicados somente depois da preparação e limpeza das fixtures. Assim, erro de precondição/cleanup é falha inesperada; corrigir o produto fará o oracle passar e o Playwright acusar sucesso inesperado, exigindo retirar a marcação. Nenhuma correção de produto foi feita.

1. **Linha 270 — BUG-L07-01, conhecido no contrato SPEC-F-009/D59:** abrir detalhe da tarefa própria exibe o heading “Dependências e bloqueios” e autocomplete. A spec aprovada exige ocultação e já registra esse gap. Oracle exige zero headings; observado um. Fonte: `TaskDetailDrawer.tsx:510`. Não foram criados links de dependência.
2. **Linha 283 — BUG-L07-02:** criar tarefa pai com responsável pela API; abrir Subtarefas; preencher título e Adicionar. POST retorna 201, GET confirma ParentId correto e ResponsibleId null. Contraria regras 1/15 de `work-item-management.md`. `TaskDetailDrawer.tsx:325` envia criação sem responsável; handler aceita null. Oracle exige string, sem supor herança específica ou escolher responsável automaticamente.
3. **Linha 303 — BUG-L07-03:** criar dois projetos/quadros próprios; POST de Subtask no primeiro com ParentId da tarefa do segundo. Nenhum item é criado e ambas as famílias permanecem intactas, mas resposta é 500/`internal_error` em vez de erro de validação 400 com detalhe do pai. Handler lança ArgumentException (`CreateWorkItemCommandHandler.cs:94`), middleware genérico converte em 500 (`MapeamentoDeErrosMiddleware.cs:65`); tipos inválidos usam DomainException/400. A proibição de pai externo é respeitada; o defeito é o tratamento HTTP da entrada inválida. A spec não fixa esse status explicitamente; 400 segue a convenção de erros de negócio da API, sem implementar nova regra de produto.

## Dúvidas e sugestões

D36 define hierarquia tipada e Subtask como folha, coberta pelo teste. Não há limite numérico de profundidade registrado nem endpoint público para reparenting: criação gera ID no servidor e atualização não aceita ParentId. Portanto não foi inventada operação para criar ciclo estrutural ou regra de profundidade diferente da folha. Confirmar contrato/fluxo de reparenting e ciclos numa tarefa própria. Ciclos de dependência são expressamente fora do escopo da SPEC-F-009, e não foram testados/criados.

Sugestões sem implementação: pedir responsável elegível na criação rápida de subtarefa; ocultar controles de edição/arquivamento para leitor (API bloqueia, edição informa erro, arquivamento não apresenta feedback específico); associar nome acessível ao checkbox e ao botão de remover checklist; mostrar mensagem de validação legível para pai externo. A retirada da UI de dependências requer o escopo de correção separado já descrito na spec.

## Verificações

Tempos de parede com `/usr/bin/time`; comandos frontend em `src/Prisma.Workspace.Web`. Logs ignorados em `.runlogs/e2e-l07`; relatório HTML/JSON separa aprovação normal de falha esperada.

| Comando | Resultado | Tempo |
| --- | --- | --- |
| `scripts/run-local.sh up qa` | QA SQL/app saudáveis; seed oficial | 65,72 s |
| `python3 .runlogs/e2e-l07/proof.py` (antes do reset) | GET health e login 200 em QA/dev; arquivos QA modo 600 | 0,84 s |
| `node .runlogs/e2e-l07/qa-ui-proof.mjs` | SPA QA e projeto seed acessíveis; API somente em 3110 | 3,70 s |
| `bash -n scripts/server-profile.sh scripts/setup-e2e-database.sh scripts/run-local.sh` + `docker compose -p prisma-dev --env-file .env.server-dev -f compose.server-dev.yaml config --quiet` + equivalente `prisma-qa`/`.env.server-qa` | passou, sintaxe/perfis Compose | 1,12 s |
| `npx tsc -b` | passou, exit 0 | 76,70 s |
| `npm run lint` | passou, 0 erros / 12 avisos anteriores | 0,73 s |
| `npx tsc --ignoreConfig --target ES2022 --module ESNext --moduleResolution bundler --strict --skipLibCheck --types node --outDir ../../.runlogs/e2e-l07/typecheck-verified e2e/tasks-l07.spec.ts e2e/fixtures/boards.ts` | passou, tipos E2E explícitos | 4,51 s |
| `python3 -m unittest discover -s scripts/tests` | passou, 8/8 | 0,84 s |
| `npm run e2e -- e2e/tasks-l07.spec.ts --grep "duplicar, arquivar" --project=chromium-desktop --project=chromium-mobile --repeat-each=3` | passou, 7/7 incluindo setup; zero retries | 87,72 s |
| `npm run e2e -- e2e/tasks-l07.spec.ts --grep "criar tarefa pela UI" --project=chromium-desktop --project=chromium-mobile --repeat-each=3` | passou, 7/7 incluindo setup; zero retries | 117,03 s |
| `npm run e2e -- e2e/tasks-l07.spec.ts --project=chromium-desktop --project=chromium-mobile --repeat-each=3 --reporter=html,list,json` | passou: 37 normais (inclui setup), 18 falhas esperadas; zero inesperadas/retries/skips; mesmo banco | 495,81 s |
| `bash .runlogs/e2e-l07/reset-clean.sh` | banco E2E próprio recriado; 28 migrations existentes, um projeto seed; app saudável | 37,62 s |
| `python3 .runlogs/e2e-l07/proof.py` (após reset) + comparação de StartedAt | health/SPA/login QA e dev 200; QA não reiniciada; arquivos QA modo 600 | 0,82 s |
| `npm run e2e -- --reporter=html,list,json` | falhou: 142 normais, 6 falhas esperadas L07, 6 skips existentes, 1 falha antiga; zero retries; banco limpo | 840,16 s |
| `python3 .runlogs/e2e-l07/rotate-dev-jwt.py` | chave local dev trocada e app saudável; verificação inicial falhou por KeyError organizationId no DTO de login | 14,22 s |
| `python3 .runlogs/e2e-l07/verify-jwt.py` | complemento passou: token anterior não expirado recebe 401 | 0,27 s |
| `python3 .runlogs/e2e-l07/proof.py` (final) | health/SPA/login QA e dev 200; modo 600 confirmado; QA sem restart | 1,12 s |

O reset local valida banco `DetranKanban_E2E` e rótulo Docker `prisma-dev`, para somente o app desse projeto, executa `scripts/e2e-create-database.sql` versionado no SQL próprio e inicia a imagem existente. Não executa e2e-reset-database.sql, não modifica schema/migrations, não toca QA ou produção.

Rodadas de construção encontraram seletor exato de label incompatível com selects, checkbox controlado assíncrono e Tab que não dispara blur no input date. Corrigidos preparo/sincronização, mantendo oracles de persistência. Focal de edição/checklist passou 3/3 incluindo setup (25,29 s). A primeira repetição geral foi interrompida após timeout de duplicar/arquivar/reativar; preservada como diagnóstico, não contabilizada como verificação concluída. Navegações redundantes desse caso foram removidas mantendo reload e consultas de persistência. Um focal subsequente (6 falhas / setup aprovado; 128,73 s) mostrou que Duplicar mantém o detalhe original aberto; adicionados fechamento pela UI e espera de saída do modal antes do reload/lista. A primeira rodada completa de repetição teve 34 aprovações normais, 18 falhas esperadas e três timeouts do mesmo caso (530,75 s). O erro primário preservado aponta o clique na lista enquanto o detalhe arquivado já estava reaberto. Fonte confirmada: invalidate do backlog remove o item/oculta o drawer antes de onSuccess terminar closeItem e limpar item da URL. closedTask passa a esperar ambos os estados antes do reload; sem aumentar timeout. Timeouts dos diagnósticos fecharam o contexto antes da limpeza; as fixtures residuais próprias foram removidas pelo reset dedicado final, sem tocar QA.

A rodada completa seguinte confirmou a correção do fechamento, mas teve três falhas de criação porque a asserção UI começava enquanto Boards ainda carregava (34 normais, 18 esperadas, 3 falhas; 481,56 s). A espera HTTP foi adicionada à entrada e ao reload; uma rodada intermediária interrompida (128,51 s) mostrou que faltava aguardar também a recarga. Focal de criação final passou 7/7 (117,03 s), sem aumentar timeout.

Uma extração inicial de trace incluiu cabeçalhos de autenticação locais na saída da ferramenta. O filtro foi corrigido para ações/rotas/status e os traces de diagnóstico foram removidos; nenhum valor foi incluído em arquivos versionados ou neste relatório. Ao final, somente a chave JWT privada do prisma-dev foi trocada e somente seu app foi recriado, preservando imagem/banco/senha. A prova inicial usou organizationId ausente no DTO de login; complemento utilizou o storage state local, sem imprimir valores, e confirmou token anterior ainda no prazo recusado com 401. Login novo segue 200. QA mantém chave/login e StartedAt original. O setup normal do próximo E2E renova o storage state antigo invalidado.

## Falha da suíte completa fora do L07

`rich-task-description.spec.ts:63`, Chromium mobile: timeout de 30 s (caso levou 32,5 s) no click para reabrir o primeiro item após fechar o drawer. É o mesmo ponto preservado na tarefa 2; reapareceu apesar do banco recriado. O snapshot ainda mostra o drawer aberto; sem trace de rede dessa execução, a causa exata não foi comprovada e não se atribui defeito de produto. Edição, imagem e salvamento ocorreram antes, mas as asserções de reabertura/persistência e restauração posteriores não foram executadas. Desktop desse spec passou. Teste anterior preservado, sem timeout aumentado ou test.fail para ocultar a falha. Os 18 casos L07 na suíte ficaram conformes (12 normais e seis falhas esperadas).

Evidência preservada localmente em `.runlogs/e2e-l07/failure-rich-mobile` (error-context, screenshot e vídeo); JSON/log/time completos em `.runlogs/e2e-l07`. `board-stage-sync` passou nos dois projetos no banco limpo. Próxima tarefa recomendada ao Maestro: investigar fechamento/reabertura do drawer antigo, seu deep link e sincronização, e depois retirar a dependência de seed desse spec, sem assumir a causa apenas pela semelhança com a corrida corrigida no L07.

## Limites e próximo passo

Sem produto/spec/schema modificado, rebuild desnecessário, backend/xUnit/Vitest/build SPA repetidos, Firefox/WebKit ou QA visual humano. Sem ponte/inferência IA em QA, serviços de produção, main, PR, merge, deploy ou agentes adicionais. Próximo passo: Maestro revisar o lote e encaminhar os três defeitos para análise/correção em escopo separado; Farol usa a instância QA sem interferência da recriação E2E.
