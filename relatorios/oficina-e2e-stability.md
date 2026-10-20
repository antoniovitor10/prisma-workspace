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
- Navegador nativo aguarda a trava compartilhada. O lote está preparado para executar os quatro specs no runtime próprio de organizações `f51aaee`; os arquivos nativos e de Kanban coincidem com a main, mas esse runtime tem as quatro correções de organizações. Não tratá-lo como binário exato da main. A configuração e o SHA do runtime devem acompanhar qualquer conclusão posterior.

## Hipóteses ordenadas e próximo ensaio

1. Kanban: conferir board resolvido, quantidade de colunas, estado ready/erro e HTTP de stages/items antes de atribuir o defeito ao seletor ou à permissão. Capturar screenshot e origem da asserção.
2. Timeout mobile: localizar a etapa de maior duração no JSONL, comparar com requests iniciados/concluídos da API própria e carga do host. Sem correlação, não atribuir ao banco/rede.
3. Seed compartilhado: demonstrar diferença ao repetir o mesmo cenário com fixture própria, mantendo todos os asserts; somente então corrigir o harness.
4. Deadlock: na próxima ocorrência do SQL QA próprio, capturar grafo dessa instância e confirmar recursos/sessões envolvidos antes de mudar transação ou fixture. Não consultar produção.
5. AI/editor: usar origens e tempos para separar estado de catálogo, fake provider, salvamento e interação mobile. Não aumentar cotas nem prazo para passar.

Autorrevisão parcial: privacidade coberta por sentinela e contraprova; reporter é opt-in e não altera o verdict. Auto-QA visual e novas reproduções nativas pendentes. REVISAO e QA oficiais: pendentes.
