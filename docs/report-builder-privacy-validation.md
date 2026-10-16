# Horas-3: privacidade no construtor de relatórios

Correção do ALTO recebido após a [entrega horas-2](time-tracking-followup-validation.md): o relatório de horas estava restrito, mas o construtor usava apenas a permissão de abrir relatórios. Um leitor podia consultar apontamentos e totais de outras pessoas pela prévia, por uma definição compartilhada ou pela exportação.

## Correção

O `ReportExecutor` recebe obrigatoriamente o ator da execução. Confere membro ativo da organização corrente e permissão de relatório; aplica papel, negações de equipe e projetos acessíveis antes de montar linhas e métricas. Ausência de filtro de projeto não amplia o acesso. O papel do criador de um relatório compartilhado não é usado para autorizar seus leitores.

Usuários comuns recebem só suas linhas de apontamento/usuário e suas horas nas tarefas e solicitações. Gestores recebem as equipes que lideram, usando a equipe do item ou a do quadro; administradores recebem o escopo autorizado da organização corrente. A fonte Usuários preserva pessoas autorizadas sem apontamentos, com zero. A contagem de projetos da fonte Equipes usa somente projetos acessíveis.

Não há alteração de endpoint, DTO público, migration, ModelSnapshot ou estrutura da auditoria. A interface do construtor não foi modificada.

## Cobertura

- `ReportBuilderVisibilityTests`: 26 casos cobrindo Viewer, Client, ProductOwner, ScrumMaster, ProjectManager, Manager e Administrator; outro tenant; projeto inacessível; negações explícitas; métricas e horas de colegas na mesma tarefa; relatório salvo compartilhado; fontes Projeto/Equipe/Sprint; usuário autorizado com zero horas.
- `OrganizationHoursVisibilityTests`: o caso de rateio do construtor passa a fornecer ator e permissão explicitamente, sem executar o serviço sem contexto de segurança.
- `e2e/report-builder-privacy.spec.ts`: duas jornadas UI com leitor nas fontes Apontamentos/Usuários, sem filtro de projeto; execução e CSV de definição compartilhada usam o leitor e o administrador com recortes diferentes. Fixtures UUID próprias e limpeza no teardown. Browser ainda pendente no checkpoint inicial.
- As duas ressalvas documentais da revisão foram registradas na spec de horas: equipe do item/quadro como recorte e início de timer ainda fora da validação de criação manual/edição.

## Verificações

| Comando | Resultado | Tempo |
| --- | --- | --- |
| `dotnet build Prisma.Workspace.sln --no-restore` | Passou; 0 erros; 3 avisos anteriores; inclui a reordenação do pré-filtro | 82,96 s |
| `dotnet test tests/Prisma.Workspace.Tests --no-restore --filter 'FullyQualifiedName~ReportBuilderVisibilityTests\|FullyQualifiedName~OrganizationHoursVisibilityTests'` | 35/35; inclui compilação dos novos testes | 61,60 s |
| `bash .runlogs/horas-3/run-xunit-qa.sh` | 366/366, sem skips; banco SQL próprio | 72,21 s testes; 96,14 s runner |
| `npm run lint` | Passou; 12 avisos anteriores | 3,15 s |
| TypeScript explícito do novo E2E, com emissão e `--ignoreConfig` | Passou | 14,35 s |
| Baseline focado, fonte Usuários/desktop, runtime anterior `d67326b` | Setup passou; caso falhou exatamente no oráculo: resposta retornou 11 usuários em vez de um; limpeza sem falhas | 17,16 s caso; 38,04 s global |
| Novo E2E desktop/mobile, três repetições | Pendente na imagem candidata | Pendente |
| E2E completo após recriar somente banco QA | Pendente na imagem candidata | Pendente |

O build inicial apontou `Guid?` no filtro de sprint sem equipe; corrigido antes dos resultados aprovados. Uma chamada direta de TypeScript também exigiu `--ignoreConfig`, opção informada pelo compilador instalado; não foi usado `--noEmit`. Vitest 155/155, `tsc -b` e build frontend da entrega anterior se referem às mesmas fontes de UI desta correção; não foram repetidos no checkpoint inicial.

Execuções exclusivamente no prisma-qa, app 3110/SQL 3111. xUnit usa `Prisma_BoardColumns_Horas2`. O app QA permanece no SHA anterior até a reconstrução com reserva própria; o Maestro cancelou a janela de silêncio no GOALS; os testes de browser deste checkpoint não podem ser considerados executados na nova correção. Prisma-dev e produção preservados.

## Achado relacionado e limites

Uma prova HTTP focada confirmou que `GetPreparedReportsQueryHandler` também retorna horas de colegas: após registrar 1h própria e 2h de colega em dados exclusivos, um Viewer recebeu 3h em `/api/reports/prepared`, com ou sem filtro do projeto próprio; `/api/reports/hours` retornou corretamente 1h. O relatório de tempo por projeto também retornou 3h e duas pessoas ao mesmo perfil, em nova fixture própria. As fixtures foram limpas. Os handlers preparado e de tempo por projeto não foram alterados neste checkpoint porque não constava nominalmente do ALTO recebido; o achado foi devolvido ao Maestro/Prumo com evidência no handoff. Não declarar o B07 globalmente fechado nem a branch pronta para produção enquanto esse caminho não estiver esclarecido.

A população de itens/apontamentos é restringida pelo papel antes de consultar o acesso a seus projetos, evitando consultar os projetos das linhas já proibidas. Essa reordenação foi recompilada e passou novamente na suíte xUnit completa de 366 casos; os tempos da nova rodada estão nos artefatos privados. A verificação de acesso aos projetos reutiliza o serviço existente, cujo N+1 está reservado ao C6. Não foi antecipada essa correção. Firefox/WebKit, provedor real de IA, produção e implantação não testados. Revisão e QA independente do SHA final continuam necessários.
