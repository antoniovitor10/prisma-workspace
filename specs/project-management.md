# SPEC-PROJECT-MANAGEMENT: Gestão e Arquivamento de Projetos

**Status:** approved

**Revisão funcional:** aprovada explicitamente por PO em 2026-08-24

**Homologação manual:** pendente

**Natureza:** contrato funcional desejado comparado ao estado atual do produto

## Propósito

Definir o ciclo funcional de criação, edição, arquivamento e restauração de projetos, incluindo seus efeitos
sobre tarefas, sua relação não proprietária com quadros transversais e sua autorização conforme os cinco
perfis-base da D55.

Esta spec permanece como contrato funcional de projetos. A proposta estética
`SPEC-PROJECTS-VISUAL-REFRESH` foi cancelada pela D66, e o visual atual da listagem deve ser preservado. Esta
spec também não reabre a spec superseded de metodologia/estrutura de trabalho.

## Escopo

- Criar e editar projetos.
- Vincular várias equipes e vários membros individuais ao mesmo projeto.
- Arquivar e restaurar projetos.
- Ocultar, sem apagar, o projeto arquivado e suas tarefas.
- Autorizar essas operações conforme D55.
- Preservar dados, relações e histórico.
- Registrar o estado atual e os gaps de implementação.

## Fora do escopo

- Redesenho visual da listagem de projetos.
- Exposição da chave técnica do projeto.
- Metodologia ou estrutura de trabalho visível.
- Exclusão física de projeto, quadro, tarefa, hora, anexo ou histórico.
- Lixeira de projeto; projeto possui somente arquivamento e restauração.
- Implementação, migration ou backfill nesta revisão documental.

## Atores e permissões

### Perfis-base

- **Administrador:** pode criar, editar, arquivar e restaurar projetos da organização ativa.
- **Gestor:** pode criar, editar, arquivar e restaurar projetos dentro do seu escopo de gestão.
- **Membro:** não cria, edita, arquiva ou restaura projetos por padrão.
- **Visualizador:** não cria, edita, arquiva ou restaura projetos.
- **Externo:** não acessa a gestão interna de projetos.

Administrador e Gestor possuem a capacidade de gestão de projetos em seus perfis-base. Conforme
`SPEC-USER-ACCESS-PERMISSIONS`, perfis personalizados e overrides explícitos podem restringir essa capacidade;
qualquer ampliação futura para um Membro exige uma concessão explícita dentro do catálogo e escopo autorizados.
Negações explícitas prevalecem e a API é a autoridade final.

## Regras de criação e edição

1. Somente Administrador ou Gestor autorizado pode criar um projeto por seu perfil-base.
2. A criação exige somente `nome` e `descrição` como campos funcionais; nenhum outro dado deve bloquear o cadastro.
3. A chave técnica é gerada pelo backend e não aparece no formulário, conforme D56.
4. Equipes e membros podem ser vinculados depois da criação sem transformar esses vínculos em campos obrigatórios.
5. O projeto nasce no estado `Ativo`, pode nascer sem quadro e não provoca criação automática de quadro.
6. O projeto possui somente os estados funcionais `Ativo` e `Arquivado`.
7. Quadro e coluna são escolhidos na tarefa, posteriormente, conforme D52; não existe `BoardId` exclusivo ou
   quadro padrão pertencente ao projeto.
8. Somente Administrador ou Gestor autorizado pode editar nome, descrição, responsável, datas, estado e demais
   configurações funcionais do projeto por seu perfil-base.
9. O ator, a organização e todos os vínculos informados devem pertencer ao tenant ativo.
10. A criação é atômica: falha de validação ou autorização não pode deixar projeto, vínculo, evento ou configuração
   parcialmente persistidos.
11. Toda criação e edição administrativa relevante gera histórico com ator, data e diferenças aplicáveis.
12. A chave técnica segue D56 e `SPEC-PROJECT-KEY-AUTO-GENERATION`: é gerada pelo backend e permanece oculta na
   experiência funcional.
13. A metodologia permanece oculta conforme D48 e `SPEC-PROJECT-METHODOLOGY-HIDDEN`.

## Equipes e membros do projeto

1. Um projeto pode possuir várias equipes vinculadas simultaneamente.
2. Um projeto pode possuir vários membros individuais vinculados simultaneamente.
3. Vínculos de equipe e vínculos individuais podem coexistir no mesmo projeto.
4. Administrador e Gestor autorizados podem adicionar ou remover equipes e membros do projeto.
5. O mesmo usuário ou equipe não pode possuir vínculo duplicado com o mesmo projeto.
6. Remover um vínculo não apaga a pessoa, a equipe, tarefas, horas ou histórico e não remove outra concessão de
   acesso independente.
7. O acesso efetivo de cada vínculo é resolvido por D55; o vínculo não recria os papéis fixos de `ProjectRole`.
8. Equipe, membro e projeto devem pertencer à mesma organização ativa.
9. Vincular uma equipe ao projeto concede automaticamente perfil-base **Membro** nesse projeto a todos os
   integrantes atuais da equipe.
10. Quem entrar posteriormente em uma equipe já vinculada herda o mesmo acesso de Membro ao projeto.
11. Ao sair da equipe, a pessoa perde o acesso herdado por esse vínculo, salvo quando ainda possuir vínculo
    individual com o projeto ou acesso herdado por outra equipe vinculada.
12. A resolução de acesso considera todas as origens válidas sem duplicar associação ou perfil efetivo; remover
    uma origem não revoga as demais.

## Arquivamento do conjunto

1. Arquivar um projeto é uma operação lógica e recuperável; não executa exclusão física.
2. Projeto não possui ação `Excluir`, endpoint de exclusão permanente, lixeira nem retenção temporária para purge.
3. O projeto arquivado deixa de aparecer nas telas normais, buscas, seletores e ações de criação de trabalho.
4. Todas as tarefas do projeto, incluindo tarefas pai e subtarefas, deixam de aparecer nas telas normais.
5. Quadros são independentes e não são arquivados como efeito colateral do projeto.
6. Horas, anexos, comentários, vínculos, sprints, eventos e histórico permanecem preservados.
7. Arquivamento deve registrar ator, data, projeto e quantidade de tarefas ocultadas, preservando as referências
   históricas aos quadros e colunas em que estavam.
8. O conjunto arquivado pode ser consultado por meio de filtro explícito por usuários autorizados.
9. Nenhuma tarefa arquivada pelo projeto deve continuar editável, movimentável, atribuível ou receber novas horas
   enquanto o projeto permanecer arquivado.
10. A operação deve ser transacional: projeto e tarefas ficam ocultos juntos ou nenhuma alteração é
   confirmada.
11. O diálogo de arquivamento informa que projeto e tarefas ficarão ocultos, que os quadros independentes serão
    preservados e que nenhum dado será apagado.

## Compatibilidade com quadros transversais da D52

No modelo desejado da D52, um quadro pode reunir tarefas de vários projetos, não é filho funcional de um projeto
e nunca é criado automaticamente por ele. Portanto:

- arquivar um projeto sempre oculta as tarefas pertencentes a ele em todos os quadros;
- nenhum quadro é arquivado, restaurado ou tem seu estado alterado como efeito colateral do projeto;
- o quadro permanece visível aos usuários autorizados, possivelmente vazio, sem mostrar as tarefas do projeto
  arquivado;
- não existe entidade, tabela ou comando de vínculo manual Projeto↔Quadro;
- criar ou mover uma tarefa do Projeto A para o Quadro X exige acesso efetivo ao projeto e ao quadro antes da
  operação;
- enquanto existir ao menos uma tarefa do projeto no quadro, o projeto aparece como opção nos filtros do quadro;
- quando a última tarefa do projeto sair do quadro, a opção derivada desaparece do filtro, sem comando de
  desvinculação;
- referências históricas entre projeto, quadro, coluna e tarefa permanecem preservadas;
- o vínculo histórico da tarefa com quadro/coluna não transforma o quadro em propriedade do projeto.

Não existe conceito funcional de “quadro do projeto” neste contrato. A interface pode listar quadros nos quais há
tarefas do projeto, mas essa é uma relação derivada das tarefas, não propriedade, exclusividade ou quadro padrão.

## Restauração

1. Administrador ou Gestor autorizado pode restaurar o projeto e o conjunto arquivado por essa operação.
2. Restaurar torna novamente visíveis o projeto e as tarefas arquivadas junto com ele.
3. Tarefas que já estavam arquivadas antes do arquivamento do projeto não podem ser reativadas
   indevidamente.
4. Quadros transversais preservados voltam a exibir as tarefas restauradas do projeto nas respectivas colunas.
5. A restauração é transacional e gera histórico com ator, data e recursos restaurados.
6. Conflitos de tenant, vínculo, estado ou concorrência impedem a operação completa.

## Invariantes

- Um projeto pertence a exatamente uma organização.
- A criação funcional exige apenas nome e descrição; chave, metodologia, equipe, membro e demais configurações
  não são campos obrigatórios do formulário inicial.
- Projeto possui somente os estados Ativo e Arquivado.
- Projeto pode existir sem quadro; não cria nem possui quadro automático, exclusivo ou padrão.
- Relação Projeto↔Quadro é calculada exclusivamente pelas tarefas atuais, sem vínculo manual persistente.
- Colocar uma tarefa em um quadro exige acesso efetivo tanto ao projeto quanto ao quadro.
- Quadro não é proprietário do projeto e projeto não é proprietário do quadro.
- Projeto nunca é excluído permanentemente nem enviado para lixeira; somente arquivado ou restaurado.
- Todo integrante de equipe vinculada herda perfil-base Membro no projeto enquanto ao menos uma origem de acesso
  válida permanecer.
- Arquivar nunca apaga fisicamente dados do conjunto.
- Um projeto arquivado não aparece nas superfícies normais nem aceita mutações operacionais.
- Nenhuma operação de projeto atravessa organizações.
- Apenas Administrador e Gestor possuem gestão de projetos por perfil-base.
- Negações explícitas podem restringir o baseline conforme D55.
- Restauração reativa somente tarefas arquivadas pela mesma operação ou cadeia identificável.
- Arquivar ou restaurar projeto nunca muda o estado do quadro transversal independente.
- Arquivamento e restauração não reescrevem nem apagam histórico.

## Estado atual comprovado no código

- `Project` possui `IsArchived`, `ArchivedAt`, `Archive()` e `Reactivate()`.
- `ProjectsController` expõe criação, atualização, arquivamento e reativação.
- A listagem aceita `includeArchived`; a interface possui `Exibir arquivados`.
- `SetProjectArchivedCommandHandler` exige hoje `ProjectRole.ProjectAdmin`, altera somente o próprio `Project` e
  grava evento `archived` ou `reactivated`.
- `UpdateProjectCommandHandler` também exige `ProjectRole.ProjectAdmin`.
- `CreateProjectCommandHandler` exige `PlatformPermission.Create` no escopo `Project` quando o serviço de
  permissão está presente.
- A matriz atual permite `Create` para perfis legados como `TeamMember` e `Developer`, sem uma capacidade
  específica `ManageProjects`.
- `ProjectAccessService` eleva `Administrator`, `Manager` e `ProjectManager` organizacionais a
  `ProjectRole.ProjectAdmin`; proprietário ou membro com `ProjectAdmin` também consegue editar/arquivar.
- `Project` já possui coleções `Members` e `Teams`, e a API expõe operações para adicionar/remover múltiplos
  membros e equipes.
- Os membros do projeto ainda recebem um `ProjectRole` fixo; o acesso de membros das equipes vinculadas não está
  refletido de forma uniforme em todas as consultas de projeto.
- O contrato atual de criação ainda expõe campos além de nome e descrição e depende de adequação à D56/D57.
- O repositório filtra o projeto arquivado nas listagens normais, mas não há filtro de conjunto comprovado.
- `Board` não possui hoje estado próprio de arquivamento.
- `WorkItem` possui arquivamento próprio, mas o handler de arquivamento de projeto não arquiva suas tarefas.
- A tela de configurações afirma que arquivar preserva tarefas, horas, sprints e histórico e oferece reativação,
  mas não comprova a ocultação dos quadros e tarefas.
- Os testes atuais de projeto cobrem metodologia, criação sem sprint automática e preservação de configurações;
  não cobrem autorização D55 nem arquivamento/restauração do conjunto.

## Gaps entre contrato e implementação

1. **Autorização de criação:** perfis legados de Membro/Developer recebem `Create` genérico e podem não respeitar
   o baseline exclusivo de Administrador/Gestor.
2. **Autorização de edição/arquivo:** depende de `ProjectRole.ProjectAdmin`, uma hierarquia sucedida pela D55.
3. **Permissão granular:** não existe uma capacidade explícita de administrar projetos no catálogo atual.
4. **Vínculos:** múltiplos membros/equipes existem, mas membros ainda usam `ProjectRole` e o acesso por equipe não
   segue integralmente o modelo único da D55.
5. **Herança por equipe:** não há resolução uniforme que conceda Membro ao vincular/entrar em equipe e revogue
   somente essa origem ao sair, preservando vínculos individuais e por outras equipes.
6. **Criação mínima:** frontend/DTO/validação ainda precisam ser comprovados com somente nome e descrição e sem
   exposição da chave técnica.
7. **Criação de quadro legado:** fluxos atuais ainda podem criar ou exigir quadro padrão por projeto, em conflito
   com a independência da D52/D57.
8. **Estados:** modelos/contratos precisam ser auditados para expor somente Ativo e Arquivado como estados do projeto.
9. **Arquivamento parcial:** o handler marca somente `Project.IsArchived`.
10. **Tarefas:** tarefas do projeto não são ocultadas ou bloqueadas automaticamente pelo arquivamento do projeto.
11. **Atomicidade:** não existe transação composta comprovada para projeto e tarefas.
12. **Restauração segura:** não existe marcador que diferencie tarefa já arquivada de tarefa arquivada pelo
   projeto, necessário para evitar reativação indevida.
13. **Quadros transversais:** o código atual ainda usa quadro por projeto/N:N e não consegue aplicar integralmente
    a compatibilidade definida com a D52.
14. **Relação derivada:** vínculos legados `ProjectId`, quadro padrão ou associações explícitas precisam deixar de
    governar Projeto↔Quadro; filtros ainda não são comprovadamente derivados da existência atual de tarefas.
15. **Exclusão:** endpoints, ações ou políticas genéricas de exclusão/lixeira devem ser auditados para garantir que
    não se apliquem a projeto.
16. **Cobertura:** faltam testes de autorização, vínculos múltiplos, acesso por equipe, relação derivada, filtros,
    ausência de exclusão, ocultação integral, mutações
    bloqueadas, restauração e concorrência.
17. **Homologação:** criação, edição, vínculos, arquivamento e restauração ainda não foram homologados por perfil.

## Persistência e migration futura

A implementação precisa identificar a causa e o momento do arquivamento das tarefas para restaurar apenas o
conjunto correto, sem mudar o estado dos quadros transversais independentes da D52.

Qualquer coluna, tabela, índice, relação ou backfill novo exige `G-MIGRATION`. A preparação deve incluir:

- relatório de projetos arquivados atuais;
- contagem de tarefas e respectivas referências históricas a quadros/colunas;
- detecção de recursos previamente arquivados;
- detecção de vínculos legados em que quadro ainda é tratado como filho/padrão do projeto;
- operação idempotente, backup, rollback e validação de órfãos;
- compatibilidade durante a transição do modelo de autorização D55 e do quadro transversal D52.

## API e interface esperadas

- Listar projetos ativos por padrão e arquivados somente sob filtro explícito.
- Criar projeto recebendo somente nome e descrição; a resposta pode conter a chave técnica sem torná-la editável
  ou visível na experiência funcional.
- Criar, editar, arquivar e restaurar validando D55 no servidor.
- Vincular/desvincular várias equipes e vários membros individuais, sem duplicidade, recalculando imediatamente
  o acesso herdado de Membro e preservando outras origens válidas conforme D55.
- Listar projetos disponíveis no filtro do quadro por consulta derivada das tarefas atuais, sem persistir vínculo
  Projeto↔Quadro.
- Criar/mover tarefa para quadro somente após validar acesso efetivo ao projeto e ao quadro.
- Não expor ação ou endpoint de excluir projeto ou enviá-lo para lixeira.
- Consultar previamente o impacto do arquivamento: projeto e tarefas que ficarão ocultos; quadros preservados.
- Arquivar/restaurar em operação transacional e devolver erro sem estado parcial.
- Esconder ações de gestão para perfis sem permissão, sem depender disso como proteção.
- Após arquivar, remover o projeto e seus recursos do cache/estado visível sem exigir reload manual.
- Após restaurar, invalidar as visões pertinentes e permitir navegação somente depois da confirmação do servidor.
- Exibir confirmação clara antes do arquivamento e feedback de sucesso/erro.

## Critérios de aceite

- **Dado** um Administrador ou Gestor autorizado, **quando** cria ou edita um projeto válido, **então** a operação
  conclui e registra histórico.
- **Dado** nome e descrição válidos, **quando** Administrador ou Gestor cria um projeto, **então** nenhum campo
  adicional é exigido e a chave é gerada automaticamente sem ser exibida no formulário.
- **Dado** um Membro, Visualizador ou Externo sem concessão adicional, **quando** tenta criar, editar ou arquivar
  um projeto pela API, **então** recebe acesso negado e nenhum estado muda.
- **Dado** um projeto com várias equipes e membros individuais, **quando** Administrador ou Gestor consulta sua
  composição, **então** todos os vínculos válidos aparecem sem duplicidade e dentro do tenant ativo.
- **Dado** uma equipe vinculada, **quando** uma pessoa integra essa equipe, **então** recebe acesso de Membro ao
  projeto sem cadastro individual adicional.
- **Dado** um integrante com acesso apenas pela equipe, **quando** sai dela, **então** perde o acesso ao projeto.
- **Dado** um integrante com vínculo individual ou por outra equipe, **quando** sai de uma equipe vinculada,
  **então** mantém o acesso proveniente das origens restantes.
- **Dado** um projeto ativo com tarefas em um ou mais quadros, **quando** é arquivado, **então** o projeto e suas
  tarefas deixam de aparecer nas telas normais, nenhum dado é apagado e nenhum quadro é arquivado.
- **Dado** uma tarefa de projeto arquivado, **quando** alguém tenta editar, mover, atribuir ou apontar horas,
  **então** a operação é recusada.
- **Dado** qualquer quadro transversal, **quando** um de seus projetos representados é arquivado, **então** o
  quadro permanece no mesmo estado e somente as tarefas do projeto arquivado deixam de aparecer.
- **Dado** acesso ao Projeto A e ao Quadro X, **quando** uma tarefa do projeto é criada ou movida para o quadro,
  **então** o Projeto A passa a aparecer no filtro do Quadro X sem vínculo manual adicional.
- **Dado** a última tarefa do Projeto A no Quadro X, **quando** ela sai desse quadro, **então** o projeto deixa de
  aparecer no filtro do quadro sem apagar projeto, quadro ou histórico.
- **Dado** falta de acesso ao projeto ou ao quadro, **quando** alguém tenta criar/mover uma tarefa entre eles,
  **então** a operação é negada sem persistência parcial.
- **Dado** qualquer perfil, **quando** consulta as ações e endpoints de projeto, **então** não existe exclusão nem
  lixeira; Administrador e Gestor utilizam somente Arquivar e Restaurar.
- **Dado** um projeto arquivado, **quando** usuário autorizado ativa `Exibir arquivados`, **então** consegue
  consultar o conjunto preservado sem reativá-lo.
- **Dado** um projeto e recursos arquivados juntos, **quando** a restauração é confirmada, **então** voltam os
  recursos dessa operação sem reativar o que já estava arquivado antes.
- **Dado** uma falha em qualquer parte do arquivamento/restauração, **quando** a transação termina, **então**
  projeto e tarefas permanecem integralmente no estado anterior; nenhum quadro tem seu estado alterado.

## Testes e homologação necessários

- Domínio: estados e regras de arquivamento/restauração.
- Aplicação: Administrador/Gestor autorizados; demais perfis negados por padrão; deny explícito prevalente.
- Integração SQL Server: atomicidade, concorrência, origem do arquivamento, preservação do quadro e rollback.
- API: create/update/archive/reactivate, tenant, `403/404/409` e ausência de mutação parcial.
- React: visibilidade das ações, confirmação, filtro de arquivados e atualização sem reload.
- Playwright: jornada por perfil, ocultação do conjunto e restauração segura em banco E2E dedicado.
- Homologação manual por PO antes de promoção.

## Human Gates

- `G-SPEC`: aprovado explicitamente por PO para este contrato documental.
- `G-SCOPE`: decisão registrada na D57 e coordenada com D52/D55.
- `G-MIGRATION`: obrigatório se a implementação alterar schema, relações ou backfill.
- `G-WORKFLOW`: obrigatório somente se o arquivamento alterar regras de transição/status.
- `G-HISTORY`: obrigatório somente se a estrutura imutável dos eventos precisar mudar.
- `G-DEPLOY`: obrigatório antes de promoção institucional.

## Referências

- `AGENTS.md`.
- `DECISIONS.md` — D17, D20, D34, D48, D52, D55, D56, D57 e D66.
- `ROADMAP.md` — Fases 6D e 8.
- `specs/user-access-permissions.md`.
- `specs/project-key-auto-generation.md`.
- `specs/project-methodology-hidden.md`.
- `specs/projects-visual-refresh.md`.
- `src/Detran.Kanban.Domain/Entities/Project.cs`.
- `src/Detran.Kanban.Domain/Entities/Board.cs`.
- `src/Detran.Kanban.Domain/Entities/WorkItem.cs`.
- `src/Detran.Kanban.Application/Features/Projects/ProjectsFeature.cs`.
- `src/Detran.Kanban.Application/Features/Projects/ProjectManagementFeature.cs`.
- `src/Detran.Kanban.Infrastructure/Repositories/ProjectRepository.cs`.
- `src/Detran.Kanban.Api/Controllers/ProjectsController.cs`.
- `src/Detran.Kanban.Web/src/pages/ProjectSettings.tsx`.
- `tests/Detran.Kanban.Tests/ProjectFeatureTests.cs`.

## Rollback

Esta revisão altera somente documentação. A implementação futura deve permitir desativar o novo fluxo sem
apagar marcadores ou dados e somente reverter schema mediante novo `G-MIGRATION`.

## Rastreabilidade

Decisão humana de PO -> D55/D57 -> `SPEC-PROJECT-MANAGEMENT` (`approved`) -> gap documental -> futura
decomposição em tasks -> implementação com gates -> testes -> homologação manual.
