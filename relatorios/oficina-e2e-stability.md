# Diagnóstico de estabilidade E2E — Oficina

Baseline de leitura: `origin/main@a2797a7`. Branch `test/e2e-stability-20261020`. Este checkpoint prepara observabilidade; não declara corrigidas falhas sem causa comprovada.

## Evidências preservadas

- QA horas-5, runtime `64d3e32`: suíte afetada 43 aprovados/7 inesperados/1 skip; repetição 19 aprovados/5 inesperados/1 skip. As três falhas de Kanban responsivo repetiram: botão de mover coluna desktop/mobile e Nova Coluna mobile. No JSON nativo externo, falhas de visibilidade levam aproximadamente 6,5s, não o timeout global de30s.
- Os dois timeouts restantes de horas na repetição duram30159/30137ms; um aponta para fixtures/functional.ts:28 no GET de itens do quadro, outro não localiza a etapa. A forma resumida dos artefatos históricos não preserva resposta, etapa completa ou estado da página. Não é possível deduzir a causa desses dados.
- QA-H10 em horas-7 `8588a8b`:46PASS/1timeout mobile na primeira suíte, três repetições focais e47/47 posteriores. Ausência de nova falha não prova correção. Asserção, prazo30s e retries originais permanecem.
- Deadlock BoardStructureSql: uma execução390/391 falhou; foco1/1 e confirmação391/391 passaram, sem grafo de deadlock. O repositório usa Serializable e o fixture escreve no mesmo banco compartilhado. Contenção de ranges/concorrência entre classes é hipótese, não causa comprovada. Não reduzir isolamento, serializar testes indiscriminadamente ou adicionar retry para esconder a falha.
- rich-task-description mobile e ai-assistant:149/:221/:340 possuem registros anteriores de falha; board-stage-sync:12 também está no recorte do goal. Até nova reprodução, continuam pendências de diagnóstico.

## Leitura do código

- `e2e/fixtures/test.ts:49`: resolve o projeto do seed e `project.boards[0]`; o teste não garante previamente que esse quadro possui colunas/cartões elegíveis.
- `src/pages/Kanban.tsx:1190`: Nova Coluna depende de boardDataStatus ready. Logo, a ausência desse botão pode decorrer de falha/carregamento do quadro antes de qualquer problema de click. `:1455` e`:1465` já têm aria-label das setas; não remover as asserções de acessibilidade.
- `e2e/board-stage-sync.spec.ts`: altera tarefa do seed e não restaura a etapa anterior. É um risco de acoplamento entre testes, não prova de que causou a falha antiga.
- `e2e/rich-task-description.spec.ts`: edição/colagem/reabertura e restauração dividem o prazo30s; etapa lenta deve ser localizada antes de mudar qualquer sincronização.
- `src/Prisma.Workspace.Infrastructure/Repositories/BoardStructureRepository.cs:22`: transação Serializable. `tests/Prisma.Workspace.Tests/BoardStructureSqlTests.cs:300`: fixture SQL compartilhada, sem captura do grafo. Nenhuma alteração no SQL de produto foi feita nesta branch.

## Instrumentação opt-in

`playwright.diagnostics.config.ts` conserva a configuração nativa e troca apenas os reporters. `e2e/reporters/diagnostic.ts` grava JSONL com origem relativa, categoria de etapa, duração, status e flags de falha. Início/fim de etapa usam número sequencial, permitindo localizar a operação lenta no arquivo do spec sem copiar seu título.

Não registra títulos de testes/etapas, argumentos, mensagens/stacks, cabeçalhos, corpos, stdout/stderr, anexos, IDs internos de testes nem caminhos externos. O output padrão fica em `.runlogs/oficina/e2e-diagnostic-steps.jsonl`, ignorado pelo Git.

Exemplo, sempre dentro do mutex compartilhado e após conferir carga/reserva:

```bash
flock /home/agentes/projetos/.trava-navegador npm run e2e -- --config playwright.diagnostics.config.ts e2e/kanban-responsive.spec.ts --workers=1 --retries=0
```

O comando é roteiro, ainda não evidência de execução deste checkpoint. Em CaaS/Contabo, usar somente runtime/banco próprios autorizados.

## Verificação real até aqui

- Teste Node de privacidade:1/1PASS. Contraprova adicionando título ao JSON:1FAIL esperada; fonte restaurada,1/1PASS novamente.
- Tipos explícitos do reporter/config/teste passaram com TypeScript6 e `--types node`; primeiro comando sem tipos de Node falhou por configuração de invocação, não por resultado ignorado. Não confundir esse comando com o gate de app `tsc -b`.
- Lint sem erros, avisos anteriores preservados. Nenhuma asserção, prazo, retry, cota, spec funcional, schema ou migration mudou; nenhum novo skip/test.fail.
- Execução nativa concluída sob mutex global/CPU próprio,1worker/retries0:25PASS/3FAIL/1skip preexistente,5,1min. Quatro specs:kanban-responsive,board-stage-sync,rich-task-description eai-assistant. Runtime próprio organizações `f51aaee` em3122/3124, banco próprioSQLQA3111; specs/Kanban idênticos à base, mas não é binário exato da main. Nenhum dos cenários Kanban/editor/sync falhou nessa execução; isso não resolve a intermitência histórica.
- Falhas:ai-assistant:252desktop e340desktop/mobile. Captura252mostra documento vazio; JSONLlocaliza navegação em:258:14 com33.020ms, seguida de clique em:258:102 com9.353ms e falha. Total42.773ms inclui encerramento. Mobile252passou em7.809ms. Não há evidência suficiente para atribuir a rede, renderização ou produto; não foi alterado o teste.
- Capturas340mostram “A conexão CLI não está instalada”. CódigoAiConnectionOnboarding lêAi:BridgeUrl e, ausente, tenta hostname de container. A API própria estava sem URL do fake nativo18747, embora o E2E o inicie. Configuração privada corrigida para127.0.0.1:18747/v1; API própria reiniciada saudável. Repetição nativa pendente, portanto ainda não se declara correção comprovada das falhas340.
- Evidências preservadas emoficina-qa/.runlogs/oficina/stability-evidence:JSONL original,3capturas,vídeos/contextos. Nenhum corpo/segredo foi copiado para este relatório. A suíte completa própria de organizações está enfileirada com a configuração correta; não misturar esse resultado futuro com o baseline inicial.

## Hipóteses ordenadas e próximo ensaio

1. Kanban: conferir board resolvido, quantidade de colunas, estado ready/erro e HTTP de stages/items antes de atribuir o defeito ao seletor ou à permissão. Capturar screenshot e origem da asserção.
2. Timeout mobile: localizar a etapa de maior duração no JSONL, comparar com requests iniciados/concluídos da API própria e carga do host. Sem correlação, não atribuir ao banco/rede.
3. Seed compartilhado: demonstrar diferença ao repetir o mesmo cenário com fixture própria, mantendo todos os asserts; somente então corrigir o harness.
4. Deadlock: na próxima ocorrência do SQL QA próprio, capturar grafo dessa instância e confirmar recursos/sessões envolvidos antes de mudar transação ou fixture. Não consultar produção.
5. AI/editor: usar origens e tempos para separar estado de catálogo, fake provider, salvamento e interação mobile. Não aumentar cotas nem prazo para passar.

Autorrevisão parcial: privacidade coberta por sentinela e contraprova; reporter é opt-in e não altera o verdict. Auto-QA visual e novas reproduções nativas pendentes. REVISAO e QA oficiais: pendentes.

## Autorrevisão e auto-QA parcial

Releitura de reporter/config/teste:nenhum achado alto/médio; metadados são mínimos e o custo de escrita síncrona só existe no modo opt-in. Capturas de falha nas duas larguras examinadas; não houve mudança visual nesta branch. A ausência de falha do Kanban nesta rodada não é prova de estabilidade permanente. QA-H10/deadlock não reproduzidos com grafo SQL; hipóteses históricas continuam abertas. Sem correção especulativa de transação,timeout,retry,cota ou assert. Migration:não. REVISAO e QA oficiais:pendentes.

## Fechamento do diagnóstico e correção comprovada — 20/10/2026

A explicação inicial de CLI por URL ausente era incompleta. O setup privado também não configurava Ai:BridgeToken; BridgeAsync retorna a mensagem de CLI indisponível antes de chamar o provedor quando esse token está vazio. Além disso, E2E_AI_PROVIDER_PORT3123 colidia com a API da própria branch de chave de projeto, provocando EADDRINUSE no beforeAll e impedindo14 casos. O harness privado passou a usar18747 e token aleatório em arquivo ignorado, conforme o contrato do CI existente. Nenhuma cota, retry, prazo ou regra de produto mudou. Depois do ajuste, o arquivo inteiro ai-assistant passou17/17 em organizações e17/17 na branch de chave. As falhas e execuções anteriores permanecem nos relatórios dessas branches; não equivalem a uma única suíte originalmente verde.

Uma corrida diferente foi comprovada no teste unitário RichTaskDescriptionEditor.test.tsx: a imagem já tinha sido inserida no DOM pelo editor enquanto a mensagem React ainda era Loading. A implementação chama a inserção antes de setStatus/setFeedback, tornando inválida a suposição de simultaneidade do teste. Corrigido somente o teste para aguardar a mesma mensagem terminal, com waitFor já existente, sem prazo extra ou relaxamento da asserção. Contraprova retirou temporariamente o feedback terminal:1FAIL/6PASS; finally restaurou a implementação e o arquivo voltou a7PASS. Commit f563b5e. A correção também foi usada nos PRs34/36/27; o componente de produto não foi alterado por esta branch. No PR34 o fechamento completo foi162PASS/41 arquivos.

A navegação vazia252desktop não foi novamente reproduzida no arquivo17/17; sua causa continua sem comprovação. Kanban, editor e sync passaram na amostra inicial, mas a intermitência histórica, QA-H10 e deadlock continuam sem causa demonstrada. Não foi feita correção especulativa nem coletado dado de produção. Próximo ensaio de deadlock depende de ocorrência na instância QA própria e captura de grafo SQL; próxima ocorrência de navegação deve correlacionar requests/ready/carga antes de mudar código.

Autorrevisão final do recorte: nenhum alto/médio confirmado; reporter preserva privacidade, e a espera unitária exige o resultado terminal original. Auto-QA por inspeção das falhas desktop/mobile e execuções reais acima; não há mudança visual de produto. Entrega de diagnóstico com limitações explícitas, sem promessa de eliminar toda intermitência. Migration: não. REVISAO e QA oficiais: pendentes. PR30 continua em rascunho.

## Continuação — causa semanal comprovada e gates reais (20/10, 13:10 UTC)

A suíte combinada do PR27 revelou botões Ajustar do início da semana fora da área alcançável no mobile: grid com nowrap, overflow-x:auto e justify-content:center distribuía excesso para coordenadas negativas. CSS passa a safe center, preservando centralização quando cabe e tornando o início acessível quando não cabe. Um novo E2E percorre os sete dias por cliques normais, confirma as duas consultas diárias200 e fecha o diálogo nas larguras1440/390. Não aumenta prazo/retry nem força clique. Commit71f8232; integrado também no PR27. Contraprova visual e execução nativa própria aguardam trava compartilhada.

Build backend e xUnit próprio:690PASS/33skips existentes. Tipos forçados, lint e build frontend passaram. A primeira rodada completa Vitest falhou na coleta de scripts/tests/diagnostic.test.mts: o runner coleta .test.mts, mas esse arquivo usava node:test sob jsdom. Corrigido para test do Vitest com anotação explícita de ambiente node; as mesmas asserções de privacidade foram preservadas, sem excluir arquivo ou caso. Tipos explícitos do reporter/teste passaram e o teste de privacidade passou na segunda rodada.

Resultado real da segunda rodada completa:162PASS/1FAIL,42arquivos,234,76s. A falha foi o primeiro caso de MethodSettingsPanel.test.tsx: mocks resolvidos, mas a interface permaneceu Carregando até o findByRole falhar; os outros nove casos passaram. Não alterar componente de produto sem causa. Há duas fixtures que restauram notifyManager.setScheduler com callback setTimeout definido em jsdom. Um probe separado demonstrou que scheduler de uma janela fechada perde o callback, enquanto defaultScheduler o entrega, mas isso não comprova que aquela execução usou a mesma condição. Portanto essa hipótese permanece não confirmada, sem patch especulativo, novo timeout ou repetição cega para obter verde. Logs completos preservados em .runlogs/oficina/full-vitest-node-test-collection.log e full-vitest.log.

E2E semanal e completo da branch própria foram enfileirados para diagnóstico e prova real; ainda não executados neste checkpoint. A antiga entrega de diagnóstico não implica que estes novos gates passaram. Auto-QA das novas capturas permanece pendente. Migration:não. REVISAO e QA oficiais:pendentes.

## Evidência semanal corrigida no ambiente combinado — 20/10/2026

A contraprova qa-weekly-counterproof teve1FAILmobile/2PASScontroles,1min, no primeiro Ajustar inacessível com centro inseguro. Restaurado o CSS safe center, qa-weekly-green teve3PASS em27,6s:setup e sete dias alcançados por cliques normais em desktop1440/mobile390, com daily-by-task e justifications200 para cada dia. Capturas foram abertas:antes a segunda/terça ficavam fora à esquerda no mobile; depois a semana começa pela segunda e o restante é alcançável horizontalmente. Desktop mantém os sete dias centralizados.

É prova do defeito específico de início semanal, com o mesmo patch transportado do PR30. Não prova eliminação de toda intermitência histórica de QA-H10, Kanban, navegação ou deadlock. A suíte combinada anterior223PASS/8FAIL/40skips permanece registrada; focal das oito falhas e full corrigido continuam na fila. Nenhum novo retry/timeout/skip ou alteração de backend nesta correção CSS. Revisão e QA oficiais pendentes.

O ensaio acima ocorreu no QA combinado do PR27; o focal e o completo próprios desta branch seguem enfileirados. Localfront162PASS/1FAIL e hipóteses não comprovadas continuam válidos como limites, não foram substituídos pelo resultado CSS.
