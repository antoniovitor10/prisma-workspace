# Reconciliação feat/main — tarefa 3

Em 2026-10-13, Tear preparou `integration/ai-foundation-main`, da feat `457c6d2`, mesclando main `6eecba8` com commit de merge, sem rebase. PR, merge na main e publicação ficam com Maestro. Nenhum código novo de produto ou migration criado nesta reconciliação.

## Conflitos e resolução

Comparadas as versões de ambos os pais e a SPEC-AI-004 aprovada, com esclarecimento humano de 2026-10-07. Os arquivos de código/testes resolvidos coincidem com a feat porque a comparação mostrou a base publicada da main integralmente presente e as diferenças limitadas às correções posteriores aceitas. Nenhuma correção exclusiva da main foi descartada. O transporte CLI com Content-Length, onboarding, recuperação do seletor após blur e Gemini permanecem.

| Arquivo em conflito | Resolução |
| --- | --- |
| DECISIONS.md | União das decisões e atualizações; D1–D98 em ordem crescente, sem IDs repetidos. Proposta e aprovação da mesma D81 reunidas em um registro com atualização, mantendo os dois textos. D89/D90/D93 reposicionadas sem mudar conteúdo. Não existia colisão entre decisões diferentes. |
| PROGRESS.md | Mantidas entradas dos dois pais, mais novas no topo, incorporando as confirmações da main de 13 E2E/38,2 s e 11 E2E. Mesma entrada histórica com autoria renomeada na feat mantém a identificação Orca da main, sem duplicar. |
| ROADMAP.md | Estado posterior publicado da feat preservado e estado anterior da main identificado como histórico. |
| backlog.md | Status implemented posterior preservado; in_progress da main conservado como comentário histórico, sem criar campos YAML duplicados. Registros anteriores da main mantidos como histórico. |
| specs/ai-conversation-experience.md | Contrato publicado comum preservado; esclarecimento aprovado Recusar e avisar e registro de validação/publicação da feat mantidos. Sem requisito novo ou alteração de status. |
| Infrastructure/Services/Ai/AiConnectionOnboarding.cs | Base publicada (provedores, paginação, catálogos, CLI) mantida; feat normaliza URL vazia, valida estrutura JSON e limita fallback manual a 404/405, sem usar falha temporária como catálogo. |
| Infrastructure/Services/Ai/AiConversationSelection.cs | Seleção por conversa, preços/envelope, guard e cache da main mantidos; feat diferencia vazio/indisponível, recusa envio com 409, mantém detalhes CLI só para admin e calcula disponibilidade local/cache na listagem sem consultar catálogo externo. |
| Infrastructure/Services/Ai/AiService.cs | Operações/chat publicados mantidos; feat propaga autoridade administrativa, integra disponibilidade e validação e neutraliza falhas CLI para usuário comum. |
| Web/src/features/ai/AiAssistant.tsx | Chat/streaming/painel da main mantidos; feat inclui conversa criada no histórico, reconcilia PATCH falho e limpa rascunho/insere bolhas só após validação HTTP. |
| Web/src/features/ai/AiConversationControls.tsx | Seletores e padrão publicados mantidos; feat exige catálogo pronto e sem fetch para liberar opções, preserva modelo salvo e não declara remoção por falha/refetch. |
| Web/src/features/ai/AiConnectionForm.test.tsx | Casos publicados preservados e regressão da feat para catálogo OpenRouter inicial indisponível/nova tentativa. |
| Web/src/features/ai/AiConversationControls.test.tsx | Casos publicados preservados e regressões da feat para indisponibilidade/refetch/cache antigo. |
| Web/e2e/ai-assistant.spec.ts | Fluxos publicados preservados, com regressão aceita de catálogo/rascunho da feat, verificação de metadados comuns, espera de fim da resposta, cota diária independente de histórico e porta parametrizada do servidor. |
| tests/Prisma.Workspace.Tests/AiFoundationTests.cs | Casos publicados preservados; fakes ampliados e regressões aceitas da feat para recusa sem inferência/cache, catálogo inválido, acesso administrativo, disponibilidade local/listagem e URL OpenRouter vazia. |

Infrastructure/Web na tabela abreviam `src/Prisma.Workspace.Infrastructure` e `src/Prisma.Workspace.Web`.

## Verificações

Tempos de parede com `/usr/bin/time`; logs locais ignorados em `.runlogs/integration`. Compilações/testes independentes executados em paralelo; duração reflete a carga do servidor.

| Comando | Resultado | Tempo |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln` | passou, 0 erros / 3 avisos anteriores | 59,76 s |
| `scripts/test-server-dev.sh` | xUnit 311/311, zero skips | 59,07 s |
| `npx tsc -b` | passou, exit 0 | 93,55 s |
| `npm run lint` | passou, 0 erros / 12 avisos anteriores | 0,90 s |
| `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` | Vitest 145/145 em 39 arquivos | 254,14 s |
| `npm run build` | passou; avisos existentes de anotação de pureza do SignalR | 106,86 s |
| `npm test` em tools/context-explorer | 42/42 | 0,91 s |
| `python3 -m unittest discover -s scripts/tests` | 8/8 | 0,28 s |
| `python3 infra/ai-bridge/test_bridge.py` | 17/17 | 0,44 s |
| `docker exec ... sqlcmd ... -i /tmp/prisma-task3-e2e-create.sql` | SQL versionado executado, banco E2E recriado | 1,89 s |
| `scripts/run-local.sh up` | imagem reconstruída, app/SQL saudáveis | 177,15 s |
| `npm run e2e` | passou, 119 aprovados / 6 skips, zero retries | 639,41 s |
| `bash scripts/ci-e2e.sh prisma-dev-app:local` (cópia isolada e invólucro de rótulos) | passou, 119 aprovados / 6 skips, zero retries | 797,05 s (preparação, npm ci, suíte e limpeza) |

As falhas da tarefa 2 desapareceram nas duas rodadas com banco limpo: board-stage-sync desktop/mobile e rich-task-description mobile passaram, sem alterar esses specs. Para rich-task-description isso confirma ausência de reprodução nesta rodada; não determina retrospectivamente a causa exata do timeout anterior. A comparação final `git diff --name-only origin/main..HEAD` não contém nenhum arquivo em Migrations ou ModelSnapshot; os 28 arquivos de histórico aplicados são os existentes. Após a rodada CI, somente app/SQL persistentes do prisma-dev permaneceram ativos e saudáveis.

## Ambiente e limites

SQL E2E recriado exclusivamente no container `prisma-dev-database-1` pelo arquivo versionado `scripts/e2e-create-database.sql`; inicialização da imagem aplica migrations existentes e seed oficial. Nenhum banco institucional tocado. App da branch servido em 0.0.0.0:3100 por `scripts/run-local.sh up`.

Script CI executado em cópia temporária dos arquivos do índice Git, sem `.env.e2e.local` ou segredos locais. Um invólucro Docker local acrescenta apenas `com.docker.compose.project=prisma-dev` aos containers/rede que o script cria; o conteúdo de `scripts/ci-e2e.sh` permanece igual ao versionado. Nomes únicos do próprio script, SQL descartável, rede interna, zero portas publicadas. Segredos aleatórios do script permanecem externos ao Git. Artefatos dessa rodada ficam na cópia temporária ignorada; app principal em 3100 é preservado.

Sem QA visual humano, contas CLI reais, inferência paga, login no proxy público, PR, merge na main ou deploy. Próximo passo: revisão do Maestro e decisão de PR/integração.
