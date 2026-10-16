# Horas: validação da Frente 1, entrega horas-2

Complementa a [validação da tarefa 7](time-tracking-fixes-validation.md). O registro anterior descreve a decisão e a execução daquela data; a decisão posterior do PO sobre rateio está no GOALS da Frente 1 e na revisão de 2026-10-16 da [spec](../specs/time-tracking.md).

## Correções

- B07: o relatório organizacional filtra antes de agregar. Membros comuns veem apenas os próprios lançamentos; gestores, equipes que lideram; administradores, a organização atual. Negações explícitas de relatório são preservadas, sem consulta de permissão por equipe. Filtros de usuário/equipe não ampliam o acesso.
- Períodos são recortados pela interseção com cada dia de `America/Sao_Paulo`: diário, semana, relatórios, construtor de relatórios, indicadores de equipe e Meu Trabalho. Um período 22h–02h contribui duas horas em cada dia. A consulta também encontra períodos iniciados antes da janela. Frações de segundo são conservadas entre os recortes.
- O lançamento original continua único: não há duplicação ou alteração física dos registros. A listagem diária pode mostrar o mesmo ID nos dois dias; totais usam somente a parcela de cada dia. Editar ou excluir esse lançamento afeta ambos.
- Revisão horas-1, médio 1: criação manual e edição compartilham validação de período, futuro, observação, sobreposição e limite diário. Ajustes rápidos escolhem uma lacuna passada livre; dois ajustes não nascem mais às 09h sobrepostos. Correções de legado podem manter/reduzir cada conflito existente, inclusive deslocando o intervalo, mas não criar nem aumentar conflitos.
- QA-H05: a grade semanal mantém o primeiro dia alcançável no mobile e permite percorrer os sete dias. No desktop, continua centralizada quando cabe na área disponível.
- Os novos E2E comparam os contadores ativos do card e de Meu Trabalho ao `/total` real após recarga, além de provar B07, rateio, recusas temporais e ajustes consecutivos editáveis.

## Verificações

Ambiente exclusivo `prisma-qa`, app 3110, SQL 3111. xUnit usa banco próprio `Prisma_BoardColumns_Horas2`; E2E usa `PrismaWorkspace_QA`. Nenhuma operação foi executada no prisma-dev, banco da demonstração ou produção. Sem migration ou alteração do ModelSnapshot no diff contra a main.

| Comando | Resultado | Tempo de parede |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln --no-restore` | Passou; 0 erros, 3 avisos anteriores | 169,83 s (última rodada) |
| `bash .runlogs/horas-2/run-xunit-qa.sh` | xUnit completo 340/340, SQL real; sem skips | 65,99 s para `dotnet test` |
| `npx tsc -b` | Passou | 263,15 s |
| `npm run lint` | Passou; 12 avisos anteriores; repetido após ajustes de fixtures | 1,03 s (final) |
| `npm test -- --pool=threads --maxWorkers=1 --fileParallelism=false` | Vitest completo 155/155, 42 arquivos | 424,41 s |
| `npm run build` | Passou | 136,99 s |
| Build Docker padrão, revisão `1355a78` | Passou; inclui build do frontend e publicação .NET | 140,46 s |
| `npm run e2e -- time-tracking-fixes.spec.ts time-tracking-followup.spec.ts --retries=0 --reporter=line,json` | 23/23, desktop/mobile e setup; zero retries | 378,48 s |
| `npm run e2e -- time-tracking-followup.spec.ts --grep 'timer ativo após reload' --repeat-each=3 --retries=0 --reporter=line,json` | 13/13 com setup; 12 casos desktop/mobile, zero retries | 214,65 s |
| Quatro specs alterados, `--repeat-each=3 --retries=0`, desktop/mobile, mesmo banco | 85/85; zero skips, retries ou falhas; 78 limpezas sem falhas | 990,56 s |
| `npm run e2e -- --retries=0 --reporter=line,json` | 149 aprovados, 6 skips condicionais anteriores; zero falhas, flaky ou retries | 1.113,40 s |
| GET `/health` e POST `/api/auth/login` em 3110 | HTTP 200 em ambos | Smoke |

O runner xUnit privado valida explicitamente projeto, porta e nome do banco antes de inicializar o schema existente e executar a suíte; não usa o script antigo que aponta para prisma-dev. Arquivos de configuração e credenciais locais permanecem ignorados, com modo 600. Logs, JSON e capturas ficam em `.runlogs/horas-2`, sem versionar tokens ou estado autenticado.

A primeira tentativa da suíte E2E inteira recebeu SIGTERM (saída 143) perto do fim, após iniciar o caso 146/147, sem relatório final nem falha de teste registrada. Não conta como aprovação. A causa do sinal não foi isolada. A suíte foi repetida em processo destacado, com status em arquivo. O único projeto de fixture que ficou ativo na janela da interrupção foi identificado por prefixo UUID, horário e membro exclusivos, arquivado e teve seu membro desativado; nenhum dado do seed ou de outro executor foi limpo.

A segunda tentativa da suíte inteira foi interrompida após duas falhas: a antiga precondição de `board-stage-sync` (quadro D89 sem item aberto) e uma precondição nova de polling de tempo. Esta última exigia quatro segundos reais antes da recarga dentro dos cinco segundos padrão, com consultas que chegavam a intervalos de um segundo; a última amostra foi 123, antes de alcançar 124. A condição de quatro segundos foi preservada, com consultas a cada 250 ms e margem HTTP de oito segundos. Os dois contadores passaram três vezes em cada dispositivo (13/13 com setup), seguidos da rodada completa final. Nenhum relógio foi simulado nem comparação com `/total` enfraquecida.

### Estabilização dos testes de frontend

As primeiras rodadas completas expuseram notificações assíncronas de React Query que chegavam após buscas custosas por papel acessível no jsdom. Foram preservadas as asserções e os prazos. O teste de ReportBuilder aguarda o render em `act` e limpa seu QueryClient após cada caso; o setup usa as APIs públicas `notifyManager.setScheduler(queueMicrotask)` e `setNotifyFunction` com `act`, mantendo a notificação assíncrona. Cinco arquivos afetados passaram juntos (20/20, 103,76 s), seguidos da suíte completa 155/155. Tentativas anteriores não foram tratadas como aprovação.

### Tempos individuais finais, três repetições por dispositivo

Mínimo–máximo observado no mesmo banco, incluindo preparação e teardown. Não houve aumento do timeout padrão de 30 s. Os sete dias da semana foram divididos em três contratos para reduzir o tempo de cada cenário.

| Caso | Desktop | Mobile |
| --- | --- | --- |
| timer ativo após reload no card não fica acima de /total | 12.27–14.96 s | 18.43–22.23 s |
| timer ativo após reload no Meu trabalho não fica acima de /total | 9.54–14.86 s | 10.68–13.28 s |
| B07: relatório do membro contém só horas próprias; admin mantém o recorte autorizado | 4.98–6.18 s | 4.12–8.90 s |
| Brasília: período 22h–02h contribui 2h em cada dia no diário, semana e relatório | 3.39–6.47 s | 3.59–7.96 s |
| criação manual recusa futuro e sobreposição sem alterar o total anterior | 3.49–4.79 s | 4.10–6.07 s |
| dois ajustes pela UI escolhem períodos livres e permitem editar observação | 9.35–20.40 s | 10.91–19.55 s |
| Meu Tempo permite alcançar e ajustar segunda a quarta por toque | 10.77–16.85 s | 8.73–13.80 s |
| Meu Tempo permite alcançar e ajustar quinta e sexta por toque | 9.12–16.46 s | 11.02–12.00 s |
| Meu Tempo permite alcançar e ajustar sábado e domingo por toque | 8.48–15.61 s | 10.42–10.64 s |

A rodada completa final executou 155 casos previstos: 149 aprovados e seis skips anteriores de configuração/dispositivo. As 34 limpezas de fixtures terminaram sem falhas. `board-stage-sync`, `rich-task-description` mobile e os três casos antigos de IA passaram nos dois dispositivos. Os skips são dois de viewport, dois do setup institucional desabilitado neste ambiente e dois casos de convite exclusivos de desktop; nenhum skip foi adicionado.

## Limitações e decisões restantes

- Nenhum limite, parada automática ou alerta de cronômetro esquecido foi introduzido; depende do PO. O rateio não encurta o total trabalhado.
- Edição/exclusão de terceiros continua sem permissão administrativa específica: apenas o dono altera seus lançamentos.
- Não foram comprovadas corridas simultâneas entre criação/edição de lançamentos; validação de aplicação não equivale a uma constraint transacional de exclusão temporal.
- Ressalvas baixas da revisão anterior permanecem: relógio do navegador adiantado, busca sem debounce e seu acesso por projeto ainda N+1 (C6), dependência opcional do serviço de acesso na busca e horários sem data na apresentação de um intervalo que atravessa a meia-noite.
- Na repetição final, os novos cenários levaram no máximo 22,23 s. Os tempos individuais e a duração da limpeza estão preservados no JSON; não foram ampliados os prazos do corpo dos testes.
- QA independente e revisão deste SHA ainda são necessários. Firefox/WebKit, credenciais reais de provedores de IA e produção não foram testados.
