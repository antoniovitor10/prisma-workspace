# Tarefa 6 — revisão L06 e L08 de horas

Entrega: L06/L08 aprovados na repetição de três rodadas desktop/mobile, 145 normais e 18 falhas esperadas, zero inesperadas/retries, 1336,53 s, máximo 16,275 s. Na suíte completa final após SQL oficial limpo, os 54 resultados novos/alterados tiveram 48 normais e seis falhas esperadas, zero inesperadas, máximo 12,640 s. A suíte completa teve duas falhas antigas mobile e não está verde; detalhes abaixo. Produto/specs/schema preservados.

Branch L08: test/e2e-l08, avançada da revisão L06 em test/e2e-l07 / b84a549ab10300ad4009e35d9d30096c4f394ab7. Produto, specs, migrations e testes anteriores de descrição rica preservados. Somente os projetos Docker prisma-dev/prisma-qa são autorizados; a QA não é resetada.

## Casos e dados

L08 cobre sete jornadas normais (seis fluxos lógicos, com justificativa dividida): start/stop positivo; troca de tarefa; reload/idempotência; manual/validação/precisão/log; diário/criação de justificativa; remoção de justificativa; negação por falta de vínculo. Há três regressões de contrato isoladas: escopo do relatório do membro, sobreposição e separação por dia de Brasília.

- fixtures/time.ts reutiliza BoardFactory, cria usuário/projeto/quadros/colunas/tarefas com UUID e verifica ausência de timer inicial. Outro usuário e projeto próprios produzem o controle positivo da negação; o apontamento de 60 segundos é consultado novamente antes do teste.
- Finally encerra somente timer de tarefa criada pela fixture, remove justificativas próprias, remove vínculos, desativa membros e arquiva projetos. Contas Identity, registros TimeEntry e auditoria permanecem ligados a projetos arquivados; não há endpoint de exclusão de TimeEntry nem de conta nesse fluxo.
- Sem seed de negócio, dados de outro spec, sleeps, asserções opcionais, retries, aumento de timeout ou mudança de produto. Cada spec aplica orçamento de 20 segundos. O acesso administrativo inicial é a autenticação de teste configurada.
- Falhas conhecidas recebem test.fail somente depois das precondições, controles positivos e limpeza, para não mascarar erros da fixture. Uma correção de produto fará o caso passar inesperadamente e exigirá remover a marcação.
- Ambos os specs usam fixtures/functional.ts para tipografia de fallback no headless; somente Google Fonts CSS é substituído. API, scripts/ativos locais, relógio, dados e ações são reais. A navegação aceita requestfinished das requisições observadas após o início da navegação principal; refetch antigo/cancelado não satisfaz a espera. Os outros specs e a instância QA mantêm seu comportamento.

## Defeitos de produto comprovados

### B07 — relatório retorna pessoa/projeto sem acesso

1. Criar um membro Developer com vínculo ao projeto A e apontamento próprio de 120 segundos.
2. Criar projeto B e outro membro com apontamento próprio de 60 segundos nele, sem conceder vínculo ao primeiro membro.
3. Como primeiro membro, GET dos apontamentos de B retorna 404; POST start/manual e total de B também são recusados, sem criar registro.
4. GET /api/reports/hours no mesmo período retorna 200 e inclui o projeto B e o usuário de B, contrariando o escopo do usuário comum em SPEC-DASHBOARDS-REPORTS e segurança de SPEC-TIME-TRACKING.

Evidência: controle positivo persistido; assert final exige lista vazia de projetos proibidos e IDs de terceiros. OrganizationHoursReportFeature não usa ActorId para limitar a agregação; ReportRepository.GetOrganizationTimeEntriesAsync filtra organização e período, sem o acesso do ator. Produto não corrigido.

### Sobreposição aceita

Um intervalo 09:00:00–09:01:01 de Brasília grava 61 segundos. Outro 09:00:30–09:01:30 do mesmo usuário/tarefa retorna 201: dois registros e total de 121 segundos. O contrato exige recusa e conservação de um registro/61 segundos. Gap 2 já documentado na spec; CreateManualTimeEntryCommandHandler não consulta os períodos existentes. Produto não corrigido.

### Meia-noite de Brasília mal distribuída

Intervalo 2026-10-12 23:59:30 até 2026-10-13 00:00:30, UTC-03:00: total correto de 60 segundos. Diário devolve [0,60] e semana [60,0]; o contrato exige [30,30] em ambas as projeções. GetMyDailyByTaskQueryHandler usa limites UTC, e GetMyWeeklyTimeQueryHandler atribui todo o intervalo ao dia inicial. Gaps 3/4 da spec; produto não corrigido.

## Dúvidas e sugestões

- D62 não explicita a reabertura dos descendentes fora da coluna: preservado o as-built já documentado no L06, mantendo esses itens concluídos e na coluna original.
- Não há SLA de carga aprovado. No banco histórico, 215 projetos arquivados ainda contribuíam com 433 dos 446 quadros em GET /api/Boards; a listagem admin levou 4,01 s, contra 0,059 s/seis projetos na lista ativa. GetAccessibleProjectIdsAsync consulta projeto por projeto. Confirmar a regra de visibilidade dos quadros arquivados e planejar consulta em lote; não alterado nesta tarefa.
- Mostrar motivo temporal específico na UI: hoje o erro do fim igual aparece como “Erro de validação”. O teste exige tipo/status da resposta, alerta exato e nenhum registro persistido.
- Exibir segundos para tornar a conferência de períodos curtos possível pela UI. A precisão foi comprovada via timestamps/total da API: 120 + 61 = 181 segundos, sem blocos de minutos.
- Usar o nome disponível no DTO do apontamento no resumo: a consulta global de responsáveis pode retornar 403 para Developer, e o resumo atual recorre ao ID. Não alterado.

## Diagnósticos e correções dos testes

- L06: 17 casos técnicos após splits de exclusão/cancelamento, recusa/transferência de quadro, guardas API/UI, conclusão/reabertura e recursão profunda; recusas API 400 com estado conservado; contador de PUT da UI igual a zero; editor novo/alerta ausente antes da segunda tentativa. Consentimento verdadeiro também comprovado no payload da UI.
- Primeira repetição L06 diagnóstica: 60 aprovações, um timeout de reload, 744,63 s; reabertura de 20,30 s dividida. Focais posteriores 9/9 (82,43 s), 5/5 (36,29 s) e 25/25 (164,98 s, máximo 10,51 s).
- Primeiro focal L08: 4 normais, 3 falhas esperadas e 4 falhas de teste (82,60 s); segundo: 6 normais, 3 esperadas e 2 falhas (56,73 s). Corrigidos locator status ambíguo com autosave, endpoint de hidratação active-timer da Topbar, enriquecimento de displayName no GET e expectativa de mensagem detalhada ausente no contrato transportado atual.
- Focal desktop/mobile: 14 normais, 6 esperadas e um timeout de espera do diário (158,51 s). Diário/justificativas repetidos três vezes: 13/13 em 141,55 s; espera de quadro foi reforçada por dados/estado próprios.
- Rodada conjunta no histórico interrompida por SIGINT somente no runner próprio: 32 normais, 3 esperadas, 5 falhas de carga, um caso interrompido (401,15 s). Sem mudar produto; reset inicial via SQL versionado, projeto/container/banco guardados.
- Rodada intermediária após reset: 112 normais, 18 esperadas, 3 falhas de navegação (1002,41 s). A espera podia selecionar refetch do documento antigo, inclusive response null. Diagnóstico de 31 reproduções (sem headers/bodies/tokens) passou sem pageerror; não foi comprovado crash de produto. A barreira inicial usava timings/requestfinished, sem ignorar HTTP de erro; a versão final identifica as referências de requests após o evento da navegação principal, evitando comparação entre relógios de requisições distintas. Focal final 19/19, três vezes por dispositivo, 203,35 s, máximo 17,80 s.
- Lint adicional detectou throw em finally na factory nova e nome use confundido com hook React; falhas agora preservadas fora do finally e callback chamado providePage. Sem alteração de comportamento do produto.

- Rodada conjunta posterior: 131 normais, 18 esperadas e dois timeouts L06 (1429,96 s), nenhum erro inesperado L08. Um caso de exclusão passou em 20,04 s; recusa/transferência e guardas API/UI foram divididas. Focal das quatro divisões 23/25 (299,26 s), ainda com dois timeouts na confirmação que navegava novamente; focal posterior 7/7 (aproximadamente 102 s), máximo 16,62 s. Outra repetição foi interrompida no runner próprio após dois novos timeouts desse fluxo, 638,00 s; não considerada aprovação. A versão final usa o destino selecionado pela UI e consultas reais ao servidor para persistência, preservando todas as asserções e eliminando navegações redundantes.

## Verificações finais

Comandos frontend no Node 22 do host. E2E no Chromium da imagem oficial Playwright 1.62.1 / Node 24.18.1, um worker, CI=false/retries=0. A repetição usa o mesmo API/banco dev, por TCP transparente/Unix; a suíte completa usa réplica da imagem exata da API no namespace do runner, com o mesmo banco E2E. O provedor falso dos specs antigos de IA precisa ser alcançável no loopback pela API.

| Comando | Resultado | Tempo |
| --- | --- | --- |
| npx tsc -b | passou | 69,20 s |
| npm run lint | passou, 0 erros/12 avisos anteriores | 0,76 s |
| npx tsc --ignoreConfig --target ES2022 --module ESNext --moduleResolution bundler --strict --skipLibCheck --types node --outDir ../../.runlogs/e2e-l08/typecheck e2e/time-tracking-l08.spec.ts e2e/boards-columns-l06.spec.ts | passou | 6,63 s |
| node_modules/.bin/oxlint -D correctness e2e/time-tracking-l08.spec.ts e2e/boards-columns-l06.spec.ts e2e/fixtures/time.ts e2e/fixtures/functional.ts | passou | 1,30 s |
| SQL oficial via .runlogs/e2e-l08/reset-clean.sh antes da repetição | passou: 28 migrations existentes/1 projeto seed | 38,26 s |
| PRISMA_E2E_RUN_NAME=repeat-isolated bash .runlogs/e2e-l08/run-isolated.sh e2e/boards-columns-l06.spec.ts e2e/time-tracking-l08.spec.ts --repeat-each=3 | passou: 145 normais/18 falhas esperadas/0 inesperadas/0 retries; desktop e mobile, sem reset entre repetições | 1336,53 s |
| SQL oficial antes da primeira suíte isolada | passou: 28/1 | 39,55 s |
| PRISMA_E2E_RUN_NAME=full-isolated bash .runlogs/e2e-l08/run-isolated.sh | falhou no ambiente: 163 normais/12 esperadas/2 setups de IA falhos/6 skips anteriores/14 não executados por serialidade | 903,78 s |
| SQL oficial antes da suíte final com réplica | passou: 28/1 | 27,12 s |
| SQL oficial antes da rodada final validada | passou: 28/1 | 24,14 s |
| PRISMA_E2E_RUN_NAME=full-final bash .runlogs/e2e-l08/run-replica.sh | falhou: 177 normais/12 falhas esperadas/6 skips/2 falhas antigas mobile; novos/alterados 48 normais+6 esperadas, 0 inesperadas/0 retries | 964,61 s |

A primeira suíte isolada não foi considerada aprovação: a API do host não alcançava o servidor falso que ai-assistant.spec.ts cria no loopback do runner. A réplica usa o mesmo image ID e env do prisma-dev, mudando somente URLs/porta para 3115 interna; SQL 3102 é encaminhado por Unix/TCP transparente. A porta interna 3103 fica livre para o provedor falso do spec antigo. Sem mudar produto, specs, testes antigos ou respostas de API. Uma tentativa de réplica foi interrompida após colisão interna 3103 (280,78 s); o transporte dessa porta foi retirado. Focal de IA primeiro falhou por pasta de screenshots ausente (23,45 s); depois da pasta preparada, passou 3/3 desktop/mobile em 21,47 s. A rodada final inicia de novo após SQL oficial recriado (24,14 s), 28 migrations/1 projeto seed.

## Tempos por teste

Máximo de cada caso nas três repetições, em segundos, incluindo preparação/limpeza do resultado Playwright. Máximo bruto conjunto: 16,275 s, todos abaixo de 20. Falhas esperadas são regressões de contrato, não cenários aprovados do produto.

boards-columns-l06.spec.ts

| Linha / caso | Desktop máx. | Mobile máx. |
| --- | --- | --- |
| 60 — L06 cria quadro pela UI, copia somente colunas e persiste após recarga | 12.70 s | 14.76 s |
| 92 — L06 cancelar exclusão de quadro preserva quadro e tarefas após recarga | 12.71 s | 15.21 s |
| 108 — L06 excluir quadro ocupado sem destino é recusado pela UI e preserva tarefas | 9.21 s | 14.33 s |
| 131 — L06 confirmar exclusão de quadro mostra destino na UI e persiste transferência | 11.27 s | 16.27 s |
| 163 — L06 criar coluna pela UI persiste nome e categoria após recarga | 10.53 s | 14.45 s |
| 180 — L06 renomear coluna pela UI conserva categoria e persiste após recarga | 9.47 s | 15.78 s |
| 197 — L06 excluir coluna vazia: cancelar preserva; confirmar remove sem afetar outras | 9.43 s | 14.01 s |
| 215 — L06 excluir coluna ocupada: API exige destino; UI informa impacto e cancelar preserva tarefas | 10.85 s | 9.06 s |
| 239 — L06 confirmar exclusão ocupada transfere os mesmos IDs e persiste após recarga | 11.00 s | 10.82 s |
| 260 — L06 D62: API recusa reclassificação sem consentimento da categoria ou descendentes | 4.10 s | 4.11 s |
| 278 — L06 UI exige ambos os consentimentos com alerta novo e zero PUTs; cancelar preserva | 9.96 s | 9.53 s |
| 305 — L06 concluir pela UI consente descendência externa e persiste estado e histórico | 12.18 s | 14.10 s |
| 332 — L06 conclusão via API alcança neto externo, preserva colunas e histórico individual | 8.25 s | 6.06 s |
| 352 — L06 reabrir: API e UI exigem confirmação; cancelar preserva tarefas concluídas | 13.21 s | 8.62 s |
| 374 — L06 reabrir coluna ocupada pela UI persiste estado e histórico das tarefas nela | 10.69 s | 13.33 s |
| 394 — L06 reabertura via API preserva descendentes concluídos fora da coluna | 4.63 s | 4.33 s |
| 411 — L06 leitor não cria nem exclui coluna pela UI ou API | 12.11 s | 13.92 s |

time-tracking-l08.spec.ts

| Linha / caso | Desktop máx. | Mobile máx. |
| --- | --- | --- |
| 54 — L08 cronômetro start/stop pela UI grava um intervalo positivo e total exato | 7.66 s | 7.68 s |
| 70 — L08 trocar tarefa na UI encerra o anterior e mantém somente um cronômetro ativo | 6.41 s | 6.68 s |
| 87 — L08 reload restaura cronômetro ativo; reiniciar a mesma tarefa é idempotente | 7.65 s | 12.94 s |
| 102 — L08 manual na UI recusa fim igual, persiste observação e totaliza segundos sem blocos | 12.95 s | 9.13 s |
| 135 — L08 diário mostra horas próprias e justificativa da UI persiste em consulta nova | 9.39 s | 11.23 s |
| 153 — L08 remover justificativa pela UI persiste sem alterar horas da tarefa | 7.79 s | 10.82 s |
| 168 — L08 negação: sem vínculo não inicia timer, lança horas nem consulta apontamentos | 5.01 s | 5.35 s |
| 187 — L08 relatório do membro não deve expor horas de pessoa ou projeto sem acesso | 5.69 s | 5.36 s |
| 209 — L08 períodos: apontamento sobreposto deve ser recusado sem aumentar o total | 3.53 s | 3.46 s |
| 223 — L08 Brasília: diário e semana dividem intervalo na meia-noite local | 3.98 s | 3.83 s |

Suíte completa final: | Linha / caso | Desktop máx. | Mobile máx. |
| --- | --- | --- |
| 60 — L06 cria quadro pela UI, copia somente colunas e persiste após recarga | 12.64 s | 6.32 s |
| 92 — L06 cancelar exclusão de quadro preserva quadro e tarefas após recarga | 12.26 s | 6.50 s |
| 108 — L06 excluir quadro ocupado sem destino é recusado pela UI e preserva tarefas | 6.00 s | 5.35 s |
| 131 — L06 confirmar exclusão de quadro mostra destino na UI e persiste transferência | 6.60 s | 6.93 s |
| 163 — L06 criar coluna pela UI persiste nome e categoria após recarga | 4.65 s | 7.85 s |
| 180 — L06 renomear coluna pela UI conserva categoria e persiste após recarga | 5.44 s | 6.29 s |
| 197 — L06 excluir coluna vazia: cancelar preserva; confirmar remove sem afetar outras | 5.31 s | 6.07 s |
| 215 — L06 excluir coluna ocupada: API exige destino; UI informa impacto e cancelar preserva tarefas | 4.90 s | 5.42 s |
| 239 — L06 confirmar exclusão ocupada transfere os mesmos IDs e persiste após recarga | 5.59 s | 6.34 s |
| 260 — L06 D62: API recusa reclassificação sem consentimento da categoria ou descendentes | 5.29 s | 3.74 s |
| 278 — L06 UI exige ambos os consentimentos com alerta novo e zero PUTs; cancelar preserva | 6.41 s | 5.94 s |
| 305 — L06 concluir pela UI consente descendência externa e persiste estado e histórico | 6.95 s | 6.70 s |
| 332 — L06 conclusão via API alcança neto externo, preserva colunas e histórico individual | 3.54 s | 3.38 s |
| 352 — L06 reabrir: API e UI exigem confirmação; cancelar preserva tarefas concluídas | 4.58 s | 5.35 s |
| 374 — L06 reabrir coluna ocupada pela UI persiste estado e histórico das tarefas nela | 5.54 s | 10.42 s |
| 394 — L06 reabertura via API preserva descendentes concluídos fora da coluna | 3.02 s | 4.20 s |
| 411 — L06 leitor não cria nem exclui coluna pela UI ou API | 6.34 s | 9.41 s |

time-tracking-l08.spec.ts

| Linha / caso | Desktop máx. | Mobile máx. |
| --- | --- | --- |
| 54 — L08 cronômetro start/stop pela UI grava um intervalo positivo e total exato | 4.68 s | 5.31 s |
| 70 — L08 trocar tarefa na UI encerra o anterior e mantém somente um cronômetro ativo | 4.49 s | 5.56 s |
| 87 — L08 reload restaura cronômetro ativo; reiniciar a mesma tarefa é idempotente | 5.05 s | 6.04 s |
| 102 — L08 manual na UI recusa fim igual, persiste observação e totaliza segundos sem blocos | 5.39 s | 6.56 s |
| 135 — L08 diário mostra horas próprias e justificativa da UI persiste em consulta nova | 5.71 s | 9.22 s |
| 153 — L08 remover justificativa pela UI persiste sem alterar horas da tarefa | 5.00 s | 7.68 s |
| 168 — L08 negação: sem vínculo não inicia timer, lança horas nem consulta apontamentos | 3.54 s | 4.25 s |
| 187 — L08 relatório do membro não deve expor horas de pessoa ou projeto sem acesso | 3.83 s | 5.07 s |
| 209 — L08 períodos: apontamento sobreposto deve ser recusado sem aumentar o total | 2.21 s | 2.83 s |
| 223 — L08 Brasília: diário e semana dividem intervalo na meia-noite local | 2.30 s | 2.54 s |

Maior duração nos 54 resultados novos/alterados: 12,640 s (L06); L08 máximo 9,219 s. Todos os novos casos mantiveram o orçamento de 20 s.


## Limites

Não repetidos backend build/xUnit, Vitest, build SPA, Context Explorer/Python ou browsers Firefox/WebKit/QA visual humano. Não testados edição/exclusão de TimeEntry (endpoints inexistentes), limite diário de 24 horas isoladamente, concorrência de início de timer ou negação entre organizações; a negação usa projeto sem acesso na mesma organização. Relatório/negação consultados por API. Não há inferência IA externa nesta tarefa.

A falha antiga rich-task-description.spec.ts:63 reapareceu em chromium-mobile: timeout de 30 s (resultado 33,096 s, incluindo encerramento), ao reabrir o detalhe. Desktop passou em 8,3 s. Sem correção ou marcação nessa tarefa, conforme orientação.

Outra falha fora do lote: ai-assistant.spec.ts:149 em chromium-mobile não encontrou a citação de tarefa no caso de catálogo, após 20 s (24,626 s no resultado). Setup, chamadas ao provedor falso e casos anteriores desse spec já tinham passado na rodada final. Causa ainda não isolada; não foi atribuída como defeito do produto e nenhum teste antigo foi alterado. O spec usa board[0] e dados compartilhados do seed; dependência/ordem deve ser investigada em recorte próprio, sem concluir retrospectivamente a causa.

A suíte completa final não está verde. O L06 e o L08 passaram suas três repetições e todos os resultados normais na suíte completa; os contratos de produto declarados como test.fail seguem falhando exatamente como documentado.

Evidências locais ignoradas: .runlogs/e2e-l08 (logs, JSON, tempos, diagnóstico sanitizado). Nenhum segredo versionado ou incluído no relatório. Scripts de reset limitados a prisma-dev, banco DetranKanban_E2E e SQL oficial; app permanece em 3100 e QA independente em 3110.


## Driver local e reprodução

Os drivers são locais e ignorados em .runlogs/e2e-l08. Não alteram o CI nem requerem biblioteca nova do produto. tcp-proxy.cjs host conecta somente aos serviços próprios de localhost 3100 e 3102; sockets Unix privados são modo 600. O container prisma-dev-e2e-runner usa --network none, UID/GID do agentes, sem porta publicada. Uma pasta de artefatos gerados em .runlogs/e2e-l08/local-artifacts é montada em /.local para as screenshots relativas do spec de IA, sem carregar recuperação privada. O runner lê a .env.e2e.local existente e mantém credenciais fora dos relatórios. A réplica prisma-dev-e2e-api compartilha seu namespace, usa exatamente o image ID de prisma-dev-app-1 e o banco DetranKanban_E2E. Sua env local ignorada é .env.server-e2e-isolated, modo 600.

Receita após prisma-dev ativo: iniciar os dois proxies host com tcp-proxy.cjs (http/3100, sql/3102); executar start-replica.sh; aguardar State.Health.Status=healthy; executar PRISMA_E2E_RUN_NAME=full-replica bash .runlogs/e2e-l08/run-replica.sh. Esse script executa npm run e2e completo no container com playwright-replica.config.ts; somente URLs apontam para o API interno 3115. Finalizar somente os dois containers com label prisma-dev e os processos de proxy próprios.

O host apresentou net::ERR_NETWORK_CHANGED em requisição real. A origem da alteração não foi identificada. O isolamento remove a exposição do navegador à rede do host; os GET/POST/PUT/DELETE, WebSockets, dados e permissões seguem reais. Na confirmação de exclusão, o GET das tarefas do destino é aguardado e seus IDs conferidos antes da seleção da UI. A antiga espera de cinco segundos concorria com a listagem ainda carregando; isso foi corrigido por sincronização HTTP, sem aumentar timeout.

Referências: [Docker network none](https://docs.docker.com/engine/network/drivers/none/), [Docker e versão do Playwright](https://playwright.dev/docs/docker), [Chromium sobre alterações de rede](https://chromium.googlesource.com/chromium/src.git/+/refs/tags/126.0.6478.162). A necessidade de compartilhar loopback com o provedor falso foi confirmada no código existente e no setup falho, não é defeito do produto.


## Estado final do servidor

Containers temporários prisma-dev-e2e-api/prisma-dev-e2e-runner e os proxies próprios foram encerrados ao terminar. A env da réplica fica ignorada, modo 600, para recuperação local; nunca imprimir seus valores. Dev app/API permanece em 0.0.0.0:3100 e QA em 3110. GET /health, SPA e login retornaram 200 nos dois perfis; QA preservou StartedAt=2026-10-14T04:52:15.985221704Z. Arquivos de login QA permanecem modo 600. Sem produção, main, PR, deploy, alteração de firewall ou agentes adicionais.

Parte A enviada em test/e2e-l07 / b84a549ab10300ad4009e35d9d30096c4f394ab7. Parte B tem somente docs, README e factory/spec de horas; sem mudanças no produto, specs, migrations, snapshot ou testes antigos.
