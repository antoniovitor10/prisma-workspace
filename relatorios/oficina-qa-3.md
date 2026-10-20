# Oficina qa-3 — cortes mobile

D13: KanbanFilterBar aceita quebra de linha e mínimos zero para busca, ordenação, contagem e filtros em Kanban e Lista. D16: formulário de convite em OrganizationSettings usa coluna única no celular, tornando Perfil alcançável. D20: Requests distribui protocolo/status em duas trilhas flexíveis no celular, título ocupa linha inteira e o status longo quebra sem sair da tela.

E2E controles após correções:25PASS/2FAIL (D13 mobile) no conjunto C3/C4 (o recorte C3 contém8 casos nas duas larguras, além do setup compartilhado). Baseline conjunto6PASS/21FAIL. Para D20 foi forçado o status real Aguardando informações; contraprova conjunta D14/D20 reproduziu3FAIL/2PASS e depois5PASS. No celular a borda direita do status anterior chegava a423px numa tela390px. Não foi ampliada tolerância geométrica.

Capturas antes/depois1440x900 e390x844 preservadas em .runlogs/oficina/screenshots/D13-*,D16-* eD20-*. Foram abertas e inspecionadas imagens finais desktop/mobile e os cortes anteriores mobile. Auto-QA verificou controles de ordenação e filtros, abriu Conteúdo do cartão antes de avaliar seus campos e conferiu status longo. Fluxo pode rolar verticalmente; não se exige caber inteiro numa dobra.

Autorrevisão: nenhum alto/médio confirmado no recorte. A mudança é de composição, sem alteração de ordenação, permissão ou contrato da fila. As imagens de filtros usam quadro vazio; não comprovam drag-and-drop com alto volume. Não se afirma ausência universal de overflow em outras áreas.

Base a2797a7; branch fix/qa-round-1, PR27 em rascunho. Merges L06/L07/L08 preservados. Código combinado validado em ffd579c; 2eda254 adiciona somente espera pelo conteúdo do grafo antes da captura. Ambiente próprio SQL QA3111, API3120 e Vite3121; nenhuma produção.

Gates combinados: build backend aprovado; xUnit724PASS/33 skips preexistentes; Vitest173PASS/45 arquivos; tsc-b --force, tipos explícitos de E2E, lint e build frontend aprovados. Suíte E2E completa combinada ainda enfileirada; não se afirma verde integral. Não houve redução de asserção, cota, prazo ou retry. Migration: não. REVISAO e QA oficiais: pendentes.

Retificação de leitura em20/10: o log controls-after-final registra25PASS/2FAIL, ambas emD13mobile, e não27PASS como relatado inicialmente. O coordenador privado devolvia0 apesar do exit nativo1; essa propagação foi corrigida e o log real prevalece. C4 possui18 casos aprovados nesse conjunto; C3 tem6 aprovados e2 falhas. Focal D14/D20 realmente5PASS e C1 realmente5PASS, conferidos pelos logs. O popover de conteúdo cortado à esquerda está sendo corrigido, com oráculo explícito de abertura; C3 não está fechado até nova prova. Full combinado223PASS/8FAIL/40skips em23,6min, com causas em diagnóstico/correção, sem atribuir verde integral.
