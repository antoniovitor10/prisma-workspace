# SPEC-USER-ACCESS-PERMISSIONS: Perfis, Permissões e Escopos de Acesso

**Status:** approved

**Revisão funcional:** aprovada explicitamente por PO em 2026-08-24

**Homologação manual:** pendente

**Natureza:** contrato funcional desejado comparado ao estado atual do produto

## Propósito

Definir um único modelo de autorização para a organização, seus projetos, quadros, equipes, tarefas,
relatórios, formulários e solicitações. O modelo deve ser compreensível para pessoas usuárias, permitir
personalização sem multiplicar papéis fixos e aplicar menor privilégio em todas as APIs e interfaces.

Esta aprovação documenta o comportamento desejado. Ela não autoriza alterar enums, banco, endpoints ou
interface nesta etapa.

## Perfis-base aprovados

Todo usuário interno comum possui uma única associação de organização ativa, com exatamente um dos cinco perfis-base:

1. **Administrador** — autoridade funcional total dentro da organização ativa.
2. **Gestor** — administra a operação, projetos, quadros, colunas, equipes, sprints, relatórios, custos e
   aprovadores dentro dos escopos autorizados, sem controlar a autoridade máxima nem segredos.
3. **Membro** — executa o trabalho autorizado: consulta, cria ou edita tarefas conforme seu escopo, move,
   conclui, participa e aponta horas.
4. **Visualizador** — acesso somente de leitura aos recursos explicitamente autorizados.
5. **Externo** — acesso exclusivo ao portal público e às solicitações permitidas por protocolo/token; não
   recebe navegação nem acesso interno à organização.

`ScrumMaster`, `ProductOwner`, `Developer` e `ProjectManager` deixam de ser perfis-base de autorização. Esses
nomes podem existir futuramente como função, cargo, especialidade ou responsabilidade operacional, mas não
concedem permissões por si mesmos.

### Limite do Administrador da organização

O perfil-base Administrador possui autoridade funcional total **dentro de uma organização ativa**, mas não é
Administrador da plataforma. Conforme D58 e `SPEC-ORGANIZATIONS`, criar, editar, arquivar, consultar uma
organização arquivada e restaurá-la são ações exclusivas do control plane e não podem ser concedidas por perfil
personalizado, `PermissionGrant` ou papel do tenant. O Administrador da organização continua responsável pelos
recursos, membros e permissões internas autorizados pela D55, sem controlar o ciclo de vida do tenant.

Não existe uma segunda hierarquia fixa de papéis por projeto. Acesso mais amplo ou mais restrito em projeto,
quadro, equipe ou outro recurso é expresso por perfil personalizado, atribuição escopada ou override explícito.

## Matriz inicial dos perfis-base

`Escopo autorizado` significa que o ator ainda precisa possuir vínculo válido com o recurso e permanecer no
tenant ativo. Nenhum perfil atravessa organizações.

| Capacidade | Administrador | Gestor | Membro | Visualizador | Externo |
|---|---|---|---|---|---|
| Visualizar recursos internos | Toda a organização | Operação sob gestão | Escopos autorizados | Escopos autorizados, somente leitura | Não |
| Criar e editar tarefas | Sim | Sim, no escopo | Sim, no escopo | Não | Não |
| Mover e concluir tarefas | Sim | Sim, no escopo | Sim, no escopo | Não | Não |
| Participar e apontar horas | Sim | Sim, no escopo | Sim, no escopo | Não | Não |
| Arquivar/excluir tarefas | Sim | Conforme permissão e escopo | Somente quando regra funcional específica permitir | Não | Não |
| Administrar quadros e colunas | Sim | Sim, no escopo | Não por padrão | Não | Não |
| Criar, editar, arquivar e restaurar projetos | Sim | Sim, no escopo | Não por padrão | Não | Não |
| Administrar equipes e sprints | Sim | Sim, no escopo | Não por padrão | Não | Não |
| Definir aprovadores e regras de aprovação | Sim | Sim, no escopo | Não | Não | Não |
| Ver relatórios | Toda a organização | Equipes/escopos geridos | Apenas seus dados, conforme spec de relatórios | Leitura explicitamente concedida | Não |
| Ver custos e orçamento | Sim | Sim, no escopo | Não | Não | Não |
| Criar/configurar perfis personalizados | Sim | Sim, sem autoridade máxima ou segredos | Não | Não | Não |
| Administrar membros da organização | Sim | Não por padrão | Não | Não | Não |
| Administrar configurações máximas e segredos | Sim | Não | Não | Não | Não |
| Usar portal e acompanhamento por token | Quando aplicável | Quando aplicável | Quando aplicável | Quando aplicável | Sim, somente esse canal |

A matriz é o baseline. Uma concessão personalizada nunca pode ultrapassar limites estruturais, tenant,
proteções de segurança ou a autoridade que o concedente possui.

### Acesso de projeto herdado por equipe

- Um projeto pode vincular várias equipes e vários membros individuais conforme `SPEC-PROJECT-MANAGEMENT`.
- Cada integrante de uma equipe vinculada herda automaticamente perfil-base Membro naquele projeto.
- Quem entra depois na equipe recebe o acesso; quem sai perde somente essa origem de acesso.
- Vínculo individual ou vínculo por outra equipe mantém o acesso mesmo após a saída de uma das equipes.
- A resolução consolida as origens sem duplicar perfil, aplica deny explícito antes dos allows e nunca atravessa tenant.

## Perfis personalizados

- Administradores e gestores podem criar perfis personalizados como conjuntos reutilizáveis de permissões.
- Todo membro interno conserva um perfil-base; perfis personalizados complementam o baseline e podem ser
  atribuídos em um escopo específico.
- Um perfil personalizado possui nome, descrição, conjunto fechado de permissões, escopos permitidos, estado
  ativo e histórico de alterações.
- Um perfil personalizado não usa cargo ou função como fonte implícita de autorização.
- Gestores só podem incluir permissões que eles próprios possuam e nunca podem conceder:
  - autoridade equivalente a Administrador;
  - administração da organização ou do último administrador;
  - gestão da política máxima de permissões;
  - acesso a segredos, credenciais ou configurações de infraestrutura.
- Administradores podem configurar todo o catálogo funcional da organização, respeitando proteções do último
  administrador, isolamento multitenant e ações reservadas à operação da plataforma, inclusive o ciclo de vida
  da própria organização definido na D58.
- Desativar ou remover um perfil personalizado não apaga usuários, recursos nem histórico. Suas concessões
  deixam de produzir efeito após a atualização autorizada.

## Catálogo funcional de permissões

O catálogo deve separar, no mínimo, as seguintes capacidades; a implementação pode usar identificadores em
inglês, mas a interface deve apresentar rótulos claros em português:

- visualizar, criar, editar, arquivar/excluir;
- atribuir responsáveis e participantes;
- mover ou alterar o estado de tarefas;
- apontar e corrigir horas;
- responder solicitações externas;
- administrar quadros e colunas;
- administrar projetos;
- administrar equipes;
- administrar sprints;
- visualizar, criar e exportar relatórios;
- visualizar custos e orçamento;
- definir aprovadores e configurar aprovações;
- administrar membros;
- administrar perfis e permissões;
- administrar configurações da organização;
- administrar configurações máximas de segurança e segredos.

Permissões de restauração ou outras ações de risco podem permanecer mais restritas do que criar/editar. Por
exemplo, `SPEC-BOARDS-STAGES-WIP` reserva a restauração de quadro arquivado ao Administrador.

## Escopos de permissão

O modelo deve suportar os escopos:

- **Organização**;
- **Equipe**;
- **Projeto**;
- **Quadro**;
- **Tarefa**;
- **Relatório**;
- **Formulário**;
- **Solicitação**.

`Quadro` é um escopo de primeira classe. A autorização de quadro não pode ser simulada apenas por um projeto,
pois o quadro transversal aprovado na D52 pode reunir tarefas de diferentes projetos.

Sprints são autorizadas pelo projeto correspondente enquanto não houver decisão futura que exija um escopo
próprio. Subtarefas herdam o contexto da tarefa, sem criar outro tipo de escopo.

## Acesso derivado do quadro aos projetos

- Somente usuários e equipes vinculados ao quadro podem visualizá-lo, conforme D52.
- O acesso ao quadro concede acesso derivado aos projetos representados por suas tarefas no mesmo nível
  funcional possuído no quadro.
- Esse acesso derivado existe somente enquanto o vínculo com o quadro e a representação do projeto existirem.
- A derivação não cria um papel fixo de projeto e não apaga concessões independentes.
- Uma negação explícita aplicável continua prevalecendo sobre o acesso derivado.
- A derivação nunca atravessa a organização ativa nem concede administração da organização, membros, perfis,
  segredos ou configurações máximas.

## Overrides e precedência

Concessões e negações explícitas podem ser atribuídas a usuário, perfil personalizado ou vínculo escopado. A
decisão deve ser calculada no servidor nesta ordem:

1. validar autenticação, associação ativa e organização ativa;
2. aplicar limites estruturais e de segurança que não podem ser delegados;
3. considerar todos os escopos aplicáveis ao recurso, inclusive acesso derivado do quadro;
4. se existir qualquer **negação explícita** aplicável, negar;
5. caso contrário, se existir **concessão explícita** aplicável, permitir;
6. caso contrário, usar os perfis personalizados ativos aplicáveis;
7. caso contrário, usar o perfil-base;
8. na ausência de permissão positiva, negar por padrão.

Uma concessão mais específica não supera uma negação aplicável. Um acesso compartilhado, relatório salvo,
link interno ou controle exibido na interface não amplia permissão. A API é sempre a autoridade final.

## Invariantes

- Cada membro interno ativo possui exatamente um perfil-base na organização.
- Usuário comum pertence a uma única organização; Administrador da plataforma alterna tenants por autoridade de
  control plane e não por múltiplas memberships comuns.
- Externo não recebe acesso interno por possuir protocolo ou token público.
- Administrador possui baseline funcional total na própria organização, sem acesso a outro tenant.
- Gestor não concede permissão que não possui nem cria um perfil equivalente a Administrador.
- Nenhum cargo, função ou nome de metodologia concede autorização implicitamente.
- Negação explícita aplicável prevalece sobre allow, perfil personalizado, perfil-base e acesso derivado.
- Sem concessão positiva, o resultado é acesso negado.
- A decisão é aplicada antes de consultar, agregar, exportar ou alterar dados.
- Interface e API produzem a mesma decisão de autorização; ocultar um botão não substitui validação no servidor.
- Alterações de perfil, permissão e vínculo são auditáveis e nunca removem histórico funcional.
- O último administrador ativo da organização não pode ser removido, rebaixado ou bloqueado de modo a deixar
  a organização sem autoridade administrativa.

## Estado atual comprovado no código

- `OrganizationRole` possui 10 valores: `Administrator`, `Manager`, `ProjectManager`, `ScrumMaster`,
  `ProductOwner`, `TeamMember`, `Developer`, `ExternalRequester`, `Client` e `Viewer`.
- `ProjectRole` possui 5 valores independentes: `Viewer`, `Member`, `ScrumMaster`, `ProductOwner` e
  `ProjectAdmin`.
- `OrganizationMember` persiste um único `OrganizationRole` por associação.
- `ProjectMember` persiste um segundo papel fixo por projeto.
- `PlatformPermission` possui 14 permissões genéricas e não representa de forma explícita todas as capacidades
  aprovadas, como administrar quadros, custos, aprovadores, perfis personalizados e segurança máxima.
- `PermissionScope` possui organização, equipe, projeto, tarefa, relatório, formulário e solicitação; não possui
  escopo `Board`.
- `PermissionGrant` oferece allow/deny explícito por usuário e escopo, mas não existe entidade de perfil
  personalizado reutilizável nem atribuição de perfil personalizado.
- `PermissionService` faz deny prevalecer sobre allow e usa `RolePermissionCatalog` como fallback do perfil-base.
- `RolePermissionCatalog` é uma matriz estática ligada aos 10 perfis atuais.
- `ProjectAccessService` compara a ordem numérica de `ProjectRole` e eleva `Administrator`, `Manager` e
  `ProjectManager` da organização para `ProjectAdmin`.
- `BoardAccessService` encontra o projeto do quadro e autoriza no escopo `Project`; não existe autorização
  própria por quadro nem acesso derivado Quadro -> Projetos.
- Convites, APIs e telas administrativas aceitam diretamente os enums atuais.
- O código protege o último administrador em operações de membro.

## Gaps entre contrato e implementação

1. **Perfis-base:** existem 10 perfis organizacionais em vez dos 5 aprovados.
2. **Duplicidade:** existe uma segunda hierarquia fixa de 5 papéis por projeto.
3. **Perfis personalizados:** não existe definição reutilizável nem atribuição escopada desses perfis.
4. **Escopo de quadro:** `PermissionScope.Board` não existe.
5. **Catálogo:** faltam permissões funcionais granulares para quadros, projetos, equipes, aprovações, custos,
   perfis personalizados e configurações máximas.
6. **Gestor:** a matriz atual é ampla por exclusão e não expressa claramente limites de autoridade máxima e
   segredos.
7. **Membro:** `TeamMember` e `Developer` duplicam comportamento semelhante.
8. **Funções Scrum/projeto:** `ScrumMaster`, `ProductOwner` e `ProjectManager` ainda concedem acesso por papel.
9. **Externo:** `ExternalRequester` e `Client` são dois perfis internos distintos; o contrato exige um perfil
   externo sem navegação interna.
10. **Quadro transversal:** acesso ainda é derivado do projeto, sentido inverso ao aprovado na D52.
11. **Precedência completa:** deny por usuário já prevalece no serviço atual, mas não há composição com perfis
    personalizados, vínculos de quadro/equipe e acesso derivado.
12. **Auditoria e homologação:** não há cobertura consolidada comprovando a nova matriz e suas fronteiras.

## Impacto de dados e migração futura

Esta revisão não altera o banco. A implementação futura provavelmente precisará:

- mapear os 10 `OrganizationRole` atuais para os 5 perfis-base;
- converter `ProjectMember.Role` em concessões/perfis escopados sem ampliar acesso;
- criar definições e atribuições de perfis personalizados;
- adicionar o escopo `Board` e os vínculos de acesso necessários;
- migrar grants atuais preservando allow/deny e autoria;
- manter temporariamente compatibilidade de leitura durante rollout e rollback;
- produzir relatório prévio de ambiguidades, permissões ampliadas/reduzidas e usuários sem mapeamento seguro.

Nenhuma migration pode ser criada ou aplicada sem `G-MIGRATION`. A substituição do modelo de autorização exige
plano de compatibilidade, backup, backfill verificável, contagens, auditoria e rollback. Mudanças no vínculo
Quadro -> Projetos devem ser coordenadas com a implementação da D52.

## Contratos e interface esperados

- A API deve retornar o perfil-base efetivo, perfis personalizados ativos, grants/denies escopados e capacidades
  resolvidas necessárias à interface, sem expor dados de outro usuário sem autorização.
- Administração de membros apresenta somente os cinco perfis-base.
- Administração de acesso permite criar, editar, desativar e atribuir perfis personalizados.
- O editor de perfil lista permissões por categoria e escopo, destaca itens sensíveis e impede que o Gestor
  salve autoridade máxima ou segredos.
- Projeto, quadro, equipe e demais superfícies permitem configurar acesso sem recriar papéis fixos locais.
- Controles não autorizados ficam ausentes ou desabilitados com explicação, mas chamadas diretas continuam
  protegidas pela API.
- Decisões de acesso negado usam `403`; recurso fora do tenant ou deliberadamente não enumerável pode usar
  `404`, conforme política do endpoint.

## Critérios de aceite

- **Dado** um novo membro interno, **quando** seu acesso é configurado, **então** escolhe-se exatamente um entre
  Administrador, Gestor, Membro ou Visualizador; Externo permanece restrito ao portal/token.
- **Dado** um usuário com função “Product Owner”, **quando** nenhuma permissão foi concedida, **então** o cargo
  não amplia seu acesso.
- **Dado** um Gestor, **quando** cria um perfil personalizado, **então** só pode incluir permissões que possui e
  não consegue conceder autoridade administrativa máxima ou segredos.
- **Dado** um perfil ou usuário com negação explícita aplicável, **quando** outra origem concede a mesma
  capacidade, **então** o acesso continua negado.
- **Dado** um Membro sem concessão para administrar quadros, **quando** chama a API diretamente, **então** a
  operação é recusada mesmo que a interface esteja desatualizada.
- **Dado** um Visualizador, **quando** acessa recurso autorizado, **então** consegue consultar, mas não alterar,
  mover, apontar horas, arquivar ou excluir.
- **Dado** um Externo com protocolo/token válido, **quando** acompanha uma solicitação, **então** acessa somente
  o contrato público e não enumera recursos internos.
- **Dado** acesso de Gestor a um quadro transversal, **quando** esse quadro representa tarefas de dois projetos,
  **então** o mesmo nível operacional é derivado para ambos sem conceder administração da organização.
- **Dado** acesso removido do quadro, **quando** não há outra concessão ao projeto, **então** o acesso derivado
  deixa de valer sem apagar dados ou histórico.
- **Dado** o último Administrador ativo, **quando** alguém tenta rebaixá-lo ou desativá-lo sem substituto,
  **então** a operação é recusada.

## Testes e homologação necessários

- Testes de domínio para matriz dos cinco perfis, default deny e limites do Gestor.
- Testes de precedência combinando base, perfil personalizado, allow, deny e acesso derivado.
- Testes de integração para todos os escopos, especialmente `Board`, tenant e não enumeração.
- Testes de migração para cada papel legado, grants atuais e papéis de projeto, sem ampliação silenciosa.
- Testes de administração de perfis personalizados, incluindo escalada de privilégio, último administrador,
  concorrência e auditoria.
- Testes de relatórios para Membro, Gestor e Administrador conforme `SPEC-DASHBOARDS-REPORTS`.
- Testes do portal garantindo que Externo não receba sessão ou navegação interna.
- Testes React e Playwright por perfil, desktop/mobile, menus, botões, rotas e chamadas diretas.
- Homologação manual da matriz por PO antes de promover o novo modelo.

## Human Gates

- `G-SPEC`: aprovado explicitamente por PO para este contrato documental.
- `G-SCOPE`: decisão arquitetural registrada na D55.
- `G-MIGRATION`: obrigatório antes de alterar enums persistidos, tabelas, vínculos, perfis ou escopos.
- `G-DEPLOY`: obrigatório antes de promover a nova autorização a ambiente institucional.
- `G-COMPLETION`: somente se a futura task for classificada com conclusão humana obrigatória.

## Referências

- `AGENTS.md`.
- `DECISIONS.md` — D12, D17, D18, D34, D46, D52 e D55.
- `ROADMAP.md` — Fases 6C e 8.
- `specs/auth-security.md`.
- `specs/boards-stages-wip.md`.
- `specs/project-management.md`.
- `specs/top-navigation-shell.md`.
- `specs/dashboards-reports.md`.
- `specs/external-portal.md`.
- `src/Detran.Kanban.Domain/Enums/OrganizationRole.cs`.
- `src/Detran.Kanban.Domain/Enums/ProjectRole.cs`.
- `src/Detran.Kanban.Domain/Enums/PlatformPermission.cs`.
- `src/Detran.Kanban.Domain/Enums/PermissionScope.cs`.
- `src/Detran.Kanban.Domain/Authorization/RolePermissionCatalog.cs`.
- `src/Detran.Kanban.Infrastructure/Identity/PermissionService.cs`.
- `src/Detran.Kanban.Infrastructure/Identity/ProjectAccessService.cs`.
- `src/Detran.Kanban.Infrastructure/Identity/BoardAccessService.cs`.

## Rollback

Esta revisão altera somente documentação. A futura implementação deve permitir retorno ao código anterior sem
apagar perfis, grants ou vínculos migrados e somente reverter schema mediante novo `G-MIGRATION`.

## Rastreabilidade

Decisão humana de PO -> D55 -> `SPEC-USER-ACCESS-PERMISSIONS` (`approved`) -> gap documental no backlog ->
futura decomposição em tasks -> implementação com gates -> testes automatizados -> homologação manual.
