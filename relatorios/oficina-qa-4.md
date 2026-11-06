# Oficina qa-4 — comportamento e acessibilidade

D01 Auth orienta solicitar convite ao administrador; D03 PublicPortal orienta conferir endereço/solicitar novo link; D05 Topbar fecha Novo item por Escape e devolve foco; D06 Projects/Teams e D09 TimePanel têm labels visíveis associados; D17 AiSettings permite Cancelar mesmo sem conexões; D19 ProjectWiki identifica o título editável; D14 ReportCharts recebe títulos de ReportsHub e passa aria-label aos SVG reais, preservando role application e navegação do Recharts3.

E2E conjunto C3/C4:25PASS/2FAIL (D13 mobile). Baseline6PASS/21FAIL. D14 exigiu distinguir carregamento de falha de acessibilidade: fixture aguarda GET200, tasks.total>0 e SVG real. Depois o baseline comprovou nomes vazios nas duas larguras; contraprova conjunta D14/D20:3FAIL/2PASS, restauração5PASS. Não foi removida a exigência do nome no SVG. Contraprova unitária Topbar/TimePanel:2FAIL, restauração2PASS; dez casos não selecionados pelo filtro, sem novo skip no código. Frontend afetado31PASS em sete arquivos.

Capturas antes/depois1440x900/390x844 em .runlogs/oficina/screenshots/D01-*,D03-*,D05-*,D06-*,D09-*,D14-*,D17-* eD19-*. Auto-QA examinou orientação, foco, labels e fechamento nas duas larguras. A imagem do gráfico pode registrar quadro intermediário da animação; nome acessível é comprovado no DOM, sem afirmar geometria final por essa foto.

Autorrevisão sem alto/médio confirmado nas alterações. Ressalvas observadas: Wiki da fixture já exibe Falha ao salvar e título longo pode cortar no celular; o alvo D19 é o nome acessível. Em relatório com sidebar expandida, a barra superior desktop ainda pode cortar controles à direita. Esses resíduos não são declarados corrigidos por C4. Não foi realizado ensaio com leitor de tela real nem validação universal de teclado.

Base a2797a7; branch fix/qa-round-1, PR27 em rascunho. Merges L06/L07/L08 preservados. Código combinado validado em ffd579c; 2eda254 adiciona somente espera pelo conteúdo do grafo antes da captura. Ambiente próprio SQL QA3111, API3120 e Vite3121; nenhuma produção.

Gates combinados: build backend aprovado; xUnit724PASS/33 skips preexistentes; Vitest173PASS/45 arquivos; tsc-b --force, tipos explícitos de E2E, lint e build frontend aprovados. Suíte E2E completa combinada ainda enfileirada; não se afirma verde integral. Não houve redução de asserção, cota, prazo ou retry. Migration: não. REVISAO e QA oficiais: pendentes.

Retificação de leitura em20/10: o log controls-after-final registra25PASS/2FAIL, ambas emD13mobile, e não27PASS como relatado inicialmente. O coordenador privado devolvia0 apesar do exit nativo1; essa propagação foi corrigida e o log real prevalece. C4 possui18 casos aprovados nesse conjunto; C3 tem6 aprovados e2 falhas. Focal D14/D20 realmente5PASS e C1 realmente5PASS, conferidos pelos logs. O popover de conteúdo cortado à esquerda está sendo corrigido, com oráculo explícito de abertura; C3 não está fechado até nova prova. Full combinado223PASS/8FAIL/40skips em23,6min, com causas em diagnóstico/correção, sem atribuir verde integral.

## Evidência semanal corrigida no ambiente combinado — 20/10/2026

A contraprova qa-weekly-counterproof teve1FAILmobile/2PASScontroles,1min, no primeiro Ajustar inacessível com centro inseguro. Restaurado o CSS safe center, qa-weekly-green teve3PASS em27,6s:setup e sete dias alcançados por cliques normais em desktop1440/mobile390, com daily-by-task e justifications200 para cada dia. Capturas foram abertas:antes a segunda/terça ficavam fora à esquerda no mobile; depois a semana começa pela segunda e o restante é alcançável horizontalmente. Desktop mantém os sete dias centralizados.

É prova do defeito específico de início semanal, com o mesmo patch transportado do PR30. Não prova eliminação de toda intermitência histórica de QA-H10, Kanban, navegação ou deadlock. A suíte combinada anterior223PASS/8FAIL/40skips permanece registrada; focal das oito falhas e full corrigido continuam na fila. Nenhum novo retry/timeout/skip ou alteração de backend nesta correção CSS. Revisão e QA oficiais pendentes.


## Controle posterior e ressalva de bootstrap

No completo local anterior à revisão estrita de sincronização, D06desktop falhou antes de main com página branca; os campos não chegaram a ser verificados nesse episódio. Controle instrumentado posterior selecionou C4inteiro27PASS, incluindo projeto/equipe nas duas larguras; quatro capturas D06abertas. A causa do bootstrap original não foi determinada. Tipos/asserções/5s mantidos. Detalhes e exclusão de telemetria sobrescrita no relatório oficina-qa-regressao.md. Não confundir controle positivo de rótulos com correção causal de bootstrap. REVISAO e QA oficiais:pendentes.
