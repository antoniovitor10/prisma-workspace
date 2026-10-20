## [2026-10-20] — Oficina — alcance de todos os dias da semana
- Contraprova1FAILmobile/2controles; corrigido3PASS27,6s em1440/390 com14consultas200 por largura e cliques normais em todos os sete dias. Capturas antes/depois mobile e desktop abertas.
- Mesmo CSS safe center do PR30; não declarar todos os timeouts resolvidos. Full anterior223/8/40 preservado e nova rodada pendente. Sem migration/produção; revisão e QA oficiais pendentes.

## [2026-10-20] — Oficina — D13 validado após retificação
- Contraprova popup2FAIL/3controles; corrigido5PASS36,8s em1440/390. Capturas antes/depois mobile e depois desktop abertas, todos os checkboxes e Salvar visual contidos.
- Histórico25/2 e completo223/8/40 preservados; não declarar branch toda verde. Full corrigido e semanal continuam enfileirados. Sem migration/produção; revisão e QA oficiais pendentes.

## 2026-10-20 — Oficina: full QA revelou oito falhas, correções pendentes de prova

Full223PASS/8FAIL/40skips. L07 lê resposta de documento anterior; manualL08 agora recebe mensagem específica apósC5; popupD13 fica emx-30; grade semanal center torna dias iniciais inalcançáveis no mobile. Correções propostas e regressão dos sete dias em andamento; ainda não são verdes. Corrigida leitura anterior: controles25PASS/2FAIL, não27. Worker futuro propaga exit nativo. Backend724/33 inalterado; frontend/nativos novos enfileirados. Relatório visual mantém todos os limites e falhas.

## 2026-10-20 — Oficina: fechamento local C5

D99 integrado e focal nativo5PASS em36,8s com grafo efetivamente carregado. Capturas1440/390 inspecionadas; nome no primeiro vínculo comprovado porAPI/SQL. Relato canônico oficina-qa-5.md preserva falha inicial400 sem Errors e contraprova tipada1FAIL/9PASS→10PASS. Gates combinados724/33 e173 frontend; fullE2E em execução. Sem migration; revisão/QA oficiais pendentes.

## 2026-10-20 — Oficina: evidências de C1, C3 e C4

Relatórios por grupo consolidados com resultados reais, contraprovas e auto-QA1440/390. Backend completo724PASS/33skips existentes; frontend173PASS/45 arquivos, tipos forçados, lint e builds aprovados. C3/C4 nativo27PASS e D14/D20 fechamento5PASS após3 falhas esperadas. C1 focal5PASS e Brasília5PASS. E2E completo enfileirado; C5 captura do grafo reforçada para aguardar conteúdo. Wiki/Topbar têm ressalvas registradas, sem alegar correção universal. Revisão e QA oficiais pendentes.

## [2026-10-20] - Codex (Oficina) - rodada QA em branch isolada, validação em andamento

- C5 integrado localmente; Auth combinou nome completo e orientação do administrador. Build conjunto, reinício da API3120 própria e E2E final ainda pendentes.
- Primeiro nativo C5:3PASS/2FAIL; nome inválido recebia400 sem erros por campo. Serialização de ValidationErrorResponse corrigida, com regressão de mensagens/privacidade:10PASS, contraprova1FAIL/9PASS e restauração10PASS. Novo nativo após a correção ainda pendente; integração local agora realizada.
- C5 em scratch próprio sobre3f973c1:cadastro completo com claim provisória transacional, transferência ao membership e timeline sobre dados existentes com grafo complementar; ocultações leadtime/automações só na UI. Backend37/37(incluiSQL), frontend16/16, tsc-b/lint/build verdes. Contraprova8FAIL/4PASS; fontes restauradas, rebuild final12/12PASS. E2E/contraprova frontend/fullbranch pendentes; relatório específico relatorios/oficina-qa-c5.md. Não é ENTREGA concluída.
- Complemento C5: tooltip de permanência oculto também no grafo complementar. SQL da associação por convite passou; afetados finais38 backend/17 frontend, tipos app/E2E, lint e build verdes. Contraprova frontend4FAIL e restauração4PASS. Native próprio C5 ainda pendente.

- C3/C4:status longo da fila passa a quebrar em duas colunas no celular; gráficos preparados recebem nomes pelo título, mantendo acessibilidade de teclado do Recharts. Baseline nativo novo confirmou3 falhas(D14 nas duas larguras e D20 mobile423>390),2 controles aprovados. Fechamento local31Vitest/7 arquivos, tipos app/E2E,lint/build aprovados; nativo após correção ainda pendente. Captura C1 agora espera a opção carregada e verificável por click trial antes da foto.

- Goal Oficina autorizado pelo PO: base `a2797a7`, worktree `oficina-qa`, branch `fix/qa-round-1`; merges de L06, L07 e L08 em ordem, preservando ambos os lados dos conflitos exclusivamente documentais. Sem produção, main, deploy, migration nova ou agentes adicionais.
- C1: subtarefa exige seleção explícita de responsável elegível e envia o projeto/responsável no comando; `ArgumentException` usa Problem Details 400; dependências passam a teste positivo de autocomplete/vínculo conforme D99. Padrão de 30s do Playwright restaurado nos lotes L06/L08 conforme o goal.
- Verificação inicial: middleware 3/3; contraprova com versão anterior 2 falhas 400/500 e 1 controle passando; versão corrigida novamente 3/3. Detalhe Vitest 9/9; contraprova falha porque o botão antigo permite adicionar sem responsável. `tsc -b` passou após corrigir uso indevido de `exact` no teste; lint sem erros. Build frontend passou; HTTP real 5/5 rejeições 400 sem efeitos colaterais. E2E aguarda trava compartilhada. Não é conclusão da entrega.
- C6 adiantado durante espera de browser: listagem geral de quadros exclui projetos arquivados; acesso e permissões por projeto usam lote, preservando negações, membership ativa, equipe ativa, administrador e escopo de token. Foco 17/17 passou com SQL real e matriz de perfis/permissões. Contraprova com código anterior: duas falhas esperadas, incluindo crescimento de 6 para 145 consultas; correção restaurada e novo 17/17 verde. As duas falhas iniciais de fixture foram corrigidas informando OwnerId e usando contexto próprio por tenant. E2E C6, autorrevisão final e auto-QA pendentes; não é ENTREGA concluída.
- C1 Brasília: agregações diária/semanal passam a recortar sobreposições por dia de Brasília, com transporte de frações sem perder segundos. Cinco casos passaram; restaurar handlers/repositório antigos produziu cinco falhas reais. O script esperava quatro falhas e um controle e encerrou por essa diferença, depois de restaurar as fontes em finally. Rebuild final e cinco casos verdes. E2E Brasília ainda pendente; C1 focado anterior9/9PASS. Não houve merge do restante da branch de horas.
- C2–C4 checkpoint:controles responsivos/rótulos/Escape/orientações/cancelamento implementados parcialmente;27Vitest,tsc-b/lint/buildPASS. ContraprovaC4:2FAIL,restauração e2PASS;10casos apenas fora do filtro.34capturas baseline; primeiroafterC2diagnosticou slug longo nafixture e3px de mínimo ancestral,ambos tratados no escopo apropriado. D14/D20 eafterfinal/E2E/auto-QA permanecem pendentes. Relatório relatorios/oficina-qa-visual.md,sem ENTREGA final ainda.
- C2 afetado final7/7, contraprova3FAIL/4PASS e restauração7/7;6 capturas after inspecionadas. C6 seletor real3/3 e6 capturas ativo/arquivado/reativado; SQL/matriz17/17 já verdes. Relatórios individuais registrados; suíte completa da branch, C3–C5 e revisões/QA oficiais seguem pendentes.
- QA próprio em API3120/Vite3121 e banco exclusivo no SQL QA3111. App principal QA não iniciado; ambientes protegidos intocados. Continuidade em `NOTAS.md` e `/home/agentes/projetos/ESTADO-goal-oficina.md`; revisão/QA oficiais pendentes.


## [2026-10-14] - Codex (Tear) - T6 parte B: L08 E2E de horas

- Branch test/e2e-l08 avançada da parte A publicada em test/e2e-l07 / b84a549. Factory própria reaproveita boards.ts, cria usuário/projeto/quadros/colunas/tarefas UUID e limpa timers/justificativas/vínculos em finally, desativando membros e arquivando projetos. Sete casos normais: start/stop, timer único ao trocar, reload/idempotência, manual/validação/segundos, diário/criação, remoção de justificativa e negação em tarefa existente sem acesso. Sem seed de negócio, sleeps, asserts opcionais, mudança de produto/spec/schema ou TimeEntry CRUD inexistente.
- Três regressões de contrato isoladas test.fail após controles/cleanup: B07 relatório retorna usuário/projeto sem acesso; manual sobreposto aceita 201 e totaliza 121 s em duas linhas em vez de recusar/61 s; diário UTC [0,60] e semana [60,0] em intervalo de 60 s cruzando meia-noite de Brasília, esperado [30,30]. Produto preservado; detalhes, passos e origem nos handlers em docs/e2e-l08-validation.md.
- Verificação final: tsc -b 69,20 s; lint 0,76 s (0 erros/12 avisos anteriores); tipos E2E 6,63 s e lint E2E 1,30 s. Repetição L06/L08 desktop/mobile x3 no mesmo banco: 145 normais/18 esperadas/0 inesperadas/0 retries, 1336,53 s, máximo 16,275 s. Na suíte completa final após SQL versionado recriado (24,14 s, 28 migrations existentes/1 projeto seed): 177 normais/12 esperadas/6 skips/2 falhas antigas mobile, 964,61 s. Os 54 resultados novos/alterados tiveram 48 normais+6 esperadas, zero inesperadas, máximo 12,640 s.
- Falhas antigas preservadas: rich-task-description.spec.ts:63 mobile reapareceu, timeout 30 s/33,096 s incluindo encerramento; ai-assistant.spec.ts:149 mobile não encontrou citação no catálogo, 24,626 s. Causa não isolada. Nenhum desses testes foi corrigido ou marcado como falha esperada. A suíte inteira não está verde.
- Ambiente: host mostrou net::ERR_NETWORK_CHANGED; testes finais em Chromium Playwright 1.62.1/Node 24.18.1 isolado com network none, transportes TCP/Unix privados ao mesmo banco dev. Para a suíte, réplica da imagem exata da API em 3115 interna compartilha loopback com provedor falso 3103; pasta de screenshots gerados montada. Drivers/env locais ignorados, sem mocks novos de API, gates, firewall/produção/QA. Tentativas de ambiente anteriores documentadas, não tratadas como aprovação.
- Recursos temporários/proxies encerrados. Dev 3100 e QA 3110 permanecem; health, SPA e login 200 em ambos, QA StartedAt inalterado e arquivos de credenciais modo 600. Relatório tem tempos individuais, dúvidas D62/arquivados/SLA e sugestões; NOTAS contém continuidade. Próximo passo: Maestro revisar/integrar os dois lotes e despachar investigação das falhas antigas/correções dos contratos de horas. Push somente nas duas branches autorizadas.


### 2026-10-20 — Oficina: full integrado ainda vermelho

Resultado real:230 aprovados,3 falhas inesperadas,40 skips,28,9min. Os três casos document-alignment escolheram fixture variável pelo fallbackINFRACOES→projects[0]; seed atualDEMO. Correção com contraprova3FAIL/2controles→5PASS em validação na branch de estabilidade, ainda não transportada. Novo focal/full obrigatório; não alegar verde. Ver relatorios/oficina-qa-regressao.md.


### 2026-10-20 — Oficina: seletor corrigido com focal integrado aprovado

Cherry8e47329 do seletor explícito; frontend178PASS46arquivos196,47s, afetados5PASS e tipos/lint/build/E2Etypes aprovados. Novo document-alignment17PASS2,1min nas duas larguras e no banco anterior. Full novo queued; preservar230/3/40 anterior e as quatro falhas esperadas de horas. PR27 rascunho; revisão e QA oficiais pendentes.


## Nova rodada local e isolamento de sincronização — 20/10/2026 18:23 UTC

qa-canonical-seed-full encerrou230PASS/3FAIL/40skips em26,6min. Os três casos de document-alignment anteriormente corrigidos passaram. As novas falhas foram board-stage-sync desktop/mobile (o primeiro quadro compartilhado já não tinha tarefa fora da conclusão) e C6mobile com quadro ainda em carregamento aos5s. Não se apresenta esta execução como verde.

A correção do teste de sincronização vem do PR30:b52addd, transportada comoe9023e7. Cria fixture exclusiva e confere categoriaDone5, stageId e completedAt pela API, além dos asserts originais no drawer/lista/reload. Contraprova própria do PR30:2FAIL/1setup, esperado Concluído/recebido A fazer; restaurado3PASS. Não houve alteração adicional de produto. Lint e tipos explícitos E2E desta branch aprovados. Novo focal/full qa-isolated-sync-* aguarda trava global. Frontend178PASS/46arquivos e backend724PASS/33skips permanecem da mesma versão de produto.

C6:diagnóstico passivo sem alterar prazo/asserção teve3PASS/22,8s. Reativação retornou estágios em125–140ms e itens em169–170ms, todos200. Isso não explica retroativamente o timeout anterior. Log/snapshot anterior e métricas numéricas posteriores preservados; nenhuma correção especulativa em cache/API.

CI39457139195 de105188b:233PASS/40skips, cinco jobs aprovados/deploy ignorado. Nova CI de e9023e7 em andamento. Quatro falhas esperadas herdadas B07/overlap continuam lacunas conhecidas, não funcionalidades corrigidas. Autorrevisão e auto-QA focais registradas; suítes finais ainda pendentes. Migration:não. REVISAO e QA oficiais:pendentes.
