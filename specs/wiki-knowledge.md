# SPEC-WIKI-001: Wiki e Base de Conhecimento

**Status:** approved
**Baseline:** contrato funcional revisado por PO em 2026-08-24, comparado com a implementação atual.

## Propósito

Disponibilizar uma base de conhecimento interna por projeto, integrada ao produto, com edição rica, anexos, organização de páginas, busca e histórico restaurável.

## Contrato funcional aprovado

### Disponibilidade e escopo

- A Wiki permanece disponível no produto.
- Cada documento pertence obrigatoriamente a um único projeto.
- Não existem documentos globais da organização, documentos pertencentes a quadros ou compartilhamento externo.
- Usuários somente podem consultar documentos de projetos aos quais possuem acesso.

### Permissões

- Administradores, Gestores e Membros do projeto podem criar e editar documentos.
- Visualizadores possuem somente leitura.
- Excluir, arquivar, restaurar ou administrar documentos depende de permissão configurável por perfil ou usuário.
- Conforme D55, os perfis-base são Administrador, Gestor, Membro, Visualizador e Externo; a autorização efetiva continua sendo avaliada no projeto e nas concessões configuráveis.

### Conteúdo e editor

- O editor deve aceitar texto formatado, imagens, anexos, links e tabelas.
- O conteúdo renderizado deve ser sanitizado contra scripts, atributos e URLs perigosos.
- Arquivos anexados seguem os controles de autorização do projeto e da página.

### Organização

- Páginas podem ser organizadas hierarquicamente dentro do projeto.
- Uma página pode ser vinculada a tarefas do mesmo projeto.
- Uma referência removida não deve corromper o documento nem expor conteúdo sem autorização.

### Histórico de versões

- Toda alteração de conteúdo deve preservar versão com autor e data.
- Usuários autorizados podem consultar uma versão anterior.
- Usuários autorizados podem restaurar uma versão anterior sem apagar o histórico já existente.
- Restauração cria um novo estado atual; não reescreve nem elimina revisões antigas.

### Busca

- A busca da Wiki deve considerar título, conteúdo e tags.
- Os resultados devem respeitar o projeto e a autorização do usuário.
- A busca não pode revelar título, trecho, tag ou existência de documento inacessível.

### Arquivamento e exclusão

- Arquivar e excluir são operações sujeitas à permissão configurável de gestão documental.
- Documentos removidos da navegação comum devem permanecer recuperáveis enquanto estiverem na lixeira ou arquivados.
- A operação não pode apagar o histórico de versões de forma silenciosa.
- Exclusão física e prazo de retenção não foram definidos nesta revisão.

### Compartilhamento externo

- Documentos da Wiki não podem ser compartilhados externamente por link público, link secreto ou portal externo.
- Endpoints e telas da Wiki exigem autenticação e acesso ao projeto.

## Fora do escopo atual

- Edição colaborativa simultânea em tempo real.
- Publicação pública na internet.
- Compartilhamento externo de documentos.
- Importação ou exportação em massa.
- Respostas por IA, busca semântica ou indexação externa.
- Fluxo editorial obrigatório com estados `Draft`, `InReview` e `Published`.
- Comentários em páginas.

## Estado atual comprovado no código

- A rota da Wiki existe dentro do workspace do projeto em `ProjectWiki`.
- `WikiPage` possui `ProjectId` obrigatório e `ParentPageId` para árvore hierárquica.
- O backend possui operações para árvore, criação, consulta, renomeação, conteúdo, movimentação, trava de edição, lixeira, restauração, histórico, anexos e vínculos com tarefas.
- O editor utiliza TipTap, salva HTML sanitizado e oferece formatação de texto, títulos, listas, citação, bloco de código e imagem incorporada.
- Anexos podem ser enviados, baixados e removidos.
- O histórico registra revisões com autor e data, permite consultar conteúdo anterior e restaurar uma revisão.
- A página usa `RowVersion` e trava temporária de edição; a trava expira após três minutos.
- O autosave agrupa alterações do mesmo autor em uma janela de sessão de 15 minutos.
- Páginas são removidas logicamente por `IsDeleted` e podem ser restauradas.
- A autorização atual permite leitura a partir de `ProjectRole.Viewer` e edição a partir de `ProjectRole.Member`.
- Não foi encontrado mecanismo de compartilhamento público da Wiki.

## Gaps entre contrato e implementação

1. **Busca ausente:** não foi comprovada busca de páginas por título ou conteúdo na API/interface.
2. **Tags ausentes:** não foi encontrado modelo, API ou interface de tags para páginas; portanto a busca por tags também não existe.
3. **Tabelas no editor:** o editor atual não comprova extensão ou controles para criar e editar tabelas.
4. **Links no editor:** o contrato exige links; a interface atual não evidencia controle específico e sua homologação continua pendente.
5. **Permissão configurável para excluir/arquivar:** hoje a autorização da Wiki é derivada do papel do projeto (`Member` ou superior para edição). Não foi comprovada permissão específica configurável para excluir, arquivar e restaurar.
6. **Arquivamento separado:** o código comprova lixeira/soft-delete, mas não um estado distinto de arquivamento documental.
7. **Perfis simplificados:** a implementação ainda usa os papéis legados do projeto e não aplica integralmente os cinco perfis-base e escopos definidos na D55.
8. **Homologação manual:** os fluxos de editor, imagens, anexos, histórico, restauração, hierarquia e vínculos ainda precisam ser validados pelo usuário na interface.

## Regras

1. Toda página deve possuir projeto, título, conteúdo, autor da alteração e controle de acesso.
2. Uma página não pode ser movida para outro projeto por uma simples reorganização da árvore.
3. Alterações não podem apagar revisões anteriores.
4. Restauração de versão precisa preservar a autoria e data da versão restaurada e registrar a nova restauração.
5. Usuário sem acesso ao projeto não pode consultar página, árvore, busca, histórico, anexos ou vínculos.
6. Conteúdo HTML deve ser sanitizado antes de persistir ou renderizar.
7. Edição concorrente não pode sobrescrever silenciosamente conteúdo protegido por versão ou trava válida.
8. Busca e vínculos não podem revelar conteúdo de outro projeto.
9. Operações de exclusão, arquivamento e restauração devem validar a permissão configurável correspondente.
10. Não pode existir rota pública de leitura da Wiki.

## Persistência

### Implementado

- `WikiPages` para página, projeto, hierarquia, posição, conteúdo, trava e lixeira.
- `WikiPageRevisions` para versões e autoria.
- `WikiAttachments` para metadados dos arquivos.
- `WikiPageWorkItemLinks` para vínculos com tarefas.

### Necessário para cumprir o contrato

- Estrutura de tags e associação entre tags e páginas.
- Índices ou mecanismo de pesquisa por título, conteúdo e tags.
- Permissão configurável para gestão documental, integrada ao modelo de autorização adotado.
- Eventual estado de arquivamento separado da lixeira, caso a implementação opte por distinguir as duas operações no banco.

Qualquer alteração de schema exige `G-MIGRATION`. Alterações na semântica imutável do histórico exigem `G-HISTORY`.

## API esperada

- Criar, consultar, atualizar, listar e reorganizar páginas no projeto.
- Arquivar, enviar à lixeira e restaurar conforme permissão.
- Obter histórico, consultar revisão e restaurar revisão.
- Pesquisar por título, conteúdo e tags respeitando autorização.
- Gerenciar tags, anexos, hierarquia e vínculos com tarefas.
- Manter controle de concorrência nas atualizações.
- Retornar erros coerentes para validação, autenticação, autorização, ausência, conflito e limite de requisições.

## Interface esperada

- Árvore de documentos do projeto.
- Campo de busca por título, conteúdo e tags.
- Editor rico com formatação, imagens, anexos, links e tabelas.
- Histórico com autor, data, pré-visualização e restauração.
- Controles de excluir, arquivar e restaurar somente quando autorizados.
- Estados distintos para vazio, carregando, sem permissão, não encontrado e erro.
- Nenhuma ação de compartilhamento externo.

## Critérios de aceite

- **Dado** um Administrador, Gestor ou Membro com acesso ao projeto, **quando** criar ou editar uma página válida, **então** o conteúdo é salvo no projeto e uma revisão com autor e data é preservada.
- **Dado** um Visualizador, **quando** abrir uma página autorizada, **então** pode ler, mas não alterar seu conteúdo.
- **Dado** um usuário sem acesso ao projeto, **quando** consultar a Wiki por qualquer endpoint, **então** conteúdo e existência não são revelados.
- **Dado** um termo presente no título, conteúdo ou tag, **quando** pesquisar, **então** a página autorizada aparece nos resultados.
- **Dado** uma versão anterior, **quando** um usuário autorizado restaurá-la, **então** ela se torna o conteúdo atual sem apagar as versões posteriores.
- **Dado** um perfil sem permissão de gestão documental, **quando** tentar excluir, arquivar ou restaurar, **então** a operação é recusada.
- **Dado** conteúdo potencialmente executável, **quando** salvar ou renderizar, **então** scripts e URLs inseguras não são executados.
- **Dado** um usuário autenticado, **quando** tentar gerar um link externo para a página, **então** essa capacidade não está disponível.
- **Dado** o editor rico, **quando** inserir texto formatado, imagem, anexo, link ou tabela, **então** o conteúdo permanece íntegro após salvar e reabrir.

## Testes e homologação

### Evidência automatizada necessária

- Testes unitários de autorização, sanitização, validação, concorrência e versionamento.
- Testes de integração para isolamento por projeto, revisões imutáveis, restauração, busca e lixeira/arquivamento.
- Testes da API para busca por título/conteúdo/tags, anexos, hierarquia, permissões e conflitos.
- Testes React para editor, árvore, busca, histórico, restauração e estados de permissão.
- Playwright para a jornada completa de criar, editar, anexar, pesquisar, consultar versão, restaurar, arquivar e recuperar.
- Testes de segurança para XSS, IDOR, vazamento pela busca e acesso indireto a anexos.

### Homologação manual pendente

1. Abrir a Wiki de um projeto e criar uma página e uma subpágina.
2. Editar e reabrir conteúdo formatado, imagem, anexo, link e tabela.
3. Conferir autor e data no histórico e restaurar uma versão.
4. Pesquisar termos presentes no título, conteúdo e tags.
5. Validar leitura por Visualizador e edição por Membro, Gestor e Administrador.
6. Validar que perfil sem permissão configurável não exclui, arquiva nem restaura.
7. Confirmar que não existe compartilhamento externo.

## Riscos

- XSS persistente por conteúdo rico mal sanitizado.
- Vazamento de conteúdo por busca, histórico, vínculo ou anexo.
- Perda de edição por concorrência ou falha de autosave.
- Crescimento do banco e armazenamento por revisões, imagens e anexos sem retenção definida.
- Confusão entre arquivamento e lixeira enquanto forem representados pela mesma operação.

## Decisões ainda abertas

- Política de retenção e exclusão física de páginas e versões.
- Limites de tamanho e quantidade para imagens e anexos da Wiki.
- Se arquivamento e lixeira terão estados persistentes separados.
- Política de nomes duplicados e slugs dentro do mesmo projeto.

## Referências

- `AGENTS.md`
- `DECISIONS.md`
- `ROADMAP.md`
- `specs/auth-security.md`
- `specs/work-item-management.md`
- `specs/task-history.md`
- `src/Detran.Kanban.Domain/Entities/WikiPage.cs`
- `src/Detran.Kanban.Domain/Entities/WikiPageRevision.cs`
- `src/Detran.Kanban.Api/Controllers/WikiController.cs`
- `src/Detran.Kanban.Application/Features/Wiki/WikiFeature.cs`
- `src/Detran.Kanban.Application/Features/Wiki/WikiPhase2Feature.cs`
- `src/Detran.Kanban.Web/src/pages/ProjectWiki.tsx`

## Rollback

Esta revisão altera somente o contrato documental. Nenhuma entidade, migration, endpoint, tela ou dado foi modificado.

## Rastreabilidade

Wiki por projeto → `SPEC-WIKI-001` → implementação atual + gaps documentados → testes automatizados → homologação manual.
