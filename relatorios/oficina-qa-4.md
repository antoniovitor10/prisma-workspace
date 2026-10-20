# Oficina qa-4 — comportamento e acessibilidade

D01 Auth orienta solicitar convite ao administrador; D03 PublicPortal orienta conferir endereço/solicitar novo link; D05 Topbar fecha Novo item por Escape e devolve foco; D06 Projects/Teams e D09 TimePanel têm labels visíveis associados; D17 AiSettings permite Cancelar mesmo sem conexões; D19 ProjectWiki identifica o título editável; D14 ReportCharts recebe títulos de ReportsHub e passa aria-label aos SVG reais, preservando role application e navegação do Recharts3.

E2E conjunto C3/C4:27PASS. Baseline6PASS/21FAIL. D14 exigiu distinguir carregamento de falha de acessibilidade: fixture aguarda GET200, tasks.total>0 e SVG real. Depois o baseline comprovou nomes vazios nas duas larguras; contraprova conjunta D14/D20:3FAIL/2PASS, restauração5PASS. Não foi removida a exigência do nome no SVG. Contraprova unitária Topbar/TimePanel:2FAIL, restauração2PASS; dez casos não selecionados pelo filtro, sem novo skip no código. Frontend afetado31PASS em sete arquivos.

Capturas antes/depois1440x900/390x844 em .runlogs/oficina/screenshots/D01-*,D03-*,D05-*,D06-*,D09-*,D14-*,D17-* eD19-*. Auto-QA examinou orientação, foco, labels e fechamento nas duas larguras. A imagem do gráfico pode registrar quadro intermediário da animação; nome acessível é comprovado no DOM, sem afirmar geometria final por essa foto.

Autorrevisão sem alto/médio confirmado nas alterações. Ressalvas observadas: Wiki da fixture já exibe Falha ao salvar e título longo pode cortar no celular; o alvo D19 é o nome acessível. Em relatório com sidebar expandida, a barra superior desktop ainda pode cortar controles à direita. Esses resíduos não são declarados corrigidos por C4. Não foi realizado ensaio com leitor de tela real nem validação universal de teclado.

Base a2797a7; branch fix/qa-round-1, PR27 em rascunho. Merges L06/L07/L08 preservados. Código combinado validado em ffd579c; 2eda254 adiciona somente espera pelo conteúdo do grafo antes da captura. Ambiente próprio SQL QA3111, API3120 e Vite3121; nenhuma produção.

Gates combinados: build backend aprovado; xUnit724PASS/33 skips preexistentes; Vitest173PASS/45 arquivos; tsc-b --force, tipos explícitos de E2E, lint e build frontend aprovados. Suíte E2E completa combinada ainda enfileirada; não se afirma verde integral. Não houve redução de asserção, cota, prazo ou retry. Migration: não. REVISAO e QA oficiais: pendentes.
