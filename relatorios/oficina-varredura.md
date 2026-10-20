# Varredura de qualidade da Oficina — somente leitura

## Escopo e estado

Leitura de código iniciada em20/10/2026; roteiro nativo1440x900/390x844 preparado, ainda não executado. Esta é uma versão preliminar e não certifica acessibilidade nem conclui o item5. Referência funcional:PR27, códigoe9023e7, basea2797a7. As outras propostas permanecem separadas; não há uma versão integrada nem homologação oficial. Nenhuma correção será implementada nesta etapa.

Critérios auxiliares: [Web Interface Guidelines](https://raw.githubusercontent.com/vercel-labs/web-interface-guidelines/main/command.md), consultadas em20/10/2026. Verificação orientada a nome acessível, teclado/foco, mudanças assíncronas, largura mobile e erro com próximo passo. Não é auditoria WCAG completa nem ensaio com leitor de tela real.

## Achados confirmados por código

| ID | Gravidade | Local | Problema e reprodução proposta | Evidência/limite |
|---|---|---|---|---|
| V01 | P2 | src/Prisma.Workspace.Web/src/pages/Requests.tsx:94 | Selecionar solicitação e chegar por teclado à resposta. O textarea depende do placeholder e não tem rótulo persistente associado, aria-label ou aria-labelledby. O heading não nomeia o controle; a dica visual desaparece ao digitar. | Código; detalhe com solicitação não foi aberto nesta varredura ainda. |
| V02 | P2 | src/Prisma.Workspace.Web/src/pages/Requests.tsx:66 | Em QA, simular falha do POST de resposta e enviar texto. A mutation não tem onError e o JSX não lê replyMutation.error; a falha não é apresentada ao atendente. | Fluxo confirmado por código; POST não será executado na etapa somente leitura. |
| V03 | P2 | src/Prisma.Workspace.Web/src/pages/OrganizationSettings.tsx:162 | Em QA, falhar alteração de perfil/ativo/nome de membro. updateMember.error não é apresentado; o formulário volta a ficar operável sem informar o motivo da falha. | Leitura da mutation e consumidores; sem alteração real de membro. |
| V04 | P2 | src/Prisma.Workspace.Web/src/pages/OrganizationSettings.tsx:184 | Falhar aplicação/remoção de permissão em QA. saveGrant/deleteGrant não têm onError nem saída error no JSX. O usuário não recebe explicação. | Código; nenhuma permissão alterada. |
| V05 | P2 | src/Prisma.Workspace.Web/src/features/portal/ExternalPortalSettings.tsx:132 | Abrir formulário do portal com regra de atribuição e navegar ao botão de remover regra. O botão contém apenas SVG, sem nome acessível. | Código; requer formulário/regra existente para confirmação nativa. Botão Remover campo já tem aria-label; é outro controle. |
| V06 | P2 | src/Prisma.Workspace.Web/src/pages/Requests.tsx:17 | Focar Buscar solicitações pelo teclado. A regra input{outline:0} tem especificidade maior que o :focus-visible global e não há substituto em SearchBox. | Código; confirmar estilo computado no navegador. |
| V07 | P3 | src/Prisma.Workspace.Web/src/pages/Auth.tsx:745 | Uma falha de autenticação aparece num div Alert sem role=alert/aria-live, sem associação ao campo e sem mover foco. O erro visual não é anunciado por esse componente. | Nome inválido de cadastro tem role=alert e não faz parte deste achado. Leitor de tela real não executado. |
| V08 | P3 | src/Prisma.Workspace.Web/src/layout/AppShell.tsx:40 | Entrar por teclado e tentar saltar a navegação para conteúdo. Não há link de salto nem alvo focável no main do shell. | Código dos três componentes do shell; confirmação DOM pendente. |
| V09 | P2 | src/Prisma.Workspace.Web/src/pages/Requests.tsx:79 | Simular falha de transporte no GET external-requests. O catch conserva Error.message do fetch e o render o mostra cru; pode surgir mensagem técnica em inglês sem orientação de recuperação. | Reprodução nativa por interceptação local de GET pendente; não atribuir falha real ao servidor. |
| V10 | P3 | src/Prisma.Workspace.Web/src/pages/OrganizationSettings.tsx:256 | Remover permissão chama deleteGrant.mutate diretamente, sem confirmação nem desfazer nessa tela. Clique acidental muda acesso imediatamente. | Código; não executado por ser mutação. |

P0/P1:nenhum confirmado nesta leitura limitada. A ausência não certifica segurança ou ausência de outros defeitos.

## Roteiro nativo preparado

Home, Projetos, Minhas tarefas, Solicitações, Relatórios, Configurações da organização, Backlog, Sprints, Quadro, Relatórios do projeto, Wiki e Configurações do projeto, nas duas larguras. Apenas navegação, GET, inspeção do DOM e capturas. Requisições de negócio diferentes deGET/HEAD/OPTIONS serão bloqueadas pelo roteiro; autenticação do setup de testes é separada. Métricas não copiam nomes, conteúdo, IDs ou credenciais. Falha de transporte será simulada apenas no browser, sem alterar API.

## Continuidade

Aguardar validações finais das branches para executar o roteiro sob trava global e completar evidências/capturas. Nenhuma mudança de produto, schema, workflow ou migration nesta etapa. Autorrevisão documental em andamento; auto-QA nativo pendente. REVISAO e QA oficiais:pendentes.
