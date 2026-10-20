# Oficina qa-1 — tarefas

Subtarefa exige escolha explícita de responsável elegível, limpa a escolha ao trocar de tarefa e envia projectId/responsibleId. ArgumentException retorna400; erros internos permanecem500 genéricos. Dependência usa autocomplete e comprova vínculo persistido. Agrupamento diário/semanal considera Brasília e recorta intervalos sobrepostos sem modificar os apontamentos; foi incorporado somente esse recorte da correção de horas, sem CRUD, B07 ou schema.

Provas: middleware3PASS, contraprova2FAIL/1controle, restauração3PASS. Drawer9PASS; componente anterior falha na exigência de seleção; restaurado9PASS. Probe HTTP5PASS400 sem efeitos colaterais. Agregação Brasília5PASS, código anterior5FAIL, restaurado5PASS; E2E Brasília5PASS. E2E tarefas geral9PASS; focal C1 cinco casos aprovados, inclusive nas larguras1440x900/390x844. Última captura aguarda opção real visível e clicável por trial antes de fotografar; as quatro imagens finais de responsável/autocomplete foram abertas e inspecionadas.

Uma tentativa de reenquadramento falhou por Vite próprio encerrado; preservada como c1-captures-connection-refused, seguida de reinício do serviço próprio e focal5PASS. Primeira foto do popup mostrava carregamento; o oráculo da captura foi fortalecido, sem sleep ou prazo extra.

Autorrevisão: nenhum alto/médio confirmado no recorte; autorização das pessoas continua no servidor. Auto-QA concluído para os controles citados, com opção real e escolha explícita nas duas larguras. Não corrige os demais problemas da branch de horas nem elimina skips herdados. Evidências ignoradas locais: .runlogs/oficina, incluindo screenshots/C1-*.

Base a2797a7; branch fix/qa-round-1, PR27 em rascunho. Merges L06/L07/L08 preservados. Código combinado validado em ffd579c; 2eda254 adiciona somente espera pelo conteúdo do grafo antes da captura. Ambiente próprio SQL QA3111, API3120 e Vite3121; nenhuma produção.

Gates combinados: build backend aprovado; xUnit724PASS/33 skips preexistentes; Vitest173PASS/45 arquivos; tsc-b --force, tipos explícitos de E2E, lint e build frontend aprovados. Suíte E2E completa combinada ainda enfileirada; não se afirma verde integral. Não houve redução de asserção, cota, prazo ou retry. Migration: não. REVISAO e QA oficiais: pendentes.

Retificação de leitura em20/10: o log controls-after-final registra25PASS/2FAIL, ambas emD13mobile, e não27PASS como relatado inicialmente. O coordenador privado devolvia0 apesar do exit nativo1; essa propagação foi corrigida e o log real prevalece. C4 possui18 casos aprovados nesse conjunto; C3 tem6 aprovados e2 falhas. Focal D14/D20 realmente5PASS e C1 realmente5PASS, conferidos pelos logs. O popover de conteúdo cortado à esquerda está sendo corrigido, com oráculo explícito de abertura; C3 não está fechado até nova prova. Full combinado223PASS/8FAIL/40skips em23,6min, com causas em diagnóstico/correção, sem atribuir verde integral.
